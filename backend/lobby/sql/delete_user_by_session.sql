DELETE FROM users
WHERE id = (
    SELECT user_id
    FROM login_sessions
    WHERE token = %s AND expires_at > CURRENT_TIMESTAMP
)
RETURNING id, username;
