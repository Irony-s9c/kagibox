"""Local-only security diagnostics: reused and weak passwords.

Operates purely on the in-memory decrypted item list handed to it by
VaultManager.all_items_for_health_check() -- nothing here touches disk or
logs plaintext.
"""
from __future__ import annotations

from collections import defaultdict
from typing import Any

WEAK_LENGTH_THRESHOLD = 10


def run(items: list[dict[str, Any]]) -> dict[str, Any]:
    by_password: dict[str, list[str]] = defaultdict(list)
    for item in items:
        pw = item.get("password", "")
        if pw:
            by_password[pw].append(item["id"])

    reused_ids: set[str] = set()
    for ids in by_password.values():
        if len(ids) > 1:
            reused_ids.update(ids)

    weak: list[dict[str, Any]] = []
    reused: list[dict[str, Any]] = []
    for item in items:
        pw = item.get("password", "")
        summary = {
            "id": item["id"],
            "service": item.get("service", ""),
            "username": item.get("username", ""),
            "password_length": len(pw),
        }
        if len(pw) <= WEAK_LENGTH_THRESHOLD:
            weak.append(summary)
        if item["id"] in reused_ids:
            reused.append(summary)

    total = len(items)
    issue_count = len({d["id"] for d in weak} | {d["id"] for d in reused})
    score = 100 if total == 0 else round(100 * (total - issue_count) / total)

    return {
        "total_accounts": total,
        "weak_passwords": weak,
        "reused_passwords": reused,
        "score": max(0, min(100, score)),
    }
