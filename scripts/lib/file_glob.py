"""Shared file/dir/glob-pattern resolution for scripts that operate on a batch of files."""

from __future__ import annotations

import fnmatch
import subprocess
from pathlib import Path

from . import proc


def resolve_files(repo_root: Path, path: str, file_filter: str, recurse: bool) -> list[Path]:
    """Resolves `path` to a list of files: a directory (its git-tracked files matching
    file_filter), a single file, or a glob pattern (e.g. "XL/*.xml"), each relative to repo_root
    unless absolute.

    A directory is scanned via `git ls-files` rather than the filesystem, so `.git/`, ignored
    build output, and submodule content are never walked into."""
    target = (repo_root / path).resolve() if not Path(path).is_absolute() else Path(path)

    if target.is_dir():
        return _tracked_files(repo_root, target, file_filter, recurse)
    if target.is_file():
        return [target]
    return sorted(repo_root.glob(path))


def _tracked_files(repo_root: Path, target: Path, file_filter: str, recurse: bool) -> list[Path]:
    pathspec = target.relative_to(repo_root)
    result = proc.git(repo_root, "ls-files", "-z", "--", str(pathspec))

    files = []
    for relpath in (p for p in result.stdout.split("\0") if p):
        file_path = repo_root / relpath
        # git ls-files also lists submodule gitlinks as a path - not a regular file to read/write
        if not file_path.is_file():
            continue
        if not fnmatch.fnmatch(file_path.name, file_filter):
            continue
        if not recurse and file_path.parent != target:
            continue
        files.append(file_path)
    return sorted(files)


def filter_text_files(repo_root: Path, files: list[Path]) -> list[Path]:
    """Keeps only files this repo's `.gitattributes` considers text (its `text` attribute is
    `set`, or `auto`/unspecified and the content itself looks like text) - drops anything
    declared binary (e.g. `-text` on LFS-tracked assets)."""
    if not files:
        return []
    attrs = _git_text_attrs(repo_root, files)
    return [f for f in files if _is_text(f, attrs.get(f, "unspecified"))]


def _git_text_attrs(repo_root: Path, files: list[Path]) -> dict[Path, str]:
    stdin = "".join(f"{f.relative_to(repo_root)}\0" for f in files)
    result = subprocess.run(
        ["git", "check-attr", "--stdin", "-z", "text"],
        cwd=repo_root,
        input=stdin,
        capture_output=True,
        text=True,
    )
    fields = result.stdout.split("\0")[:-1]  # trailing entry is the empty tail after the last NUL
    return {repo_root / fields[i]: fields[i + 2] for i in range(0, len(fields), 3)}


def _is_text(path: Path, attr_value: str, sample_size: int = 8000) -> bool:
    if attr_value == "unset":
        return False
    if attr_value == "set":
        return True
    # "auto" (text=auto) or "unspecified" (no matching rule): fall back to the same NUL-byte
    # heuristic git's own text=auto detection uses.
    try:
        with path.open("rb") as f:
            sample = f.read(sample_size)
    except OSError:
        return False
    return b"\0" not in sample
