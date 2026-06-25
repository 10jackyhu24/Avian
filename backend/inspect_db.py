import argparse
import os
import subprocess
import sys

def load_env_variables():
    """
    Manually parses the .env file to extract database credentials
    without using any third-party libraries like python-dotenv.
    """
    env_data = {}
    # Look for .env in the current directory or one level up
    paths_to_try = [".env", "../.env"]
    env_file_path = None
    
    for path in paths_to_try:
        if os.path.exists(path):
            env_file_path = path
            break
            
    if not env_file_path:
        print("Warning: .env file not found. Falling back to default values.", file=sys.stderr)
        return "postgres", "postgres"

    # Read and parse the .env file line by line
    with open(env_file_path, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            # Ignore comments and empty lines
            if not line or line.startswith("#"):
                continue
            if "=" in line:
                key, val = line.split("=", 1)
                # Clean up quotes if present
                env_data[key.strip()] = val.strip().strip('"').strip("'")

    # Extract user and db name, fallback to 'postgres' if not defined
    db_user = env_data.get("DB_USER", "postgres")
    db_name = env_data.get("DB_NAME", "postgres")
    return db_user, db_name

def run_docker_psql(sql_command):
    """
    Executes a psql command inside the postgres_db container dynamically
    using the credentials found in the .env file.
    """
    # Dynamically get the correct username and database name from .env
    db_user, db_name = load_env_variables()

    cmd = [
        "docker", "exec", "-i", "postgres_db", 
        "psql", "-U", db_user, "-d", db_name, "-c", sql_command
    ]
    
    try:
        result = subprocess.run(cmd, capture_output=True, text=True, check=True)
        print(result.stdout)
    except subprocess.CalledProcessError as e:
        print(f"Error executing command inside Docker:\n{e.stderr}", file=sys.stderr)
    except FileNotFoundError:
        print("Error: 'docker' command not found.", file=sys.stderr)

def main():
    parser = argparse.ArgumentParser(description="Inspect the PostgreSQL database inside Docker container.")
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--tables", action="store_true", help="List all tables in the database")
    group.add_argument("--query", type=str, help="Execute a custom SQL SELECT query")

    args = parser.parse_args()

    if args.tables:
        run_docker_psql("\\dt")
    elif args.query:
        run_docker_psql(args.query)

if __name__ == "__main__":
    main()