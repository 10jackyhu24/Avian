import os
from pathlib import Path

import psycopg2


def main():
    db_host = os.getenv("DB_HOST")
    db_port = int(os.getenv("DB_PORT"))
    db_user = os.getenv("DB_USER")
    db_password = os.getenv("DB_PASSWORD")
    db_name = os.getenv("DB_NAME")

    sql_dir = Path(__file__).resolve().parent
    sql_texts = [
        (sql_dir / "schema_users.sql").read_text(encoding="utf-8"),
        (sql_dir / "schema_login_sessions.sql").read_text(encoding="utf-8"),
    ]

    conn = psycopg2.connect(
        host=db_host,
        port=db_port,
        user=db_user,
        password=db_password,
        dbname=db_name,
    )
    try:
        with conn:
            with conn.cursor() as cursor:
                for sql_text in sql_texts:
                    cursor.execute(sql_text)
    finally:
        conn.close()


if __name__ == "__main__":
    main()
