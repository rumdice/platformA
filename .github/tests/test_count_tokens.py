"""
count_tokens.py 유닛 테스트.

임시 JSONL 파일을 생성하여 DB 연결 없이 토큰 계산 로직을 검증한다.
"""

import json
import os
import sys
import tempfile
import unittest
from datetime import datetime, timezone, timedelta
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "scripts"))

import count_tokens


class TestParseTimestamp(unittest.TestCase):
    def test_iso_with_z(self):
        result = count_tokens.parse_timestamp("2026-06-17T10:00:00Z")
        self.assertIsNotNone(result)
        self.assertEqual(result.year, 2026)
        self.assertEqual(result.hour, 10)

    def test_iso_with_offset(self):
        result = count_tokens.parse_timestamp("2026-06-17T10:00:00+00:00")
        self.assertIsNotNone(result)

    def test_invalid_returns_none(self):
        result = count_tokens.parse_timestamp("not-a-date")
        self.assertIsNone(result)

    def test_empty_string_returns_none(self):
        result = count_tokens.parse_timestamp("")
        self.assertIsNone(result)


class TestGetProjectDir(unittest.TestCase):
    def test_returns_none_when_no_project_dir(self):
        with patch.dict(os.environ, {}):
            with patch("pathlib.Path.exists", return_value=False):
                result = count_tokens.get_project_dir()
                self.assertIsNone(result)

    def test_returns_path_when_exists(self):
        with tempfile.TemporaryDirectory() as tmp:
            # simulate ~/.claude/projects/<hash>/ existing
            home = Path(tmp)
            cwd = os.getcwd()
            if cwd.startswith("/"):
                # unix-style
                project_hash = cwd.replace(":", "-").replace("/", "-").lstrip("-")
            else:
                project_hash = cwd.replace(":", "-").replace("\\", "-").replace("/", "-")
            project_dir = home / ".claude" / "projects" / project_hash
            project_dir.mkdir(parents=True)

            with patch("pathlib.Path.home", return_value=home):
                result = count_tokens.get_project_dir()
                self.assertIsNotNone(result)


class TestCountTokens(unittest.TestCase):
    def _write_jsonl(self, path: Path, entries: list) -> None:
        with open(path, "w", encoding="utf-8") as f:
            for entry in entries:
                f.write(json.dumps(entry) + "\n")

    def test_counts_tokens_after_cutoff(self):
        with tempfile.TemporaryDirectory() as tmp:
            project_dir = Path(tmp)
            cutoff = datetime(2026, 6, 17, 10, 0, 0, tzinfo=timezone.utc)
            after = datetime(2026, 6, 17, 11, 0, 0, tzinfo=timezone.utc)
            before = datetime(2026, 6, 17, 9, 0, 0, tzinfo=timezone.utc)

            jsonl_path = project_dir / "session.jsonl"
            self._write_jsonl(jsonl_path, [
                {
                    "type": "assistant",
                    "timestamp": after.isoformat(),
                    "message": {"usage": {
                        "input_tokens": 100,
                        "output_tokens": 50,
                        "cache_read_input_tokens": 20,
                        "cache_creation_input_tokens": 10,
                    }}
                },
                {
                    "type": "assistant",
                    "timestamp": before.isoformat(),  # before cutoff — excluded
                    "message": {"usage": {
                        "input_tokens": 999,
                        "output_tokens": 999,
                    }}
                },
            ])

            with patch.object(count_tokens, "get_project_dir", return_value=project_dir):
                _, consume, cache = count_tokens.count_tokens("2026-06-17T10:00:00Z")

            self.assertEqual(consume, 100 + 50 + 20 + 10)
            self.assertEqual(cache, 10)

    def test_skips_non_assistant_entries(self):
        with tempfile.TemporaryDirectory() as tmp:
            project_dir = Path(tmp)
            after = datetime(2026, 6, 17, 11, 0, 0, tzinfo=timezone.utc)

            jsonl_path = project_dir / "session.jsonl"
            self._write_jsonl(jsonl_path, [
                {
                    "type": "user",  # not assistant — should be skipped
                    "timestamp": after.isoformat(),
                    "message": {"usage": {"input_tokens": 999}}
                },
            ])

            with patch.object(count_tokens, "get_project_dir", return_value=project_dir):
                _, consume, cache = count_tokens.count_tokens("2026-06-17T10:00:00Z")

            self.assertEqual(consume, 0)

    def test_handles_missing_usage_fields(self):
        with tempfile.TemporaryDirectory() as tmp:
            project_dir = Path(tmp)
            after = datetime(2026, 6, 17, 11, 0, 0, tzinfo=timezone.utc)

            jsonl_path = project_dir / "session.jsonl"
            self._write_jsonl(jsonl_path, [
                {
                    "type": "assistant",
                    "timestamp": after.isoformat(),
                    "message": {"usage": {}}  # no token fields
                },
            ])

            with patch.object(count_tokens, "get_project_dir", return_value=project_dir):
                _, consume, cache = count_tokens.count_tokens("2026-06-17T10:00:00Z")

            self.assertEqual(consume, 0)
            self.assertEqual(cache, 0)

    def test_returns_zero_when_no_project_dir(self):
        with patch.object(count_tokens, "get_project_dir", return_value=None):
            duration, consume, cache = count_tokens.count_tokens("2020-01-01T00:00:00Z")
        self.assertGreater(duration, 0)
        self.assertEqual(consume, 0)
        self.assertEqual(cache, 0)

    def test_invalid_created_at_returns_zeros(self):
        duration, consume, cache = count_tokens.count_tokens("not-a-date")
        self.assertEqual(duration, 0)
        self.assertEqual(consume, 0)
        self.assertEqual(cache, 0)

    def test_duration_is_positive(self):
        past = (datetime.now(timezone.utc) - timedelta(minutes=5)).strftime("%Y-%m-%dT%H:%M:%SZ")
        with patch.object(count_tokens, "get_project_dir", return_value=None):
            duration, _, _ = count_tokens.count_tokens(past)
        self.assertGreater(duration, 0)

    def test_sums_multiple_jsonl_files(self):
        with tempfile.TemporaryDirectory() as tmp:
            project_dir = Path(tmp)
            subdir = project_dir / "subagents"
            subdir.mkdir()
            after = datetime(2026, 6, 17, 11, 0, 0, tzinfo=timezone.utc)

            entry = {
                "type": "assistant",
                "timestamp": after.isoformat(),
                "message": {"usage": {"input_tokens": 100, "output_tokens": 50}}
            }
            self._write_jsonl(project_dir / "main.jsonl", [entry])
            self._write_jsonl(subdir / "sub.jsonl", [entry])

            with patch.object(count_tokens, "get_project_dir", return_value=project_dir):
                _, consume, _ = count_tokens.count_tokens("2026-06-17T10:00:00Z")

            self.assertEqual(consume, 300)  # 150 * 2 files


if __name__ == "__main__":
    unittest.main()
