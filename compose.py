import argparse
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
        clusters = subprocess.run(["kind", "get", "clusters"], cwd=PROJECT_ROOT, check=True, capture_output=True, text=True)
        if "avian-cluster" not in clusters.stdout.splitlines():
            run_commands([["kind", "create", "cluster", "--config", "backend/kind-config.yaml"]], "Create Kind cluster")
        run_commands(K8S_COMMANDS, "Kubernetes (Kind) Deployment")

    if args.docker or args.all:
        run_commands(DOCKER_COMMANDS, "Docker Compose Deployment")

if __name__ == "__main__":
    main()
