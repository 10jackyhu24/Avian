# Account deletion API

`DELETE /auth/account` deletes the account identified by a valid login session.
Send the `session_token` returned by registration or login in the HTTP header:

```http
DELETE /auth/account HTTP/1.1
Authorization: Bearer YOUR_SESSION_TOKEN
```

The token determines which account is deleted. A username or user ID is not needed.

For example, in PowerShell:

```powershell
curl.exe --request DELETE "https://140.136.151.88:8443/auth/account" --header "Authorization: Bearer YOUR_SESSION_TOKEN"
```

Successful response (HTTP 200):

```json
{"deleted": true, "user_id": 1, "username": "user1"}
```

This removes that user's row from `users`, including the password salt and hash.
The foreign key `login_sessions.user_id REFERENCES users(id) ON DELETE CASCADE`
also removes **every login session** belonging to the deleted account, including
tokens from other devices. Other users' records remain intact. Tables and columns
are preserved, and the deleted username can be registered again.

Token expiry and account selection are checked in the same SQL statement that
deletes the account. A missing, invalid or expired bearer token returns HTTP 401.
Reusing any token after its account is deleted also fails authentication.
The client should clear its stored session token after a successful deletion.

## In-game account deletion

After logging in, the multiplayer menu has a red **Delete Account** button in
the lower-right corner. It opens a confirmation window showing the current
username. Cancel or the close button dismisses it. Confirm sends the DELETE
request with the current session token in the Authorization header.

While the request is pending, a modal progress window blocks repeated actions.
On success, the game clears its cached session and credential inputs, resets the
menu history, and returns to the login screen. An HTTP 401 response clears the
expired/invalid session and returns to login. Other request failures show the
error and allow retrying.

## Caddy and deployment

The existing Caddy `handle /auth/*` route forwards this DELETE request and its
Authorization header to the lobby service over internal HTTPS.

Deploy from the `backend` directory on the deployment host:

```bash
docker compose up -d --build avian-lobby
```
