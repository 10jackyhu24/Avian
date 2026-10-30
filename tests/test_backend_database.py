"""Ordered live database/account lifecycle tests; run after BackendConnection."""

import secrets
import unittest
from uuid import uuid4

from backend_api import BackendAPI


class BackendDatabaseTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.api = BackendAPI()
        cls.username = "ci_" + uuid4().hex[:29]
        cls.password = secrets.token_urlsafe(24)
        cls.database_ready = False
        cls.user_ready = False
        cls.cleanup_needed = False
        cls.account_deleted = False
        cls.user_id = None
        cls.tokens = []

    def test_01_connect_to_database(self):
        status, body = self.api.request("GET", "/auth/health/database")
        self.assertEqual(
            status, 200,
            "Deploy /auth/health/database first. HTTP 503 means PostgreSQL is unavailable.",
        )
        self.assertIs(body.get("ok"), True)
        self.assertEqual(body.get("database"), "ok")
        type(self).database_ready = True

    def test_02_create_user_with_multiple_sessions(self):
        if not self.database_ready:
            self.skipTest("Database connection test did not pass.")
        cls = type(self)
        cls.cleanup_needed = True
        status, account = self.api.request(
            "POST", "/auth/register", {"username": self.username, "password": self.password},
        )
        if status < 500 and status not in (200, 201):
            cls.cleanup_needed = False
        token = account.get("session_token")
        if isinstance(token, str) and token:
            cls.tokens.append(token)
        self.assertEqual(status, 200)
        self.assertEqual(account.get("username"), self.username)
        self.assertIs(type(account.get("user_id")), int)
        self.assertGreater(account["user_id"], 0)
        cls.user_id = account["user_id"]
        self.assertTrue(isinstance(token, str) and bool(token), "Registration must return a session token.")
        for field in ("password", "password_salt", "password_hash"):
            self.assertFalse(field in account, f"Registration exposed {field}.")

        # A second login represents another device/session for the same account.
        status, login = self.api.request(
            "POST", "/auth/login", {"username": self.username, "password": self.password},
        )
        second_token = login.get("session_token")
        if isinstance(second_token, str) and second_token:
            cls.tokens.append(second_token)
        self.assertEqual(status, 200)
        self.assertEqual(login.get("user_id"), self.user_id)
        self.assertEqual(login.get("username"), self.username)
        self.assertTrue(isinstance(second_token, str) and bool(second_token), "Login must return a session token.")
        self.assertEqual(len(set(self.tokens)), 2, "Registration and login must create separate sessions.")

        for index, session_token in enumerate(self.tokens, start=1):
            status, session = self.api.request("GET", "/auth/validate", params={"token": session_token})
            self.assertEqual(status, 200, f"Session {index} must be valid before deletion.")
            self.assertEqual(session.get("user_id"), self.user_id)
            self.assertEqual(session.get("username"), self.username)
        cls.user_ready = True

    def test_03_delete_account_and_all_sessions(self):
        if not self.user_ready:
            self.skipTest("User creation and session checks did not pass.")
        status, deleted = self.api.request("DELETE", "/auth/account", token=self.tokens[-1])
        if status == 200 and deleted.get("deleted") is True:
            type(self).account_deleted = True
        self.assertEqual(status, 200)
        self.assertIs(deleted.get("deleted"), True)
        self.assertEqual(deleted.get("user_id"), self.user_id)
        self.assertEqual(deleted.get("username"), self.username)
        self.assertIs(type(deleted.get("remaining_sessions")), int)
        self.assertEqual(
            deleted.get("remaining_sessions"), 0,
            "The deployed deletion API must verify that no database sessions remain.",
        )

        for index, session_token in enumerate(self.tokens, start=1):
            with self.subTest(revoked_session=index):
                status, _ = self.api.request("GET", "/auth/validate", params={"token": session_token})
                self.assertEqual(status, 401)
        status, _ = self.api.request(
            "POST", "/auth/login", {"username": self.username, "password": self.password},
        )
        self.assertEqual(status, 401, "Deleted accounts must not be able to log in.")

    @classmethod
    def tearDownClass(cls):
        if not cls.cleanup_needed or cls.account_deleted:
            return
        # Only delete tokens whose owner matches this run's random username.
        for token in reversed(cls.tokens):
            if cls._cleanup_with_token(token):
                return
        status, login = cls.api.request(
            "POST", "/auth/login", {"username": cls.username, "password": cls.password},
        )
        if status == 401:
            return  # No account accessible with this run's credentials remains.
        token = login.get("session_token")
        if status == 200 and isinstance(token, str) and cls._cleanup_with_token(token):
            return
        raise AssertionError(f"Could not clean up test account {cls.username}. Check the deployed deletion API.")

    @classmethod
    def _cleanup_with_token(cls, token):
        status, owner = cls.api.request("GET", "/auth/validate", params={"token": token})
        if status != 200 or owner.get("username") != cls.username:
            return False
        status, response = cls.api.request("DELETE", "/auth/account", token=token)
        return status == 200 and response.get("deleted") is True


if __name__ == "__main__":
    unittest.main(verbosity=2)
