UPDATE users
SET password_salt = %s, password_hash = %s
WHERE id = %s;
