"""Write a Windows client's runtime API config from the active Avian tunnel."""

import argparse
import ipaddress
import json
from pathlib import Path
from urllib.error import URLError
from urllib.request import Request, urlopen


PROJECT_ROOT = Path(__file__).resolve().parent
AVIAN_UPSTREAM = "http://avian-caddy:8000"


def read_json(url):
    request = Request(url, headers={"Accept": "application/json"})
    with urlopen(request, timeout=5) as response:
        return json.load(response)


def is_local_only(endpoint):
    host, separator, _ = endpoint.rpartition(":")
    if not separator:
        return True

    host = host.strip("[]").lower()
    if host == "localhost":
        return True

    try:
        address = ipaddress.ip_address(host)
    except ValueError:
        return False

    return address.is_loopback or address.is_unspecified


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--output",
        default="network-config.json",
        help="Output file next to the Unity client .exe (default: project root).",
    )
    parser.add_argument("--ngrok-port", type=int, default=4040, help="Host port of the ngrok inspector.")
    parser.add_argument("--matchmaker-port", type=int, default=8080, help="Host port of the matchmaker.")
    args = parser.parse_args()

    try:
        tunnels = read_json(f"http://127.0.0.1:{args.ngrok_port}/api/tunnels").get("tunnels", [])
        diagnostics = read_json(f"http://127.0.0.1:{args.matchmaker_port}/match/diagnostics")
    except (OSError, URLError, ValueError, json.JSONDecodeError) as error:
        parser.error(f"Cannot read local ngrok or matchmaker status: {error}")

    avian_tunnels = [
        tunnel for tunnel in tunnels
        if tunnel.get("proto") == "https"
        and tunnel.get("config", {}).get("addr") == AVIAN_UPSTREAM
    ]
    if len(avian_tunnels) != 1:
        parser.error(
            f"Expected one HTTPS ngrok tunnel to avian-caddy:8000 on localhost:{args.ngrok_port}."
        )

    mode = diagnostics.get("room_mode")
    if mode == "static":
        game_endpoint = diagnostics.get("static_world_endpoint", "")
    elif mode == "k8s":
        game_endpoint = diagnostics.get("room_endpoint_example", "")
    else:
        parser.error(f"Unsupported room mode: {mode}")

    if is_local_only(game_endpoint):
        parser.error(
            f"Game endpoint {game_endpoint!r} points to this computer. "
            "Set a reachable UDP endpoint in backend/.env and recreate avian-matchmaker first."
        )

    api_base_url = avian_tunnels[0].get("public_url", "")
    if not api_base_url.startswith("https://"):
        parser.error("Avian ngrok tunnel has no HTTPS public URL.")

    output = Path(args.output).expanduser()
    if not output.is_absolute():
        output = PROJECT_ROOT / output
    if not output.parent.is_dir():
        parser.error(f"Output directory does not exist: {output.parent}")

    output.write_text(json.dumps({"apiBaseUrl": api_base_url}, indent=2) + "\n", encoding="utf-8")
    print(f"Client config written to {output}")
    print(f"Matchmaker currently advertises UDP game endpoint {game_endpoint}")


if __name__ == "__main__":
    main()
