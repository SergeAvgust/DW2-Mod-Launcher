"""Canonical Go-toolchain verbs shared by scripts/*.py.
usage: from common import go_checks; go_checks.run_go_fmt_check(repo_root)

Each verb (fmt check, vet, lint, tidy check, build, test) is defined exactly once here and
used identically by scripts/validate.py (CI's validate job) and open-pr.py (local pre-flight) -
a sibling repo (critical-mass) once had a bash reimplementation of one of these checks drift
out of sync with a real bugfix made only on the Python side; this module exists so that class
of bug can't happen here.
"""

import re
import subprocess
from pathlib import Path

from . import output

_GOLANGCI_LINT_PIN_PATTERN = re.compile(r'(?m)^\s*GOLANGCI_LINT_VERSION:\s*"([^"]+)"')


def read_golangci_lint_pin(repo_root: Path) -> str:
    """.gitlab-ci.yml's GOLANGCI_LINT_VERSION is the single canonical pin - update-golangci-lint.py
    is the only thing that ever changes it; every caller (CI, open-pr.py) reads it from there
    rather than hardcoding a second copy of the version that could drift out of sync."""
    ci_file = repo_root / ".gitlab-ci.yml"
    if not ci_file.is_file():
        output.fail(f".gitlab-ci.yml not found at {ci_file}")
    m = _GOLANGCI_LINT_PIN_PATTERN.search(ci_file.read_text(encoding="utf-8"))
    if not m:
        output.fail("could not find GOLANGCI_LINT_VERSION in .gitlab-ci.yml")
    return m.group(1)


def run_go_fmt_check(repo_root: Path) -> None:
    print("  » go fmt")
    result = subprocess.run(["go", "fmt", "./..."], cwd=repo_root, capture_output=True, text=True)
    out = (result.stdout + result.stderr).strip()
    if out:
        print(out)
        output.fail("go fmt reformatted files above — changes have been applied, please review and re-run")
    output.ok("go fmt")


def run_go_vet(repo_root: Path) -> None:
    print("  » go vet")
    result = subprocess.run(["go", "vet", "./..."], cwd=repo_root)
    if result.returncode != 0:
        output.fail(f"go vet failed (exit {result.returncode})")
    output.ok("go vet")


def run_golangci_lint(repo_root: Path, version: str) -> None:
    print(f"  » golangci-lint ({version})")
    install = subprocess.run(
        ["go", "install", f"github.com/golangci/golangci-lint/v2/cmd/golangci-lint@{version}"],
        cwd=repo_root,
    )
    if install.returncode != 0:
        output.fail(f"golangci-lint install failed (exit {install.returncode})")

    gopath_result = subprocess.run(["go", "env", "GOPATH"], cwd=repo_root, capture_output=True, text=True)
    lint_bin = str(Path(gopath_result.stdout.strip()) / "bin" / "golangci-lint")
    result = subprocess.run([lint_bin, "run", "./..."], cwd=repo_root)
    if result.returncode != 0:
        output.fail(f"golangci-lint failed (exit {result.returncode})")
    output.ok("golangci-lint")


def run_go_mod_tidy_check(repo_root: Path, baseline_go_mod: str, baseline_go_sum: str) -> None:
    """Runs `go mod tidy` then compares go.mod/go.sum against the given baseline. The baseline
    is a caller concern, not something this function should guess: CI compares against the
    checkout's starting content, local pre-flight may need a different baseline if it ever
    intentionally changes go.mod/go.sum first (mirrors critical-mass's fox-refresh baseline)."""
    print("  » go mod tidy")
    subprocess.run(["go", "mod", "tidy"], cwd=repo_root, capture_output=True)

    go_mod_path = repo_root / "go.mod"
    go_sum_path = repo_root / "go.sum"
    current_go_mod = go_mod_path.read_text(encoding="utf-8")
    current_go_sum = go_sum_path.read_text(encoding="utf-8") if go_sum_path.is_file() else ""

    if current_go_mod != baseline_go_mod or current_go_sum != baseline_go_sum:
        output.fail("go mod tidy changed go.mod/go.sum beyond the expected baseline — review the diff and re-run")
    output.ok("go.mod/go.sum tidy")


def run_go_build(repo_root: Path) -> None:
    print("  » go build")
    result = subprocess.run(["go", "build", "./..."], cwd=repo_root)
    if result.returncode != 0:
        output.fail(f"go build failed (exit {result.returncode})")
    output.ok("go build")


def run_go_test(repo_root: Path, exclude_substr: str = "/cmd/") -> None:
    print(f"  » go test (excluding {exclude_substr})")
    list_result = subprocess.run(["go", "list", "./..."], cwd=repo_root, capture_output=True, text=True)
    packages = [p for p in list_result.stdout.splitlines() if exclude_substr not in p]
    if not packages:
        output.ok("no test packages to run")
        return
    result = subprocess.run(["go", "test", "-v", "-race", *packages], cwd=repo_root)
    if result.returncode != 0:
        output.fail("tests failed")
    output.ok("all tests passed")
