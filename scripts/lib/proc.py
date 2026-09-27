"""Shared subprocess helpers for scripts/*.py.
usage: from common import proc; proc.run(repo_root, "go build", ["go", "build", "./..."])

Canonical, shared verbatim across fox, critical-mass, critical-mass-client, and DW2-XL - run() and git()
were each copied by hand into every script that needed them (open-pr.py, new-branch.py,
release.py) until they drifted into three near-identical local defs per repo; this module is the
one copy all of them import instead.
"""

import signal
import subprocess
from pathlib import Path

from . import output


def run(
    cwd: Path, desc: str, args: list[str], capture_output: bool = False, check: bool = True
) -> subprocess.CompletedProcess:
    """Runs args, printing a "  » desc" progress line first. Fails loudly (via output.fail) on a
    nonzero exit unless check=False - the default for every imperative action a script takes.
    capture_output also sets text=True, matching subprocess.run's own convention that captured
    output is almost always wanted as str, not bytes."""
    print(f"  » {desc}")
    result = subprocess.run(args, cwd=cwd, capture_output=capture_output, text=capture_output)
    if check and result.returncode != 0:
        output.fail(f"{desc} failed (exit {result.returncode})")
    return result


def git(cwd: Path, *args: str) -> subprocess.CompletedProcess:
    """Runs git quietly (captured, never fails) - for querying repo state the caller inspects
    itself (branch name, status, etc.), as opposed to run()'s fail-loudly imperative actions."""
    return subprocess.run(["git", *args], cwd=cwd, capture_output=True, text=True)


def repo_root(cwd: Path | None = None) -> Path:
    """The enclosing git repository's top-level directory (of cwd, default the process's own).
    Fails loudly outside a git repository."""
    result = subprocess.run(["git", "rev-parse", "--show-toplevel"], cwd=cwd, capture_output=True, text=True)
    if result.returncode != 0:
        output.fail("not inside a git repository")
    return Path(result.stdout.strip())


# what a Windows process's exit code reads as when ^C killed it outright (STATUS_CONTROL_C_EXIT)
_WINDOWS_CONTROL_C_EXIT = 0xC000013A


def was_interrupted(returncode: int) -> bool:
    """Whether a child's returncode means ^C stopped it: it caught the interrupt itself and exited
    output.INTERRUPTED_EXIT_CODE, or it was killed by it (Windows STATUS_CONTROL_C_EXIT, or -SIGINT
    on POSIX)."""
    return returncode in (output.INTERRUPTED_EXIT_CODE, _WINDOWS_CONTROL_C_EXIT, -signal.SIGINT)


def run_foreground(cwd: Path, args: list[str]) -> int:
    """Runs args attached to this process's terminal and returns its exit code, handing ^C to the
    child for as long as it runs.

    A child sharing our console gets every ^C too, and it's the one that knows what ^C means at
    that moment (e.g. a y/N prompt that aborts cleanly and says so). Left to default handling, this
    process would also die mid-wait - tearing down with a KeyboardInterrupt, and possibly exiting
    before the child has printed its own message. So SIGINT is ignored here until the child exits;
    check was_interrupted() on the result to tell a ^C stop from a real failure."""
    previous_handler = signal.signal(signal.SIGINT, signal.SIG_IGN)
    try:
        return subprocess.run(args, cwd=cwd).returncode
    finally:
        signal.signal(signal.SIGINT, previous_handler)
