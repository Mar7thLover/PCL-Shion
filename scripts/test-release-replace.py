#!/usr/bin/env python3
"""Offline release-replacement regressions: no credentials or network are used."""

import importlib.util
import io
import json
from pathlib import Path
import unittest
from urllib.error import HTTPError, URLError

spec = importlib.util.spec_from_file_location("release_replace", Path(__file__).with_name("replace-release.py"))
release = importlib.util.module_from_spec(spec)
spec.loader.exec_module(release)

REPOSITORY = "example/launcher"
TAG = "v1.3.0"
BASE = "repos/" + REPOSITORY
LIST = BASE + "/releases?per_page=100&page=1"
REF = BASE + "/git/ref/tags/" + TAG
DELETE_REF = BASE + "/git/refs/tags/" + TAG
ENTRY = {"id": 42, "tag_name": TAG, "draft": True}
REFERENCE = {"ref": "refs/tags/" + TAG}


class Response(io.BytesIO):
    def __init__(self, status, body):
        super().__init__(json.dumps(body).encode())
        self.status = status


class FakeOpener:
    def __init__(self, responses):
        self.responses = list(responses)
        self.calls = []

    def open(self, request, timeout):
        self.calls.append((request.method, request.full_url.removeprefix("https://api.github.com/")))
        if not self.responses:
            raise AssertionError("Unexpected subsequent API call after a failure.")
        response = self.responses.pop(0)
        if isinstance(response, Exception):
            raise response
        status, body = response
        if status >= 300:
            raise HTTPError(request.full_url, status, "fixture", {}, io.BytesIO(b"untrusted response"))
        return Response(status, body)


class ReplacementTest(unittest.TestCase):
    def run_scenario(self, responses, fail=False):
        opener = FakeOpener(responses)
        api = release.GitHubApi("test-placeholder", opener)
        if fail:
            with self.assertRaises(release.ReleaseError):
                release.replace_release(api, REPOSITORY, TAG)
        else:
            release.replace_release(api, REPOSITORY, TAG)
        self.assertEqual([], opener.responses)
        return opener.calls

    def test_removes_selected_draft_release_and_tag_then_verifies_absence(self):
        calls = self.run_scenario([(200, [ENTRY]), (200, REFERENCE), (204, None), (204, None), (404, None)])
        self.assertEqual([("GET", LIST), ("GET", REF), ("DELETE", BASE + "/releases/42"),
                          ("DELETE", DELETE_REF), ("GET", REF)], calls)

    def test_explicit_404_for_missing_tag_is_safe_and_other_releases_are_untouched(self):
        calls = self.run_scenario([(200, [{"id": 2, "tag_name": "v1.2.0"}]), (404, None), (404, None)])
        self.assertTrue(all(method == "GET" for method, _ in calls))

    def test_release_deleted_by_another_actor_may_return_404(self):
        self.run_scenario([(200, [ENTRY]), (200, REFERENCE), (404, None), (204, None), (404, None)])

    def test_delete_release_permission_or_server_failure_never_deletes_tag(self):
        for status in [401, 403, 409, 422, 429, 500, 503]:
            with self.subTest(status=status):
                calls = self.run_scenario([(200, [ENTRY]), (200, REFERENCE), (status, None)], fail=True)
                self.assertNotIn(("DELETE", DELETE_REF), calls)

    def test_tag_delete_failure_aborts_without_followup(self):
        for failure in [(403, None), (422, None), (500, None), URLError("offline"), TimeoutError("timeout")]:
            with self.subTest(failure=failure):
                calls = self.run_scenario([(200, []), (200, REFERENCE), failure], fail=True)
                self.assertEqual(("DELETE", DELETE_REF), calls[-1])

    def test_read_failures_do_not_start_any_deletions(self):
        for failure in [(403, None), (500, None), URLError("offline"), (200, {"invalid": "list"})]:
            with self.subTest(failure=failure):
                calls = self.run_scenario([failure], fail=True)
                self.assertEqual([("GET", LIST)], calls)

    def test_collection_404_is_not_treated_as_missing_release(self):
        self.run_scenario([(404, None)], fail=True)

    def test_invalid_release_id_or_reference_does_not_start_deletion(self):
        for entries, reference in [([{"tag_name": TAG, "id": "42"}], None),
                                   ([ENTRY], None), ([ENTRY], {"ref": "refs/heads/dev"})]:
            with self.subTest(entries=entries, reference=reference):
                responses = [(200, entries)]
                if entries[0].get("id") == 42:
                    responses.append((200, reference))
                calls = self.run_scenario(responses, fail=True)
                self.assertTrue(all(method == "GET" for method, _ in calls))

    def test_old_tag_remaining_after_delete_prevents_continuation(self):
        self.run_scenario([(200, []), (200, REFERENCE), (204, None), (200, REFERENCE)], fail=True)

    def test_unexpected_success_status_is_not_accepted_as_deletion(self):
        self.run_scenario([(200, []), (200, REFERENCE), (200, {})], fail=True)

    def test_redirect_is_not_followed(self):
        self.assertIsNone(release.NoRedirect().redirect_request(None, None, 302, "redirect", {}, "https://example.invalid"))
        self.run_scenario([(302, None)], fail=True)


if __name__ == "__main__":
    unittest.main()
