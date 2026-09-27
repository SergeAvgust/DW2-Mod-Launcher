#!/usr/bin/env python3
"""Normalizes file line endings to LF and rewrites as UTF-8 without a BOM.

Accepts a file, directory, or glob pattern and rewrites matching files with consistent LF line
endings. Useful for XML and other text assets.

Usage:
  python scripts/fix-lf.py
  python scripts/fix-lf.py --path XL --filter "*.xml"
  python scripts/fix-lf.py "XL/*.xml"
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent / "common-python"))
from lib import output  # noqa: E402
from lib import proc  # noqa: E402
from lib.file_glob import filter_text_files, resolve_files  # noqa: E402


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("path", nargs="?", default=".", help="file, directory, or glob pattern (default: whole repo)")
    parser.add_argument("--filter", default="*", help='file pattern when path is a directory (default: "*", all files)')
    parser.add_argument("--no-recurse", dest="recurse", action="store_false", help="don't recurse into subdirectories")
    parser.add_argument(
        "--dry-run", action="store_true", help="list the files that would change, without writing anything"
    )
    args = parser.parse_args()

    repo_root = proc.repo_root()
    files = filter_text_files(repo_root, resolve_files(repo_root, args.path, args.filter, args.recurse))

    if not files:
        output.warn(f"No text files found matching: {args.path}")
        return 1

    output.step(f"normalizing {len(files)} file(s) to LF..." + (" (dry run)" if args.dry_run else ""))
    updated = 0
    for file_path in files:
        original = file_path.read_bytes()
        try:
            # utf-8-sig drops a leading BOM (plain utf-8 would keep it as U+FEFF and write it back)
            normalized = original.decode("utf-8-sig").replace("\r\n", "\n").replace("\r", "\n").encode("utf-8")
        except UnicodeDecodeError:
            output.warn(f"skipped (not valid UTF-8): {file_path}")
            continue
        if normalized == original:
            continue
        if args.dry_run:
            print(f"  [dry-run] would fix: {file_path}")
        else:
            file_path.write_bytes(normalized)
            output.ok(f"fixed: {file_path}")
        updated += 1

    output.step("summary")
    print(f"  Files processed: {len(files)}")
    print(f"  Files {'that would be updated' if args.dry_run else 'updated'}:   {updated}")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        output.abort("interrupted")
        sys.exit(130)
