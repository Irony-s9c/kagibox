"""SemVer 2.0.0 parse / compare helper. Standard library only.

    from semver import parse, compare, tag_to_version, is_newer

    latest = tag_to_version(release["tag_name"])   # "v1.2.3" -> "1.2.3", invalid -> None
    if latest and is_newer(latest, CURRENT_VERSION):
        show_update_dialog(latest)

Do NOT use packaging.version or distutils for this: their ordering differs from SemVer.
"""
from __future__ import annotations

import re
from dataclasses import dataclass
from typing import Optional, Tuple, Union

__all__ = ["SEMVER_RE", "SemVer", "parse", "is_valid", "compare", "is_newer", "tag_to_version"]

SEMVER_RE = re.compile(
    r"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)"
    r"(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?"
    r"(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$",
    re.ASCII,
)


@dataclass(frozen=True)
class SemVer:
    major: int
    minor: int
    patch: int
    prerelease: Tuple[str, ...] = ()
    build: Tuple[str, ...] = ()

    def __str__(self) -> str:
        s = f"{self.major}.{self.minor}.{self.patch}"
        if self.prerelease:
            s += "-" + ".".join(self.prerelease)
        if self.build:
            s += "+" + ".".join(self.build)
        return s


def parse(text: str) -> Optional[SemVer]:
    """Return SemVer for a valid version string (no 'v' prefix), otherwise None."""
    if not isinstance(text, str):
        return None
    # fullmatch + no surrounding whitespace ("1.2.3\n" must be rejected; '$' alone allows it)
    m = SEMVER_RE.fullmatch(text)
    if m is None or text != text.strip():
        return None
    pre = tuple(m.group(4).split(".")) if m.group(4) else ()
    build = tuple(m.group(5).split(".")) if m.group(5) else ()
    return SemVer(int(m.group(1)), int(m.group(2)), int(m.group(3)), pre, build)


def is_valid(text: str) -> bool:
    return parse(text) is not None


def _cmp_ident(a: str, b: str) -> int:
    a_num, b_num = a.isdigit(), b.isdigit()
    if a_num and b_num:
        ia, ib = int(a), int(b)
        return (ia > ib) - (ia < ib)
    if a_num:
        return -1  # numeric identifiers always have lower precedence
    if b_num:
        return 1
    return (a > b) - (a < b)  # ASCII order


def _as_semver(v: Union[str, SemVer]) -> SemVer:
    if isinstance(v, SemVer):
        return v
    p = parse(v)
    if p is None:
        raise ValueError(f"invalid SemVer: {v!r}")
    return p


def compare(a: Union[str, SemVer], b: Union[str, SemVer]) -> int:
    """-1 if a < b, 0 if equal precedence, 1 if a > b. Build metadata is ignored."""
    x, y = _as_semver(a), _as_semver(b)
    for p, q in ((x.major, y.major), (x.minor, y.minor), (x.patch, y.patch)):
        if p != q:
            return 1 if p > q else -1
    if not x.prerelease and not y.prerelease:
        return 0
    if not x.prerelease:
        return 1
    if not y.prerelease:
        return -1
    for ia, ib in zip(x.prerelease, y.prerelease):
        c = _cmp_ident(ia, ib)
        if c:
            return c
    n, m = len(x.prerelease), len(y.prerelease)
    return (n > m) - (n < m)


def is_newer(candidate: Union[str, SemVer], current: Union[str, SemVer]) -> bool:
    return compare(candidate, current) > 0


def tag_to_version(tag: str) -> Optional[str]:
    """'v1.2.3' -> '1.2.3'. Only one lowercase 'v' is stripped. Invalid -> None (ignore that tag)."""
    if not isinstance(tag, str):
        return None
    s = tag[1:] if tag.startswith("v") else tag
    return s if is_valid(s) else None
