"""Manual regression fixture for Unity Transport receiving large TLS records.

Run inside the private Kind Docker network with a read-only TLS certificate mount.
This forwards the actual WebSocket game stream, then adds RFC 6455 PONG frames in
one TLS write. It requires no game account and does not disable TLS validation.
See Packages/com.unity.transport/AVIAN-PATCHES.md and README.md for usage.
"""

import argparse
import select
import socket
import ssl
import threading
import time


def pong_frames(record_bytes):
    """Create legal unsolicited PONG frames, without inducing a reply flood."""
    frames = bytearray()
    while record_bytes >= 2:
        payload_size = min(125, record_bytes - 2)
        if record_bytes - payload_size - 2 == 1:
            payload_size -= 1
        frames += bytes((0x8A, payload_size)) + bytes(payload_size)
        record_bytes -= payload_size + 2
    return frames


def serve(raw, context, upstream_address, padding):
    upstream = None
    client = None
    closed = threading.Event()
    try:
        raw.settimeout(10)
        client = context.wrap_socket(raw, server_side=True)
        client.settimeout(None)
        upstream = socket.create_connection(upstream_address, timeout=10)
        upstream.settimeout(None)

        def upload():
            try:
                while not closed.is_set():
                    data = client.recv(65536)
                    if not data:
                        break
                    upstream.sendall(data)
            except (OSError, ssl.SSLError):
                pass
            finally:
                closed.set()

        threading.Thread(target=upload, daemon=True).start()
        upgraded = False
        response = bytearray()
        inject_at = None
        while not closed.is_set():
            readable, _, _ = select.select([upstream], [], [], 0.05)
            data = upstream.recv(65536) if readable else b""
            if readable and not data:
                break
            if not upgraded:
                response += data
                if b"\r\n\r\n" not in response:
                    continue
                header, body = response.split(b"\r\n\r\n", 1)
                client.sendall(header + b"\r\n\r\n")
                if not header.startswith(b"HTTP/1.1 101"):
                    break
                upgraded = True
                inject_at = time.monotonic() + 3
                data = body
            if inject_at is not None and time.monotonic() >= inject_at:
                client.sendall(padding)
                print(f"Injected {len(padding)} bytes in one TLS write", flush=True)
                inject_at = None
            if data:
                client.sendall(data)
    except (OSError, ssl.SSLError) as error:
        print(f"Connection closed: {type(error).__name__}: {error}", flush=True)
    finally:
        closed.set()
        for connection in (upstream, client or raw):
            if connection is not None:
                connection.close()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--cert", required=True)
    parser.add_argument("--key", required=True)
    parser.add_argument("--port", type=int, default=18443)
    parser.add_argument("--upstream-host", default="avian-cluster-control-plane")
    parser.add_argument("--upstream-port", type=int, default=30080)
    parser.add_argument("--record-bytes", type=int, choices=(8192, 16384), default=8192)
    args = parser.parse_args()
    context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
    context.minimum_version = ssl.TLSVersion.TLSv1_2
    context.maximum_version = ssl.TLSVersion.TLSv1_2
    context.load_cert_chain(args.cert, args.key)
    padding = pong_frames(args.record_bytes)
    with socket.create_server(("0.0.0.0", args.port)) as listener:
        print(f"TLS regression fixture ready on {args.port}", flush=True)
        while True:
            raw, _ = listener.accept()
            threading.Thread(target=serve, args=(raw, context,
                (args.upstream_host, args.upstream_port), padding), daemon=True).start()


if __name__ == "__main__":
    main()
