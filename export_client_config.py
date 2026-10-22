"""Write client config after checking Caddy HTTPS and the advertised game endpoint."""

import argparse
import base64
import hashlib
import http.client
import ipaddress
import json
import secrets
from pathlib import Path
from urllib.error import URLError
from urllib.request import Request, urlopen
from urllib.parse import urlsplit


PROJECT_ROOT = Path(__file__).resolve().parent


def read_json(url):
    request = Request(url, headers={"Accept": "application/json"})
    with urlopen(request, timeout=5) as response:
        return json.load(response)


def is_local_only(endpoint):
    try:
        parsed = urlsplit(endpoint if "://" in endpoint else "udp://" + endpoint)
        host = parsed.hostname
        port = parsed.port if parsed.port is not None else {"ws": 80, "wss": 443}.get(parsed.scheme)
    except ValueError:
        return True
    if parsed.scheme not in ("udp", "ws", "wss") or not host or not port or parsed.username or parsed.password or parsed.query or parsed.fragment:
        return True
    host = host.lower()
    if host == "localhost":
        return True

    try:
        address = ipaddress.ip_address(host)
    except ValueError:
        return False

    return address.is_loopback or address.is_unspecified


def check_websocket(endpoint):
    parsed = urlsplit(endpoint)
    connection_type = http.client.HTTPSConnection if parsed.scheme == "wss" else http.client.HTTPConnection
    key = base64.b64encode(secrets.token_bytes(16)).decode("ascii")
    connection = connection_type(parsed.hostname, parsed.port, timeout=8)
    try:
        connection.putrequest("GET", parsed.path or "/")
        for name, value in {
            "Upgrade": "websocket", "Connection": "Upgrade",
            "Sec-WebSocket-Key": key, "Sec-WebSocket-Version": "13",
        }.items():
            connection.putheader(name, value)
        connection.endheaders()
        response = connection.getresponse()
        expected = base64.b64encode(hashlib.sha1((key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11").encode("ascii")).digest()).decode("ascii")
        if response.status != 101 or response.getheader("Sec-WebSocket-Accept") != expected:
            raise ValueError(f"Game WebSocket upgrade failed: HTTP {response.status}")
    finally:
        connection.close()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--output",
        default="network-config.json",
        help="Output file next to the Unity client .exe (default: project root).",
    )
    parser.add_argument("--api-url", help="HTTPS API base URL; defaults to the project's network-config.json.")
    args = parser.parse_args()

    api_base_url = args.api_url
    if api_base_url is None:
        try:
            api_base_url = json.loads((PROJECT_ROOT / "network-config.json").read_text(encoding="utf-8"))["apiBaseUrl"]
        except (OSError, URLError, ValueError) as error:
            parser.error(f"Cannot read network-config.json; pass --api-url: {error}")
        except (KeyError, TypeError):
            parser.error("network-config.json must contain apiBaseUrl.")
    if not isinstance(api_base_url, str):
        parser.error("apiBaseUrl must be a URL string.")
    api_base_url = api_base_url.strip().rstrip("/")
    try:
        parsed = urlsplit(api_base_url)
        valid = (parsed.scheme in ("http", "https") and parsed.hostname
                 and not parsed.username and not parsed.password and not parsed.query and not parsed.fragment)
        parsed.port
    except ValueError:
        valid = False
    if not valid:
        parser.error("--api-url must be an HTTP(S) base URL without credentials, query, or fragment.")
    try:
        diagnostics = read_json(f"{api_base_url}/match/diagnostics")
    except (OSError, URLError, ValueError) as error:
        parser.error(f"Cannot read matchmaking diagnostics: {error}")

    if not isinstance(diagnostics, dict):
        parser.error("Matchmaking diagnostics did not return a JSON object.")

    mode = diagnostics.get("room_mode")
    if mode == "static":
        game_endpoint = diagnostics.get("static_world_endpoint", "")
    elif mode == "k8s":
        game_endpoint = diagnostics.get("room_endpoint_example", "")
    else:
        parser.error(f"Unsupported room mode: {mode}")

    if not isinstance(game_endpoint, str) or is_local_only(game_endpoint):
        parser.error(
            f"Game endpoint {game_endpoint!r} is invalid or local-only. "
            "Set a reachable WSS game endpoint in backend/.env and recreate avian-matchmaker first."
        )

    if game_endpoint.startswith(("wss://", "ws://")):
        try:
            check_websocket(game_endpoint)
        except (OSError, ValueError, http.client.HTTPException) as error:
            parser.error(f"Cannot connect to the game WebSocket: {error}")

    output = Path(args.output).expanduser()
    if not output.is_absolute():
        output = PROJECT_ROOT / output
    if not output.parent.is_dir():
        parser.error(f"Output directory does not exist: {output.parent}")

    output.write_text(json.dumps({"apiBaseUrl": api_base_url}, indent=2) + "\n", encoding="utf-8")
    print(f"Client config written to {output}")
    print(f"Game endpoint: {game_endpoint}")
    print("HTTPS and WebSocket checks do not verify Netcode/player spawning; test a match from another computer.")


if __name__ == "__main__":
    main()
