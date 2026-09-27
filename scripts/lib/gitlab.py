"""Shared minimal GitLab Merge Request API calls for scripts/*.py.

Token resolution lives in common.gitlab_token (kept separate since its signature and existing
callers predate this module) - this module only covers parsing the GitLab remote and the REST
calls needed to create/update a Merge Request.

API calls go through the `glab` CLI when it's installed: its OAuth login carries the `api` scope
and refreshes itself, whereas a Git Credential Manager GitLab OAuth token only has repository
scopes and gets 403 on MR endpoints (found in DW2-XL, 2026-09-24). Without glab (or if glab
fails, e.g. not logged in), calls fall back to a raw token from common.gitlab_token.
"""

from __future__ import annotations

import json
import re
import shutil
import subprocess
import urllib.error
import urllib.parse
import urllib.request
from urllib.parse import urlsplit


def parse_remote(remote_url: str) -> tuple[str, str]:
    """Parses a GitLab remote URL into (host_url, project_path). Supports
    git@host:group/project.git, https://host/group/project.git (with optional userinfo),
    and ssh://host/group/project.git. Returns ("", "") if unparseable."""
    m = re.match(r"^git@([^:]+):(.+?)(?:\.git)?$", remote_url)
    if not m:
        m = re.match(r"^(?:https?|ssh)://(?:[^@/]+@)?([^/]+)/(.+?)(?:\.git)?$", remote_url)
    if not m:
        return "", ""
    return f"https://{m.group(1)}", m.group(2)


def glab_available() -> bool:
    return shutil.which("glab") is not None


def has_credentials(token: str) -> bool:
    """True if api_request has anything to authenticate with - a token or the glab CLI."""
    return bool(token) or glab_available()


def _glab_api(host: str, endpoint: str, method: str, data: dict | None):
    """Runs `glab api` against {host}/api/v4/{endpoint}. Raises RuntimeError on failure."""
    args = ["glab", "api", "--hostname", urlsplit(host).netloc, "-X", method, endpoint]
    body = None
    if data is not None:
        args += ["-H", "Content-Type: application/json", "--input", "-"]
        body = json.dumps(data)
    try:
        result = subprocess.run(args, input=body, capture_output=True, text=True, encoding="utf-8", timeout=60)
    except (OSError, subprocess.TimeoutExpired) as e:
        raise RuntimeError(f"glab api failed: {e}") from e
    if result.returncode != 0:
        raise RuntimeError(f"glab api failed: {(result.stderr or result.stdout).strip()}")
    return json.loads(result.stdout) if result.stdout.strip() else None


def _token_api_request(host: str, endpoint: str, token: str, method: str, data: dict | None):
    """Calls {host}/api/v4/{endpoint} directly with token. Raises urllib.error.HTTPError/URLError
    on failure.

    The token may be a PAT (PRIVATE-TOKEN header) or an OAuth access token handed out by GCM's
    GitLab OAuth flow (Authorization: Bearer) - GitLab rejects each in the other's header with
    401, so try PAT style first and fall back to Bearer."""
    url = f"{host}/api/v4/{endpoint}"
    body = json.dumps(data).encode("utf-8") if data is not None else None
    auth_styles = ({"PRIVATE-TOKEN": token}, {"Authorization": f"Bearer {token}"})
    for i, auth_headers in enumerate(auth_styles):
        headers = dict(auth_headers)
        if body is not None:
            headers["Content-Type"] = "application/json"
        request = urllib.request.Request(url, data=body, method=method, headers=headers)
        try:
            with urllib.request.urlopen(request, timeout=30) as response:
                return json.loads(response.read().decode("utf-8"))
        except urllib.error.HTTPError as e:
            if e.code != 401 or i == len(auth_styles) - 1:
                raise


def api_request(host: str, project_path: str, path: str, token: str, method: str = "GET", data: dict | None = None):
    """Calls the GitLab REST API at {host}/api/v4/projects/{project_path}/{path}, via `glab api`
    when available, else directly with token. If both are available and glab fails, the token is
    tried too; glab's error is re-raised only if there's no token to fall back to. Raises
    RuntimeError or urllib.error.HTTPError/URLError on failure."""
    endpoint = f"projects/{urllib.parse.quote(project_path, safe='')}/{path}"
    glab_error: RuntimeError | None = None
    if glab_available():
        try:
            return _glab_api(host, endpoint, method, data)
        except RuntimeError as e:
            glab_error = e
    if not token:
        raise glab_error or RuntimeError("no GitLab credentials: install glab and run `glab auth login`")
    try:
        return _token_api_request(host, endpoint, token, method, data)
    except urllib.error.HTTPError as e:
        if glab_error:
            raise RuntimeError(f"{glab_error}; token fallback: {e}") from e
        raise


def find_open_mr(host: str, project_path: str, token: str, source_branch: str, target_branch: str) -> dict | None:
    path = (
        f"merge_requests?state=opened&source_branch={urllib.parse.quote(source_branch, safe='')}"
        f"&target_branch={urllib.parse.quote(target_branch, safe='')}"
    )
    try:
        existing = api_request(host, project_path, path, token)
    except (urllib.error.URLError, RuntimeError):
        return None
    if isinstance(existing, list) and existing:
        return existing[0]
    return None


def create_mr(host: str, project_path: str, token: str, data: dict) -> dict:
    return api_request(host, project_path, "merge_requests", token, method="POST", data=data)


def update_mr(host: str, project_path: str, token: str, iid: str, data: dict) -> dict:
    return api_request(host, project_path, f"merge_requests/{iid}", token, method="PUT", data=data)
