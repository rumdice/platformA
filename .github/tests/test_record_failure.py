"""
record_failure.py 유닛 테스트.

실제 DB 연결 없이 psycopg2를 Mock하여 로직만 검증한다.
"""

import sys
import os
import unittest
from unittest.mock import MagicMock, patch, call

# scripts 경로를 import 경로에 추가
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "scripts"))

import record_failure


class TestParseConn(unittest.TestCase):
    def test_default_values(self):
        result = record_failure.parse_conn(
            "Host=localhost;Port=5432;Database=platforma_sdlc;Username=platforma;Password=platforma_dev_password"
        )
        self.assertEqual(result["host"], "localhost")
        self.assertEqual(result["port"], 5432)
        self.assertEqual(result["dbname"], "platforma_sdlc")
        self.assertEqual(result["user"], "platforma")
        self.assertEqual(result["password"], "platforma_dev_password")

    def test_custom_values(self):
        result = record_failure.parse_conn(
            "Host=db-server;Port=5433;Database=my_db;Username=admin;Password=secret"
        )
        self.assertEqual(result["host"], "db-server")
        self.assertEqual(result["port"], 5433)
        self.assertEqual(result["dbname"], "my_db")
        self.assertEqual(result["user"], "admin")
        self.assertEqual(result["password"], "secret")

    def test_missing_keys_use_defaults(self):
        result = record_failure.parse_conn("")
        self.assertEqual(result["host"], "localhost")
        self.assertEqual(result["port"], 5432)
        self.assertEqual(result["dbname"], "platforma_sdlc")


class TestRecord(unittest.TestCase):
    def _make_mock_conn(self):
        mock_conn = MagicMock()
        mock_cur = MagicMock()
        mock_conn.cursor.return_value.__enter__ = MagicMock(return_value=mock_cur)
        mock_conn.cursor.return_value.__exit__ = MagicMock(return_value=False)
        return mock_conn, mock_cur

    @patch("record_failure.get_conn")
    def test_record_success(self, mock_get_conn):
        mock_conn, mock_cur = self._make_mock_conn()
        mock_get_conn.return_value = mock_conn

        result = record_failure.record(
            failure_type="build_failed",
            branch="test-branch",
            message="빌드 실패 테스트",
        )

        self.assertTrue(result)
        mock_cur.execute.assert_called_once()
        sql_args = mock_cur.execute.call_args[0]
        params = sql_args[1]
        self.assertEqual(params[0], "build_failed")
        self.assertEqual(params[2], "빌드 실패 테스트")
        mock_conn.commit.assert_called_once()

    @patch("record_failure.get_conn")
    def test_record_returns_false_when_no_conn(self, mock_get_conn):
        mock_get_conn.return_value = None
        result = record_failure.record("build_failed", "branch", "msg")
        self.assertFalse(result)

    @patch("record_failure.get_conn")
    def test_record_fixable_flag(self, mock_get_conn):
        mock_conn, mock_cur = self._make_mock_conn()
        mock_get_conn.return_value = mock_conn

        record_failure.record("format_failed", "feat/x", "포맷 오류", fixable=True)

        params = mock_cur.execute.call_args[0][1]
        self.assertTrue(params[3])  # fixable_by_ai

    @patch("record_failure.get_conn")
    def test_record_with_github_ids(self, mock_get_conn):
        mock_conn, mock_cur = self._make_mock_conn()
        mock_get_conn.return_value = mock_conn

        record_failure.record(
            "test_failed", "main", "테스트 실패",
            github_run_id=12345, github_job_id=67890,
        )

        params = mock_cur.execute.call_args[0][1]
        self.assertEqual(params[7], 12345)  # git_hub_run_id
        self.assertEqual(params[8], 67890)  # git_hub_job_id


class TestListUnresolved(unittest.TestCase):
    def _make_mock_conn(self, rows):
        mock_conn = MagicMock()
        mock_cur = MagicMock()
        mock_cur.fetchall.return_value = rows
        mock_conn.cursor.return_value.__enter__ = MagicMock(return_value=mock_cur)
        mock_conn.cursor.return_value.__exit__ = MagicMock(return_value=False)
        return mock_conn, mock_cur

    @patch("record_failure.get_conn")
    def test_list_with_branch(self, mock_get_conn):
        from datetime import datetime
        mock_conn, mock_cur = self._make_mock_conn([
            ("build_failed", "빌드 실패", datetime(2026, 6, 5), "{}"),
        ])
        mock_get_conn.return_value = mock_conn

        result = record_failure.list_unresolved("test-branch")

        self.assertEqual(len(result), 1)
        self.assertEqual(result[0]["failure_type"], "build_failed")
        self.assertEqual(result[0]["branch"], "test-branch")
        # branch가 있을 때 파라미터에 branch가 전달되는지 확인
        sql = mock_cur.execute.call_args[0][0]
        self.assertIn("branch = %s", sql)

    @patch("record_failure.get_conn")
    def test_list_all_when_no_branch(self, mock_get_conn):
        from datetime import datetime
        mock_conn, mock_cur = self._make_mock_conn([
            ("format_failed", "test-n8n", "포맷 오류", datetime(2026, 6, 5)),
        ])
        mock_get_conn.return_value = mock_conn

        result = record_failure.list_unresolved("")

        sql = mock_cur.execute.call_args[0][0]
        self.assertNotIn("branch = %s", sql)

    @patch("record_failure.get_conn")
    def test_list_returns_empty_on_no_conn(self, mock_get_conn):
        mock_get_conn.return_value = None
        result = record_failure.list_unresolved("branch")
        self.assertEqual(result, [])


class TestResolve(unittest.TestCase):
    def _make_mock_conn(self):
        mock_conn = MagicMock()
        mock_cur = MagicMock()
        mock_conn.cursor.return_value.__enter__ = MagicMock(return_value=mock_cur)
        mock_conn.cursor.return_value.__exit__ = MagicMock(return_value=False)
        return mock_conn, mock_cur

    @patch("record_failure.get_conn")
    def test_resolve_success(self, mock_get_conn):
        mock_conn, mock_cur = self._make_mock_conn()
        mock_get_conn.return_value = mock_conn

        result = record_failure.resolve("test-branch", "build_failed")

        self.assertTrue(result)
        mock_cur.execute.assert_called_once()
        params = mock_cur.execute.call_args[0][1]
        self.assertIn("test-branch", params)
        self.assertIn("build_failed", params)
        mock_conn.commit.assert_called_once()

    @patch("record_failure.get_conn")
    def test_resolve_returns_false_on_no_conn(self, mock_get_conn):
        mock_get_conn.return_value = None
        result = record_failure.resolve("branch", "build_failed")
        self.assertFalse(result)


if __name__ == "__main__":
    unittest.main()
