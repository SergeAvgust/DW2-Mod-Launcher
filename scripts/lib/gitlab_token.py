"""Shared GitLab PAT resolution for scripts/*.py.
usage: from common.gitlab_token import resolve_gitlab_token
       token = resolve_gitlab_token(provided_token, gitlab_origin_url)
"""

import os
import subprocess
from urllib.parse import urlsplit

from .gitlab import parse_remote


def _credential_host_url(origin_url: str) -> str:
    """origin_url as the https://<host> the credential helper should be asked about. Accepts a
    bare host URL ("https://gitlab.com") or a full git remote, SSH ones included
    ("git@gitlab.com:group/project.git") - always asks for https://<host> even for an SSH remote:
    the REST API is HTTPS-only, and GCM can serve an OAuth token for the host regardless of how
    git itself talks to it."""
    host_url, _ = parse_remote(origin_url)
    return host_url or origin_url


def _get_token_from_credential_helper(origin_url: str) -> str:
    """Queries the configured git credential helper (GCM, libsecret, etc) for a
    stored PAT matching origin_url's host (see _credential_host_url) - this is how
    `git push`/`go get` already authenticate, so it stays in sync with whatever
    secure storage the helper backs onto without the token ever touching disk
    in plaintext."""
    parsed = urlsplit(_credential_host_url(origin_url))
    if not parsed.scheme or not parsed.netloc:
        return ""
    credential_input = f"protocol={parsed.scheme}\nhost={parsed.netloc}\n\n"

    try:
        result = subprocess.run(
            ["git", "credential", "fill"],
            input=credential_input,
            capture_output=True,
            text=True,
            timeout=15,
        )
    except (OSError, subprocess.TimeoutExpired):
        return ""

    if result.returncode != 0:
        return ""

    for line in result.stdout.splitlines():
        if line.startswith("password="):
            return line[len("password="):].strip()

    return ""


def resolve_gitlab_token(provided: str, origin_url: str) -> str:
    """Resolves a gitlab PAT, trying in order: an explicit arg, $GITLAB_TOKEN,
    the configured git credential helper (matched against origin_url - a host URL
    or a full git remote), then legacy `git config gitlab.token`. Returns "" if
    none of the sources have one."""
    if provided and provided.strip():
        return provided.strip()

    env_token = os.environ.get("GITLAB_TOKEN", "")
    if env_token.strip():
        return env_token.strip()

    token = _get_token_from_credential_helper(origin_url)
    if token.strip():
        return token.strip()

    try:
        result = subprocess.run(
            ["git", "config", "--get", "gitlab.token"],
            capture_output=True,
            text=True,
            timeout=10,
        )
        if result.returncode == 0 and result.stdout.strip():
            return result.stdout.strip()
    except (OSError, subprocess.TimeoutExpired):
        pass

    return ""
