"""Creates a throwaway test vault with sample accounts for manual testing.

Usage: python tests\\seed_test_vault.py <data_dir>
Never point this at a real data folder: it refuses to touch an existing vault.
"""
from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "core"))

from backend.vault import VaultManager  # noqa: E402

TEST_MASTER_PASSWORD = "test1234"

# Includes reused and short passwords on purpose so the security check has
# something to report.
SAMPLE_ACCOUNTS = [
    {"service": "Google", "username": "taro.yamada@gmail.com", "password": "G00gle-Personal-2026!", "url": "https://accounts.google.com", "category": "個人", "favorite": True},
    {"service": "Google", "username": "work.taro@example.co.jp", "password": "W0rk-Account#Secure", "url": "https://accounts.google.com", "category": "仕事"},
    {"service": "GitHub", "username": "taro-dev", "password": "gh-Dev!Pa55word-xyz", "url": "github.com", "category": "仕事"},
    {"service": "Amazon", "username": "taro.yamada@gmail.com", "password": "shop1234", "url": "amazon.co.jp", "category": "買い物", "favorite": True},
    {"service": "Netflix", "username": "taro.yamada@gmail.com", "password": "shop1234", "url": "netflix.com"},
    {"service": "楽天銀行", "username": "taro0101", "password": "Bank#Rakuten-2026", "category": "金融"},
    {"service": "Wi-Fi（自宅）", "username": "", "password": "home-wifi-passphrase", "category": "個人"},
]


def main() -> int:
    if len(sys.argv) != 2:
        print("[ERROR] Usage: seed_test_vault.py <data_dir>")
        return 2
    data_dir = Path(sys.argv[1])
    vault_path = data_dir / "vault.kagibox"
    if vault_path.exists():
        print(f"[ERROR] {vault_path} already exists. Refusing to overwrite it.")
        return 1

    vault = VaultManager(vault_path)
    vault.create(TEST_MASTER_PASSWORD)
    for account in SAMPLE_ACCOUNTS:
        vault.add_account(account)
    vault.lock()
    print(f"[INFO] Test vault created with {len(SAMPLE_ACCOUNTS)} sample accounts.")
    print(f"[INFO] Master password: {TEST_MASTER_PASSWORD}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
