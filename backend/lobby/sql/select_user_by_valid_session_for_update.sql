SELECT u.id, u.username, u.password_salt, u.password_hash
FROM users u
JOIN login_sessions s ON s.user_id = u.id
WHERE s.token = %s AND s.expires_at > CURRENT_TIMESTAMP
FOR UPDATE OF u;
