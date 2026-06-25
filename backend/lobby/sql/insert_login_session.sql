INSERT INTO login_sessions (token, user_id, expires_at)
VALUES (%s, %s, to_timestamp(%s));

