import argparse
import os
import re
import subprocess
import sys
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parent

# Define command sequences as lists of arguments for safety and cross-platform compatibility
DOCKER_COMMANDS = [
    ["docker", "compose", "--env-file", "backend/.env", "-f", "backend/docker-compose.yaml", "up", "-d", "--build", "--remove-orphans"]
]

K8S_COMMANDS = [
    ["docker", "build", "-t", "avian-dedicated-server:v1", "-f", "backend/Linux_Dedicated_Server/Dockerfile", "Builds/LinuxServer"],
    ["kind", "export", "kubeconfig", "--name", "avian-cluster"],
    ["kind", "load", "docker-image", "avian-dedicated-server:v1", "--name", "avian-cluster"],
    ["kubectl", "apply", "-f", "backend/avian-cluster.yaml"],
    ["kubectl", "rollout", "restart", "deployment/avian-rooms-pool-deployment"],
    ["kubectl", "rollout", "status", "deployment/avian-rooms-pool-deployment", "--timeout=180s"]
]


def room_namespace():
    """Read only this non-secret setting; never print the contents of .env."""
    namespace = os.getenv("ROOM_NAMESPACE", "").strip()
    env_file = PROJECT_ROOT / "backend/.env"
    if not namespace and env_file.is_file():
        for line in env_file.read_text(encoding="utf-8-sig").splitlines():
            key, separator, value = line.partition("=")
            if separator and key.strip() == "ROOM_NAMESPACE":
                namespace = value.split("#", 1)[0].strip().strip("\"'")
                break
    namespace = namespace or "default"
    if len(namespace) > 63 or not re.fullmatch(r"[a-z0-9](?:[a-z0-9-]*[a-z0-9])?", namespace):
        raise ValueError("ROOM_NAMESPACE must be a Kubernetes namespace name.")
    return namespace


def install_game_tls_secret(directory, namespace):
    """Pipe the generated Secret directly to kubectl; do not log its key/data."""
    print(f"Installing internal game TLS certificate in namespace {namespace} (private data not logged).")
    generated = subprocess.run([
        "kubectl", "create", "secret", "tls", "avian-game-tls", "--namespace", namespace,
        "--cert", str(directory / "game/tls.crt"), "--key", str(directory / "game/tls.key"),
        "--dry-run=client", "-o", "yaml",
    ], cwd=PROJECT_ROOT, check=True, capture_output=True, text=True)
    subprocess.run(["kubectl", "apply", "--namespace", namespace, "-f", "-"],
                   input=generated.stdout, cwd=PROJECT_ROOT, check=True, text=True)

def run_commands(commands, description):
    """Executes a list of commands sequentially. Exits if any command fails."""
    print(f"=== Starting: {description} ===")
    for cmd in commands:
        print(f"Running: {' '.join(cmd)}")
        try:
            # check=True raises CalledProcessError if the command fails (non-zero exit code)
            subprocess.run(cmd, check=True, cwd=PROJECT_ROOT)
        except subprocess.CalledProcessError as e:
            print(f"Error: Command failed with exit code {e.returncode}", file=sys.stderr)
            sys.exit(e.returncode)
        except FileNotFoundError:
            print(f"Error: Command '{cmd[0]}' not found. Please ensure it is installed and in your PATH.", file=sys.stderr)
            sys.exit(1)
    print(f"=== Completed: {description} successfully ===\n")

def main():
    parser = argparse.ArgumentParser(description="DevOps automation script for Docker and Kubernetes (Kind).")
    
    # Use mutually exclusive group to prevent mixing conflicting flags, unless handled via choices
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--docker", action="store_true", help="Deploy using Docker Compose.")
    group.add_argument("--k8s", action="store_true", help="Deploy using local Kubernetes (Kind).")
    group.add_argument("--all", action="store_true", help="Deploy both Docker Compose and Kubernetes.")

    args = parser.parse_args()

    if args.k8s or args.all:
        server = PROJECT_ROOT / "Builds/LinuxServer/AvianServer.x86_64"
        if not server.is_file():
            parser.error(f"Build the Linux Dedicated Server first: missing {server}")

    try:
        from backend.tls.setup_tls import ensure_certificates
        certificates = ensure_certificates()
        namespace = room_namespace()
    except ImportError:
        parser.error("Install TLS tooling first: python -m pip install -r backend/tls/requirements.txt")
    except (OSError, ValueError) as error:
        parser.error(str(error))
    print(f"Internal TLS certificates ready; renewed: {', '.join(certificates.renewed) or 'none'}")

    if args.k8s or args.all:
        clusters = subprocess.run(["kind", "get", "clusters"], cwd=PROJECT_ROOT, check=True, capture_output=True, text=True)
        if "avian-cluster" not in clusters.stdout.splitlines():
            run_commands([["kind", "create", "cluster", "--config", "backend/kind-config.yaml"]], "Create Kind cluster")
        run_commands(K8S_COMMANDS[:3], "Build and load Kubernetes server image")
        try:
            # The static Deployment is in default; dynamic Jobs may use another namespace.
            for target in sorted({"default", namespace}):
                install_game_tls_secret(certificates.directory, target)
        except (subprocess.CalledProcessError, FileNotFoundError) as error:
            parser.exit(1, f"Could not install game TLS Secret: {error}\n")
        run_commands(K8S_COMMANDS[3:], "Kubernetes (Kind) Deployment")

    if args.docker or args.all:
        run_commands(DOCKER_COMMANDS, "Docker Compose Deployment")
        if certificates.renewed:
            # Mounted PEM changes alone do not reload Uvicorn/Caddy TLS contexts.
            # Recreate only TLS endpoints, never the database dependency.
            command = DOCKER_COMMANDS[0][:6] + ["up", "-d", "--no-deps", "--force-recreate",
                                                "avian-lobby", "avian-matchmaker", "avian-caddy"]
            run_commands([command], "Reload renewed TLS certificates")

if __name__ == "__main__":
    main()
