import hashlib
import hmac
import os
import secrets
from contextlib import contextmanager
from datetime import datetime, timezone
from pathlib import Path

import psycopg2
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field


DB_USER = os.getenv("DB_USER")
DB_PASSWORD = os.getenv("DB_PASSWORD")
DB_NAME = os.getenv("DB_NAME")
DB_HOST = os.getenv("DB_HOST")
DB_PORT = int(os.getenv("DB_PORT", "5432"))
PBKDF2_ITERATIONS = int(os.getenv("PASSWORD_HASH_ITERATIONS", "210000"))

SQL_DIR = Path(__file__).resolve().parent / "sql"

app = FastAPI(title="Avian Lobby", version="1.0.0")


class RegisterRequest(BaseModel):
    username: str = Field(min_length=3, max_length=32)
    password: str = Field(min_length=6, max_length=128)


class LoginRequest(BaseModel):
    username: str = Field(min_length=3, max_length=32)
    password: str = Field(min_length=6, max_length=128)


def load_sql(filename: str) -> str:
    return (SQL_DIR / filename).read_text(encoding="utf-8")


def _conn():
    return psycopg2.connect(
        host=DB_HOST,
        port=DB_PORT,
        user=DB_USER,
        password=DB_PASSWORD,
        dbname=DB_NAME,
    )


@contextmanager
def db_cursor(commit: bool = False):
    conn = _conn()
    try:
        with conn:
            with conn.cursor() as cursor:
                yield cursor
        if commit:
            conn.commit()
    finally:
        conn.close()


def _hash_password(password: str, salt: bytes | None = None) -> tuple[str, str]:
    if salt is None:
        salt = secrets.token_bytes(16)
    digest = hashlib.pbkdf2_hmac(
        "sha256",
        password.encode("utf-8"),
        salt,
        PBKDF2_ITERATIONS,
    )
    return salt.hex(), digest.hex()


def _verify_password(password: str, salt_hex: str, expected_hash_hex: str) -> bool:
    salt = bytes.fromhex(salt_hex)
    _, candidate_hash = _hash_password(password, salt)
    return hmac.compare_digest(candidate_hash, expected_hash_hex)


def ensure_schema() -> None:
    with db_cursor(commit=True) as cursor:
        cursor.execute(load_sql("schema_users.sql"))
        cursor.execute(load_sql("schema_login_sessions.sql"))


def create_session(user_id: int, ttl_hours: int = 72) -> str:
    token = secrets.token_urlsafe(32)
    expires_at = datetime.now(timezone.utc).timestamp() + ttl_hours * 3600
    with db_cursor(commit=True) as cursor:
        cursor.execute(load_sql("insert_login_session.sql"), (token, user_id, expires_at))
    return token


def get_user_by_username(username: str):
    with db_cursor() as cursor:
        cursor.execute(load_sql("select_user_by_username.sql"), (username,))
        return cursor.fetchone()


@app.on_event("startup")
def on_startup():
    ensure_schema()


@app.get("/health")
def health():
    return {"ok": True}


@app.post("/auth/register")
def register(payload: RegisterRequest):
    if get_user_by_username(payload.username):
        raise HTTPException(status_code=409, detail="Username already exists")

    salt_hex, hash_hex = _hash_password(payload.password)
    with db_cursor(commit=True) as cursor:
        cursor.execute(
            load_sql("insert_user.sql"),
            (payload.username, salt_hex, hash_hex),
        )
        user_id, username, created_at = cursor.fetchone()

    token = create_session(user_id)
    return {
        "user_id": user_id,
        "username": username,
        "created_at": created_at.isoformat(),
        "session_token": token,
    }


@app.post("/auth/login")
def login(payload: LoginRequest):
    user = get_user_by_username(payload.username)
    if not user:
        raise HTTPException(status_code=401, detail="Invalid username or password")

    user_id, username, salt_hex, hash_hex, created_at = user
    if not _verify_password(payload.password, salt_hex, hash_hex):
        raise HTTPException(status_code=401, detail="Invalid username or password")

    token = create_session(user_id)
    return {
        "user_id": user_id,
        "username": username,
        "created_at": created_at.isoformat(),
        "session_token": token,
    }


@app.get("/auth/validate")
def validate(token: str):
    with db_cursor() as cursor:
        cursor.execute(load_sql("select_session_by_token.sql"), (token,))
        row = cursor.fetchone()

    if not row:
        raise HTTPException(status_code=401, detail="Invalid token")

    session_token, user_id, username, expires_at = row
    if expires_at.tzinfo is None:
        expires_at = expires_at.replace(tzinfo=timezone.utc)
    if expires_at < datetime.now(timezone.utc):
        raise HTTPException(status_code=401, detail="Token expired")

    return {
        "session_token": session_token,
        "user_id": user_id,
        "username": username,
        "expires_at": expires_at.isoformat(),
    }


@app.get("/me")
def me(token: str):
    return validate(token)

