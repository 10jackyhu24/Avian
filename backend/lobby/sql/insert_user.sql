INSERT INTO users (username, password_salt, password_hash)
VALUES (%s, %s, %s)
RETURNING id, username, created_at;

