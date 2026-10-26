#!/usr/bin/env python3
"""Remove one explicitly selected release/tag; only an HTTP 404 is ignorable."""

import argparse
import json
import os
import re
from urllib.error import HTTPError, URLError
from urllib.parse import quote
from urllib.request import HTTPRedirectHandler, Request, build_opener


class ReleaseError(RuntimeError):
    pass


MISSING = object()


class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self, request, fp, code, message, headers, new_url):
        return None  # A moved endpoint must not redirect a destructive request or its token.


class GitHubApi:
    def __init__(self, token, opener=None):
        if not token:
            raise ReleaseError("GH_TOKEN is required.")
        self.token = token
        self.opener = opener or build_opener(NoRedirect())

    def call(self, method, path, *, missing_ok=False):
        request = Request("https://api.github.com/" + path, method=method, headers={
            "Authorization": "Bearer " + self.token,
            "Accept": "application/vnd.github+json",
            "X-GitHub-Api-Version": "2022-11-28",
            "User-Agent": "PCL-Shion-Release",
        })
        try:
            with self.opener.open(request, timeout=30) as response:
                expected = 204 if method == "DELETE" else 200
                if response.status != expected:
                    raise ReleaseError(f"{method} {path}: unexpected HTTP {response.status}.")
                return json.loads(response.read()) if method == "GET" else None
        except HTTPError as error:
            status = error.code
            error.close()
            if status == 404 and missing_ok:
                return MISSING
            raise ReleaseError(f"{method} {path}: HTTP {status}; replacement aborted.") from None
        except (URLError, OSError, ValueError) as error:
            # Never print response bodies, request headers, or the authentication token.
            raise ReleaseError(f"{method} {path}: {type(error).__name__}; replacement aborted.") from None


def replace_release(api, repository, tag):
    if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repository):
        raise ReleaseError("Repository must be owner/name.")
    if not re.fullmatch(r"v[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?", tag):
        raise ReleaseError("Tag must be a release version such as v1.3.0 or v1.3.0-beta.1.")
    base = f"repos/{repository}"
    release_ids = []
    page = 1
    while True:
        # This endpoint includes drafts for a token with push access. The
        # releases/tags endpoint only promises published releases.
        releases = api.call("GET", f"{base}/releases?per_page=100&page={page}")
        if not isinstance(releases, list) or any(not isinstance(item, dict) for item in releases):
            raise ReleaseError("Invalid release-list response; replacement aborted.")
        for release in releases:
            if release.get("tag_name") == tag:
                release_id = release.get("id")
                if type(release_id) is not int or release_id <= 0:
                    raise ReleaseError("Invalid release ID; replacement aborted.")
                release_ids.append(release_id)
        if len(releases) < 100:
            break
        page += 1

    encoded_tag = quote(tag, safe="")
    ref_path = f"{base}/git/ref/tags/{encoded_tag}"
    reference = api.call("GET", ref_path, missing_ok=True)
    if reference is not MISSING and (not isinstance(reference, dict) or reference.get("ref") != f"refs/tags/{tag}"):
        raise ReleaseError("Unexpected tag reference; replacement aborted.")

    for release_id in release_ids:
        api.call("DELETE", f"{base}/releases/{release_id}", missing_ok=True)
    if reference is not MISSING:
        # Preflight GET distinguishes a missing ref (404) from DELETE's generic
        # 422 response, which must never be treated as permission to continue.
        api.call("DELETE", f"{base}/git/refs/tags/{encoded_tag}", missing_ok=True)
    if api.call("GET", ref_path, missing_ok=True) is not MISSING:
        raise ReleaseError("The old tag still exists; refusing to publish new binaries against it.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository", required=True)
    parser.add_argument("--tag", required=True)
    args = parser.parse_args()
    try:
        replace_release(GitHubApi(os.environ.get("GH_TOKEN")), args.repository, args.tag)
    except ReleaseError as error:
        parser.exit(1, f"Release replacement failed: {error}\n")
    print(f"Verified that {args.repository} tag {args.tag} is absent; ready to create the validated release.")


if __name__ == "__main__":
    main()
