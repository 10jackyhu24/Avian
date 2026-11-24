"""Account deletion authorization tests, using a mocked database connection."""

import unittest
from unittest.mock import patch

from fastapi.testclient import TestClient

from backend.lobby import main as lobby


class AccountDeletionTests(unittest.TestCase):
    def setUp(self):
        self.enterContext(patch.object(lobby, "ensure_schema"))
        self.client = self.enterContext(TestClient(lobby.app))
        self.connect = self.enterContext(patch.object(lobby, "_conn"))
        self.cursor = self.connect.return_value.cursor.return_value.__enter__.return_value

    def test_missing_token_is_rejected_before_database_access(self):
        response = self.client.delete("/auth/account")
        self.assertEqual(response.status_code, 401)
        self.assertEqual(response.headers["www-authenticate"], "Bearer")
        self.connect.assert_not_called()

    def test_non_bearer_authorization_is_rejected(self):
        response = self.client.delete(
            "/auth/account", headers={"Authorization": "Basic credentials"},
        )
        self.assertEqual(response.status_code, 401)
        self.connect.assert_not_called()

    def test_invalid_or_expired_session_cannot_delete_an_account(self):
        self.cursor.fetchone.return_value = None
        response = self.client.delete(
            "/auth/account", headers={"Authorization": "Bearer invalid-token"},
        )
        self.assertEqual(response.status_code, 401)
        self.assertEqual(response.json(), {"detail": "Invalid or expired session"})

    def test_deletion_uses_session_owner_instead_of_supplied_username(self):
        self.cursor.fetchone.side_effect = [(7, "user1"), (0,)]
        response = self.client.request(
            "DELETE", "/auth/account",
            headers={"Authorization": "Bearer user1-session-token"},
            json={"username": "user2", "user_id": 8},
        )
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.json(), {"deleted": True, "user_id": 7, "username": "user1", "remaining_sessions": 0})
        self.assertEqual(self.cursor.execute.call_args_list[0].args[1], ("user1-session-token",))
        self.assertEqual(self.cursor.execute.call_args_list[1].args[1], (7,))

    def test_remaining_sessions_cause_failure_inside_transaction(self):
        self.cursor.fetchone.side_effect = [(7, "user1"), (1,)]
        response = self.client.delete(
            "/auth/account", headers={"Authorization": "Bearer user1-session-token"},
        )
        self.assertEqual(response.status_code, 500)
        self.assertEqual(response.json(), {"detail": "Account deletion did not remove all sessions"})
        connection = self.connect.return_value
        self.assertIs(connection.__exit__.call_args.args[0], lobby.HTTPException)
        connection.commit.assert_not_called()


if __name__ == "__main__":
    unittest.main()
