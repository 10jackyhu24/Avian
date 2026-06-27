SELECT s.token, u.id, u.username, s.expires_at
FROM login_sessions s
JOIN users u ON u.id = s.user_id
WHERE s.token = %s;

