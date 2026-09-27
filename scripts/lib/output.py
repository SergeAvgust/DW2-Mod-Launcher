"""Shared console output for scripts/*.py - one function per semantic tag.
usage: from common import output; output.step("working tree"); output.check("querying release");
output.ok("clean")

Canonical, shared verbatim across fox, critical-mass, critical-mass-client, and DW2-XL - a
superset of fox's original step/ok/warn/fail/abort plus critical-mass's tag->color vocabulary
(documented in that repo's AGENTS.md under "Script Log Tags"), DW2-XL's --color flag and inline
styling helpers, and a ^C safety net (see _abort_on_keyboard_interrupt). Prefer the most specific function for what's
actually happening (check() before a lookup, build() before compiling, replace()/clean() before a
destructive step, ...) over a bare print() - that's what keeps a scan of a script's output legible
at a glance, and it's why this is a fixed set of named functions rather than a string tag: a typo
in a tag string silently falls back to an untagged line, a typo in a function name is a NameError.
"""

import io
import os
import sys
from typing import NoReturn

_CYAN = "\033[36m"
_BLUE = "\033[34m"
_GREEN = "\033[32m"
_GREEN_BOLD = "\033[1;32m"
_RED_BOLD = "\033[1;31m"
_YELLOW = "\033[33m"
_YELLOW_BOLD = "\033[1;33m"
_MAGENTA_BOLD = "\033[1;35m"
_RESET = "\033[0m"


def _enable_windows_ansi() -> None:
    if os.name != "nt":
        return
    try:
        import ctypes

        kernel32 = ctypes.windll.kernel32  # type: ignore[attr-defined]
        handle = kernel32.GetStdHandle(-11)  # STD_OUTPUT_HANDLE
        mode = ctypes.c_uint32()
        if kernel32.GetConsoleMode(handle, ctypes.byref(mode)):
            kernel32.SetConsoleMode(handle, mode.value | 0x0004)  # ENABLE_VIRTUAL_TERMINAL_PROCESSING
    except Exception:
        pass


_enable_windows_ansi()

# scripts commonly print a status line, then immediately shell out to a
# sibling process - without line buffering, stdout's default block buffering
# (in effect whenever stdout isn't a tty) can let the child's output land on
# screen before the parent's already-issued print() flushes, interleaving
# lines out of order.
#
# also forced to UTF-8: a Windows console or pipe often defaults to a legacy codepage (cp1252) that
# can't encode the ✅/⚠/── marks this module prints, which crashes the print instead of just looking
# wrong ("replace" so an unencodable character degrades to "?" rather than raising)
for _stream in (sys.stdout, sys.stderr):
    if isinstance(_stream, io.TextIOWrapper):
        try:
            if (_stream.encoding or "").lower() != "utf-8":
                _stream.reconfigure(encoding="utf-8", errors="replace")
            _stream.reconfigure(line_buffering=True)
        except ValueError:
            pass

def _detect_color_enabled() -> bool:
    """True unless explicitly turned off (NO_COLOR) or the terminal genuinely can't render it (not
    a tty, dumb TERM). FORCE_COLOR overrides the tty/TERM checks, but never NO_COLOR - for a child
    process whose stdout is a pipe back to a script that captures and reprints it
    (subprocess.run(..., capture_output=True)) even though the real terminal at the other end of
    that reprinting supports color; see subprocess_env()."""
    if os.environ.get("NO_COLOR"):
        return False
    if os.environ.get("FORCE_COLOR"):
        return True
    if not sys.stdout.isatty():
        return False
    if os.name != "nt" and os.environ.get("TERM", "dumb") == "dumb":
        return False
    return True


_color_enabled = _detect_color_enabled()


def add_color_argument(parser) -> None:
    """Adds the standard --color {auto,always,never} flag to an argparse parser; pass the parsed
    value to set_color()."""
    parser.add_argument(
        "--color",
        choices=["auto", "always", "never"],
        default="auto",
        help="color output mode. Default: auto (color on a real terminal, off otherwise or if NO_COLOR is set).",
    )


def set_color(mode: str) -> None:
    """Overrides the import-time color auto-detection: "always"/"never" force it, "auto" re-runs
    _detect_color_enabled(). For a script's --color flag (see add_color_argument)."""
    global _color_enabled
    if mode == "always":
        _color_enabled = True
    elif mode == "never":
        _color_enabled = False
    else:
        _color_enabled = _detect_color_enabled()


def step(msg: str) -> None:
    """A new phase of work - the only function that prints a leading blank line."""
    if _color_enabled:
        print(f"\n{_CYAN}── {msg}{_RESET}")
    else:
        print(f"\n── {msg}")


def _tagged(tag: str, color: str, msg: str) -> None:
    """Indented, colored progress line for a tag that isn't a pass/fail/warn verdict - see
    ok/warn/fail/abort/done for those."""
    if _color_enabled:
        print(f"  {color}{msg}{_RESET}" if color else f"  {msg}")
    else:
        print(f"  [{tag}] {msg}")


def check(msg: str) -> None:
    """Querying/inspecting something before acting on it."""
    _tagged("check", _CYAN, msg)


def verify(msg: str) -> None:
    """Confirming the result of an action already taken."""
    _tagged("verify", _CYAN, msg)


def build(msg: str) -> None:
    """Compiling/constructing something."""
    _tagged("build", _BLUE, msg)


def download(msg: str) -> None:
    """Fetching something over the network."""
    _tagged("download", _BLUE, msg)


def replace(msg: str) -> None:
    """A mutating/destructive step done as a matter of course (e.g. overwrite-in-place)."""
    _tagged("replace", _YELLOW, msg)


def clean(msg: str) -> None:
    """A mutating/destructive step done as a matter of course (e.g. deleting build output)."""
    _tagged("clean", _YELLOW, msg)


def notice(msg: str) -> None:
    """A heads-up worth reading, but not an error or a warning."""
    _tagged("notice", _MAGENTA_BOLD, msg)


def info(msg: str) -> None:
    """General informational output with no severity of its own."""
    _tagged("info", "", msg)


def done(msg: str) -> None:
    """Terminal success - the whole operation finished, not just one step of it. Bolder than
    ok() on purpose: this is the line a human skimming the tail of the output should land on."""
    if _color_enabled:
        print(f"  {_GREEN_BOLD}✅ {msg}{_RESET}")
    else:
        print(f"  [done] {msg}")


def ok(msg: str) -> None:
    """Lightweight, per-step success."""
    if _color_enabled:
        print(f"  {_GREEN}✅ {msg}{_RESET}")
    else:
        print(f"  [ok] {msg}")


def warn(msg: str) -> None:
    if _color_enabled:
        print(f"  {_YELLOW_BOLD}⚠ {msg}{_RESET}")
    else:
        print(f"  [warn] {msg}")


def fail(msg: str) -> NoReturn:
    if _color_enabled:
        print(f"\n  {_RED_BOLD}❌ {msg}{_RESET}", file=sys.stderr)
    else:
        print(f"\n  [error] {msg}", file=sys.stderr)
    sys.exit(1)


def abort(msg: str) -> None:
    """Report a clean, user-initiated abort (e.g. Ctrl-C) - not an error, no traceback, no [error] framing."""
    if _color_enabled:
        print(f"\n  {_YELLOW}⚠ {msg}{_RESET}", file=sys.stderr)
    else:
        print(f"\n  [abort] {msg}", file=sys.stderr)


def subprocess_env(base: dict[str, str] | None = None) -> dict[str, str]:
    """Environment for a child process whose output will be captured and reprinted rather than
    inherited (subprocess.run(..., capture_output=True)) - a captured child always sees a pipe on
    stdout, so its own color detection goes dark even when the real terminal at the other end of
    the parent's reprinting supports color just fine. Pass as subprocess.run(...,
    env=output.subprocess_env())."""
    env = dict(base if base is not None else os.environ)
    if _color_enabled:
        env["FORCE_COLOR"] = "1"
    return env


def redraw_last_line(text: str) -> None:
    """Overwrite the line just printed/entered with `text` - used to replace an input
    prompt's hint (e.g. "[Y/n]") with the resolved answer once known. Falls back to a
    plain trailing line when an in-place redraw isn't safe (not a tty, NO_COLOR, dumb
    TERM) rather than emitting raw escape codes into piped output or a log file."""
    if _color_enabled:
        sys.stdout.write(f"\033[F\033[K{text}\n")
        sys.stdout.flush()
    else:
        print(text)


def confirm(prompt: str, default: bool) -> bool:
    """y/n prompt (case-insensitive); Enter/blank accepts the default. Ctrl-C is always the
    third, unstated option (abort). Anything else - including stray digits like "1"/"2" out of
    habit from numbered menus - is rejected and re-prompted rather than silently guessed at; that
    exact leniency once misread "2" as "yes" and triggered a real commit (fox, 2026-09-10)."""
    suffix = "[Y/n]" if default else "[y/N]"
    while True:
        answer = input(f"{prompt} {suffix} ").strip().lower()
        if not answer:
            resolved = default
        elif answer in ("y", "yes"):
            resolved = True
        elif answer in ("n", "no"):
            resolved = False
        else:
            print(f"  please answer y or n (or press enter for the default: {'y' if default else 'n'})")
            continue
        redraw_last_line(f"{prompt} {'yes' if resolved else 'no'}")
        return resolved


# inline styling, for coloring a fragment *within* a line a script composes itself (a file:line
# reference, a highlighted name, a MISSING marker in a report row) - as opposed to the whole-line
# semantic tags above. Returns text unchanged when color is off, so callers never branch on it.
_STYLES = {
    "bold": "1",
    "red": "31",
    "green": "32",
    "yellow": "33",
    "magenta": "35",
    "cyan": "36",
}


def style(text: str, *names: str) -> str:
    """Wraps text in the named SGR styles (style(text, "bold", "yellow")) if color is on."""
    if not _color_enabled or not names:
        return text
    codes = ";".join(_STYLES[name] for name in names)
    return f"\033[{codes}m{text}{_RESET}"


def bold(text: str) -> str:
    return style(text, "bold")


def red(text: str) -> str:
    return style(text, "red")


def green(text: str) -> str:
    return style(text, "green")


def yellow(text: str) -> str:
    return style(text, "yellow")


def magenta(text: str) -> str:
    return style(text, "magenta")


def red_bold(text: str) -> str:
    return style(text, "bold", "red")


def yellow_bold(text: str) -> str:
    return style(text, "bold", "yellow")


def cyan_bold(text: str) -> str:
    return style(text, "bold", "cyan")


# conventional exit status for "stopped by ^C" (128 + SIGINT)
INTERRUPTED_EXIT_CODE = 130


def _abort_on_keyboard_interrupt(exc_type, exc, tb) -> None:
    """sys.excepthook: a ^C that no script caught reports a clean abort (see abort()) and exits
    INTERRUPTED_EXIT_CODE - never a traceback. Every other exception goes to the default hook.

    Every script's __main__ still catches KeyboardInterrupt itself (the explicit, documented
    pattern); this is the net under that, for a ^C that lands somewhere the script's own handler
    doesn't cover (e.g. in a module-level import, or in a script missing the handler)."""
    if issubclass(exc_type, KeyboardInterrupt):
        abort("interrupted")
        # an excepthook's return value doesn't set the exit status (and SystemExit raised in here
        # is just reported, not honored), so flush and exit explicitly
        sys.stdout.flush()
        sys.stderr.flush()
        os._exit(INTERRUPTED_EXIT_CODE)
    sys.__excepthook__(exc_type, exc, tb)


sys.excepthook = _abort_on_keyboard_interrupt
