"""Argon2id key derivation + AES-256-GCM authenticated encryption primitives.

Every write path in this app must go through encrypt()/decrypt() here.
Nothing plaintext is ever allowed to reach disk.
"""
from __future__ import annotations

import base64
import os

from argon2.low_level import Type, hash_secret_raw
from cryptography.hazmat.primitives.ciphers.aead import AESGCM

# Tuned for interactive desktop unlock (~0.3-0.6s on modern hardware) while
# staying well above OWASP's Argon2id minimums for a local vault use case.
ARGON2_TIME_COST = 3
ARGON2_MEMORY_COST_KIB = 65536  # 64 MiB
ARGON2_PARALLELISM = 4
KEY_LEN = 32  # AES-256
SALT_LEN = 16
NONCE_LEN = 12  # recommended nonce size for AES-GCM


def generate_salt() -> bytes:
    return os.urandom(SALT_LEN)


def derive_key(master_password: str, salt: bytes) -> bytes:
    return hash_secret_raw(
        secret=master_password.encode("utf-8"),
        salt=salt,
        time_cost=ARGON2_TIME_COST,
        memory_cost=ARGON2_MEMORY_COST_KIB,
        parallelism=ARGON2_PARALLELISM,
        hash_len=KEY_LEN,
        type=Type.ID,
    )


def encrypt(key: bytes, plaintext: bytes, associated_data: bytes | None = None) -> tuple[bytes, bytes]:
    nonce = os.urandom(NONCE_LEN)
    ciphertext = AESGCM(key).encrypt(nonce, plaintext, associated_data)
    return nonce, ciphertext


def decrypt(key: bytes, nonce: bytes, ciphertext: bytes, associated_data: bytes | None = None) -> bytes:
    return AESGCM(key).decrypt(nonce, ciphertext, associated_data)


def b64e(data: bytes) -> str:
    return base64.b64encode(data).decode("ascii")


def b64d(data: str) -> bytes:
    return base64.b64decode(data.encode("ascii"))


def wipe(buf: bytearray) -> None:
    """Best-effort in-memory zeroing. CPython can't guarantee no copies were
    made elsewhere, but this closes the obvious window for the key material
    we control directly."""
    for i in range(len(buf)):
        buf[i] = 0
