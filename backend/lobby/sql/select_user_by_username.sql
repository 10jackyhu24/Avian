SELECT id, username, password_salt, password_hash, created_at
FROM users
WHERE username = %s;

