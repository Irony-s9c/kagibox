"""2026-baseline random password generation and strength scoring."""
from __future__ import annotations

import math
import secrets

LOWER = "abcdefghijklmnopqrstuvwxyz"
UPPER = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
DIGITS = "0123456789"
SYMBOLS = "!@#$%^&*()-_=+[]{};:,.<>?"
AMBIGUOUS = set("0Ol1I|")


def generate_password(
    length: int = 16,
    use_upper: bool = True,
    use_lower: bool = True,
    use_digits: bool = True,
    use_symbols: bool = True,
    exclude_ambiguous: bool = True,
) -> str:
    length = max(8, min(64, length))

    pools = []
    if use_lower:
        pools.append(LOWER)
    if use_upper:
        pools.append(UPPER)
    if use_digits:
        pools.append(DIGITS)
    if use_symbols:
        pools.append(SYMBOLS)
    if not pools:
        pools = [LOWER]

    if exclude_ambiguous:
        pools = ["".join(c for c in pool if c not in AMBIGUOUS) for pool in pools]

    all_chars = "".join(pools)

    # Guarantee at least one char from every selected pool, then fill the rest.
    result = [secrets.choice(pool) for pool in pools]
    while len(result) < length:
        result.append(secrets.choice(all_chars))

    secrets.SystemRandom().shuffle(result)
    return "".join(result[:length])


def estimate_strength(password: str) -> dict:
    """Rough entropy-based strength estimate for the UI meter.

    Not a substitute for zxcvbn-style dictionary/pattern analysis, but
    sufficient for a local-only real-time gauge.
    """
    if not password:
        return {"score": 0, "label": "weak", "entropy_bits": 0}

    pool_size = 0
    if any(c in LOWER for c in password):
        pool_size += len(LOWER)
    if any(c in UPPER for c in password):
        pool_size += len(UPPER)
    if any(c in DIGITS for c in password):
        pool_size += len(DIGITS)
    if any(c in SYMBOLS for c in password):
        pool_size += len(SYMBOLS)
    if pool_size == 0:
        pool_size = 26

    entropy_bits = len(password) * math.log2(pool_size)

    if len(password) <= 10 or entropy_bits < 40:
        label, score = "weak", 1
    elif entropy_bits < 70:
        label, score = "medium", 2
    else:
        label, score = "strong", 3

    return {"score": score, "label": label, "entropy_bits": round(entropy_bits, 1)}
