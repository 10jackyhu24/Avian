SELECT u.id, u.username
FROM users u
JOIN login_sessions s ON s.user_id = u.id
WHERE s.token = %s AND s.expires_at > CURRENT_TIMESTAMP
FOR UPDATE OF u;
