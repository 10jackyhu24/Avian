# Internal HTTPS / WSS

The public URL and TCP 8443 do not change. Encryption now covers both network hops:

```text
Client -- HTTPS/WSS --> Caddy -- HTTPS --> Lobby / Matchmaker
                             -- WSS   --> Unity Dedicated Server
```

Caddy decrypts and re-encrypts traffic; this is **two TLS sessions**, not an opaque end-to-end encrypted tunnel. PostgreSQL uses its existing database protocol; this change does not configure database TLS or change the existing database port mapping. Singleplayer loopback UDP is unchanged.

## Deploy

From the repository root:

```powershell
python -m pip install -r backend/tls/requirements.txt
```

Rebuild the Linux Dedicated Server in Unity, then run:

```powershell
python compose.py --all
```

The helper generates/reuses certificates, pipes only the game certificate/key into the Kubernetes Secret without printing its data, and deploys the updated containers. It does not delete database volumes or install certificates into the host/client trust store. A fresh TLS-enabled server build is required; merely changing a diagram or Caddy configuration cannot encrypt an older game binary.

If using Docker Compose directly, first run `python backend/tls/setup_tls.py`. The API containers deliberately refuse to start without their certificate mounts; they do not fall back to HTTP. Direct Compose startup does not install the Kubernetes Secret or rebuild/restart game Pods; use the helper's `--k8s`/`--all` path for that part.

## Trust and keys

| Location / name | Purpose |
| --- | --- |
| backend/.tls/ca.crt | Public internal trust anchor, mounted read-only into Caddy |
| backend/.tls/ca.key | Local signing key; never mounted into any service or stored in Kubernetes |
| backend/.tls/lobby/ | Leaf for DNS SAN avian-lobby |
| backend/.tls/matchmaker/ | Leaf for DNS SAN avian-matchmaker |
| backend/.tls/game/ | Leaf for DNS SAN avian-game-room |
| Kubernetes Secret avian-game-tls | Only the game leaf certificate/key, mounted read-only into room Pods |

API containers receive only their own leaf/key and a public CA copy. Caddy verifies the issuer, validity and expected hostname. For game NodePorts, Caddy uses SNI `avian-game-room` while dialing the configured Kind node. No `tls_insecure_skip_verify` or equivalent bypass is used.

Generated material is excluded by `.gitignore`; keep a secure backup of `.tls` and restrict filesystem access to the deploy operator. POSIX key files are written with mode 0600. On Windows, additionally protect the directory with appropriate NTFS permissions; chmod alone is not an ACL policy. The generator will not silently replace a missing/mismatched authority because that would invalidate the existing trust relationship.

## Expiration and rotation

The root lasts 10 years; leaves last 90 days. Deployment renews leaves when 30 days or less remain. **There is no unattended renewal scheduler.** Run the deployment regularly and monitor expiration:

```powershell
python backend/tls/setup_tls.py --check
```

After generating certificates manually, restart/recreate Lobby, Matchmaker and Caddy so they reload mounted PEMs, and deploy/restart static game Pods with the refreshed Secret. Existing dynamic room processes retain their certificate until they finish; new Jobs use the new Secret. Finish/restart any unusually long-lived room before its old certificate expires. A full `python compose.py --all` refreshes the static server and changed API images; if PEMs were renewed separately, explicitly reload the TLS endpoints:

```powershell
docker compose --env-file backend/.env -f backend/docker-compose.yaml up -d --no-deps --force-recreate avian-lobby avian-matchmaker avian-caddy
```

CA rotation is an explicit maintenance operation: back up existing material, provision the replacement trust/certificates consistently, and recreate every TLS endpoint/room. Do not delete the CA on a live deployment or turn off verification to work around errors. Protect Kubernetes Secret access with RBAC and cluster storage security; Secret base64 encoding is not encryption at rest.

## Verify and troubleshoot

```powershell
python -m unittest discover -s tests -p 'test_*.py' -v
docker compose --env-file backend/.env -f backend/docker-compose.yaml config --quiet
curl.exe https://140.136.151.88:8443/health
python export_client_config.py --output Builds/WindowsClient/network-config.json
```

`/health` checks Caddy only. The exporter also accesses Matchmaker and performs the real game WebSocket upgrade. A full login and Unity client join are still required to check PostgreSQL, NGO and player spawning.

- API health checks verify their own HTTPS certificate using the public internal CA copy.
- `502` / TLS errors: check certificate expiry, CA mismatch, service SAN/SNI, mounted files and whether the Linux server was rebuilt.
- Missing game secret: deploy it into the Pod's namespace before creating/restarting Pods. `compose.py --all` handles default and ROOM_NAMESPACE; custom namespaces must already exist.
- Matchmaker diagnostic mapping is `127.0.0.1:8080` (HTTPS), not a public plaintext bypass.
- Client public-certificate validation and public certificate renewal remain unchanged.

Implementation references: [Caddy upstream TLS and trust pools](https://caddyserver.com/docs/caddyfile/directives/reverse_proxy#the-http-transport), [Uvicorn HTTPS flags](https://github.com/encode/uvicorn/blob/0.34.0/docs/settings.md#https), [Unity Transport WebSocket TLS](https://docs.unity3d.com/Packages/com.unity.transport@2.7/manual/websockets.html#websockets-and-encryption-tls).
