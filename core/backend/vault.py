"""Encrypted vault storage.

On disk, every item is its own AES-256-GCM ciphertext (including fields such
as service/username that a naive implementation might leave in the clear).
Once unlocked, the decrypted items live only in process memory
(VaultManager._items) and are never written back out in plaintext. The API
layer (backend/api.py) is responsible for stripping the "password" field
before anything is handed to the JS frontend, and only decrypts/returns a
single password when explicitly requested.
"""
from __future__ import annotations

import json
import os
import time
import uuid
from pathlib import Path
from typing import Any

from . import crypto_utils as cu

VAULT_MARKER = b"KAGIBOX-VAULT-OK"


class VaultLockedError(Exception):
    pass


class InvalidMasterPasswordError(Exception):
    pass


class VaultManager:
    def __init__(self, vault_path: Path):
        self.vault_path = vault_path
        self._key: bytearray | None = None
        self._salt: bytes | None = None
        self._items: dict[str, dict[str, Any]] = {}
        self._unlocked = False

    # -- lifecycle -----------------------------------------------------
    @property
    def is_unlocked(self) -> bool:
        return self._unlocked

    def exists(self) -> bool:
        return self.vault_path.exists()

    def create(self, master_password: str) -> None:
        salt = cu.generate_salt()
        key = cu.derive_key(master_password, salt)
        nonce, ciphertext = cu.encrypt(key, VAULT_MARKER)
        self._key = bytearray(key)
        self._salt = salt
        self._items = {}
        self._unlocked = True
        self._write(
            {
                "version": 1,
                "kdf": {
                    "salt": cu.b64e(salt),
                    "time_cost": cu.ARGON2_TIME_COST,
                    "memory_cost": cu.ARGON2_MEMORY_COST_KIB,
                    "parallelism": cu.ARGON2_PARALLELISM,
                },
                "verifier": {"nonce": cu.b64e(nonce), "ciphertext": cu.b64e(ciphertext)},
                "items": [],
            }
        )

    def unlock(self, master_password: str) -> None:
        raw = self._read()
        kdf = raw["kdf"]
        salt = cu.b64d(kdf["salt"])
        key = cu.derive_key(master_password, salt)

        verifier = raw["verifier"]
        try:
            marker = cu.decrypt(key, cu.b64d(verifier["nonce"]), cu.b64d(verifier["ciphertext"]))
        except Exception as exc:  # wrong password / tampered file -> auth tag fails
            raise InvalidMasterPasswordError() from exc
        if marker != VAULT_MARKER:
            raise InvalidMasterPasswordError()

        items: dict[str, dict[str, Any]] = {}
        for entry in raw["items"]:
            plaintext = cu.decrypt(key, cu.b64d(entry["nonce"]), cu.b64d(entry["ciphertext"]), entry["id"].encode())
            items[entry["id"]] = json.loads(plaintext)

        self._key = bytearray(key)
        self._salt = salt
        self._items = items
        self._unlocked = True

    def lock(self) -> None:
        if self._key is not None:
            cu.wipe(self._key)
        self._key = None
        self._salt = None
        self._items = {}
        self._unlocked = False

    # -- CRUD (operates on the in-memory decrypted cache, then persists) --
    def list_accounts(self) -> list[dict[str, Any]]:
        self._require_unlocked()
        out = []
        for item_id, item in self._items.items():
            out.append(
                {
                    "id": item_id,
                    "service": item.get("service", ""),
                    "username": item.get("username", ""),
                    "url": item.get("url", ""),
                    "category": item.get("category", ""),
                    "tags": item.get("tags", []),
                    "favorite": item.get("favorite", False),
                    "password_length": len(item.get("password", "")),
                }
            )
        return out

    def get_password(self, item_id: str) -> str:
        self._require_unlocked()
        item = self._items.get(item_id)
        if item is None:
            raise KeyError(item_id)
        return item.get("password", "")

    def get_item_full(self, item_id: str) -> dict[str, Any]:
        self._require_unlocked()
        item = self._items.get(item_id)
        if item is None:
            raise KeyError(item_id)
        return {**item, "id": item_id}

    def add_account(self, data: dict[str, Any]) -> str:
        self._require_unlocked()
        item_id = str(uuid.uuid4())
        now = time.time()
        self._items[item_id] = {
            "service": data.get("service", ""),
            "username": data.get("username", ""),
            "password": data.get("password", ""),
            "url": data.get("url", ""),
            "category": data.get("category", ""),
            "tags": data.get("tags", []),
            "favorite": bool(data.get("favorite", False)),
            "notes": data.get("notes", ""),
            "created_at": now,
            "updated_at": now,
        }
        self._persist()
        return item_id

    def update_account(self, item_id: str, data: dict[str, Any]) -> None:
        self._require_unlocked()
        item = self._items.get(item_id)
        if item is None:
            raise KeyError(item_id)
        for field in ("service", "username", "password", "url", "category", "tags", "favorite", "notes"):
            if field in data:
                item[field] = data[field]
        item["updated_at"] = time.time()
        self._persist()

    def delete_account(self, item_id: str) -> None:
        self._require_unlocked()
        self._items.pop(item_id, None)
        self._persist()

    def toggle_favorite(self, item_id: str) -> bool:
        self._require_unlocked()
        item = self._items[item_id]
        item["favorite"] = not item.get("favorite", False)
        self._persist()
        return item["favorite"]

    def all_items_for_health_check(self) -> list[dict[str, Any]]:
        self._require_unlocked()
        return [{"id": k, **v} for k, v in self._items.items()]

    # -- import / export (still encrypted, just a portable container) --
    def export_encrypted_blob(self) -> dict[str, Any]:
        self._require_unlocked()
        return self._read()

    def import_encrypted_blob(self, raw: dict[str, Any], master_password: str) -> None:
        """Validates the incoming file decrypts with the given password
        before replacing the current vault on disk."""
        kdf = raw["kdf"]
        salt = cu.b64d(kdf["salt"])
        key = cu.derive_key(master_password, salt)
        verifier = raw["verifier"]
        try:
            marker = cu.decrypt(key, cu.b64d(verifier["nonce"]), cu.b64d(verifier["ciphertext"]))
        except Exception as exc:  # wrong password -> auth tag fails
            raise InvalidMasterPasswordError() from exc
        if marker != VAULT_MARKER:
            raise InvalidMasterPasswordError()
        self._write(raw)

    # -- internal --------------------------------------------------------
    def _require_unlocked(self) -> None:
        if not self._unlocked or self._key is None:
            raise VaultLockedError()

    def _persist(self) -> None:
        assert self._key is not None and self._salt is not None
        items_out = []
        for item_id, item in self._items.items():
            plaintext = json.dumps(item).encode("utf-8")
            nonce, ciphertext = cu.encrypt(bytes(self._key), plaintext, item_id.encode())
            items_out.append({"id": item_id, "nonce": cu.b64e(nonce), "ciphertext": cu.b64e(ciphertext)})

        verifier_nonce, verifier_ct = cu.encrypt(bytes(self._key), VAULT_MARKER)
        self._write(
            {
                "version": 1,
                "kdf": {
                    "salt": cu.b64e(self._salt),
                    "time_cost": cu.ARGON2_TIME_COST,
                    "memory_cost": cu.ARGON2_MEMORY_COST_KIB,
                    "parallelism": cu.ARGON2_PARALLELISM,
                },
                "verifier": {"nonce": cu.b64e(verifier_nonce), "ciphertext": cu.b64e(verifier_ct)},
                "items": items_out,
            }
        )

    def _write(self, raw: dict[str, Any]) -> None:
        self.vault_path.parent.mkdir(parents=True, exist_ok=True)
        tmp_path = self.vault_path.with_suffix(".tmp")
        tmp_path.write_text(json.dumps(raw), encoding="utf-8")
        os.replace(tmp_path, self.vault_path)

    def _read(self) -> dict[str, Any]:
        return json.loads(self.vault_path.read_text(encoding="utf-8"))
