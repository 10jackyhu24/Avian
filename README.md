# Avian

Avian uses Caddy HTTPS for login and matchmaking, and encrypted WebSockets (WSS) for multiplayer gameplay. Both use public TCP **8443**. Public game clients no longer require a working UDP return path or ngrok.

## Connect from this or another computer

Use Unity **6000.3.15f1** with `Assets/Scenes/SampleScene.unity`, or copy the **whole** freshly built `Builds/WindowsClient` folder to the other computer. Older clients that only support UDP cannot use the new WSS endpoint.

Put `network-config.json` in the project root for the Editor, or beside `Avian.exe` for a Windows player:

```json
{ "apiBaseUrl": "https://140.136.151.88:8443" }
```

Both computers use this same API address. Log in with different accounts, choose Match, and select characters. Matchmaking supplies the game address:

```text
wss://140.136.151.88:8443/game/30080
```

The client validates the public TLS certificate. Caddy decrypts the connection and forwards WebSockets over the private Docker/Kind network to the game server. The menu enters gameplay only after Netcode connects and the local player object spawns; a failed join times out and permits retry. If the connection later drops, it clears the active world state and returns to character selection with the disconnect reason, instead of leaving an empty map on screen.

The project includes Unity Transport 2.7.2 in `Packages/com.unity.transport` with a TLS receive-buffer fix. Without it, valid large TLS records can cause `AuthenticationFailure` shortly after spawning, particularly when a reverse proxy combines small WebSocket frames. Preserve this embedded package when moving the project. See [the patch notes](Packages/com.unity.transport/AVIAN-PATCHES.md) and [the regression fixture](tests/README-wss-tls.md).

## Build and deploy the host

Install Git LFS, Docker Desktop, Kind, kubectl, Python, and Unity's Linux Dedicated Server build support. Run commands from the project root.

1. Copy `backend/.env.example` to `backend/.env` if it does not exist. Preserve existing database credentials. Set:

   ```env
   AVIAN_API_HOST=140.136.151.88
   AVIAN_HTTPS_PORT=8443
   AVIAN_HTTP_PORT=8000
   ROOM_MODE=static
   GAME_TRANSPORT=websocket
   WORLD_ENDPOINT=wss://140.136.151.88:8443/game/30080
   ROOM_PUBLIC_BASE_URL=wss://140.136.151.88:8443/game
   AVIAN_GAME_GATEWAY_HOST=avian-cluster-control-plane
   STATIC_ROOM_SEED=100000
   ```

   `STATIC_ROOM_SEED` must match `MAP_SEED` in `backend/avian-cluster.yaml`. This static server shares one world across its matches. Its `QUIT_WHEN_EMPTY=false` keeps it available between matches; dynamic room Jobs retain their automatic empty-room cleanup.

   The `.env.example` game URLs reference `${AVIAN_API_HOST}` and `${AVIAN_HTTPS_PORT}`. Keep those two variables above their references when editing `.env`; the current `.env` is arranged this way. With these references, the game URLs follow changes to the host IP automatically.

2. Build the server and Windows client using **Avian > Build server and Windows client** in Unity. This explicitly chooses the correct scene for each build. Outputs are `Builds/LinuxServer/AvianServer.x86_64` and `Builds/WindowsClient/Avian.exe`. When Unity is closed, batch builds are also available:

   ```powershell
   & 'C:\Program Files\Unity\Hub\Editor\6000.3.15f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath "$PWD" -buildTarget Linux64 -standaloneBuildSubtarget Server -executeMethod AvianBuild.BuildServer -logFile server-build.log
   ```

   ```powershell
   & 'C:\Program Files\Unity\Hub\Editor\6000.3.15f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath "$PWD" -buildTarget Win64 -standaloneBuildSubtarget Player -executeMethod AvianBuild.BuildClient -logFile client-build.log
   ```

3. Deploy:

   ```powershell
   python compose.py --all
   ```

   The script creates Kind only when missing, loads the new game image, updates the server, and starts Compose with Caddy. It reuses the existing cluster and database. Caddy joins the existing `kind` Docker network and reaches TCP NodePorts directly, including on an older cluster whose Windows port mappings only expose UDP. Game ports do not need public forwarding.

4. Check HTTPS and export a client config:

   ```powershell
   curl.exe https://140.136.151.88:8443/health
   python export_client_config.py --output Builds/WindowsClient/network-config.json
   ```

   The exporter verifies API TLS, the advertised endpoint, and a real WebSocket upgrade through Caddy before writing the file. This checks the proxy path; a full match additionally checks Netcode and player spawning. Copy the entire Windows client folder to each player.

## Public certificate and sharing this IP

Public TCP **80** must reach Caddy's ACME HTTP challenge for certificate issuance and renewal. Public TCP **8443** must reach Avian for HTTPS and WSS. Certificates and keys persist in the `caddy_data` and `caddy_config` Docker volumes. Caddy renews the public IP certificate automatically; keep the challenge route working.

This computer already runs another Caddy for File-Converter on ports 80/443. Its `C:\D\Programing\File-Converter\Caddyfile` imports:

```caddyfile
import C:/D/Unity_project/Avian/backend/Caddyfile.acme-proxy
```

This forwards only IP HTTP ACME challenge requests to `127.0.0.1:8000`. File-Converter continues using its existing HTTPS 443 routes. Use `https://140.136.151.88:8443/health` for Avian; omitting `:8443` reaches the existing service.

On a new host with port 80 free, expose the challenge listener directly:

```powershell
docker compose --env-file backend/.env -f backend/docker-compose.yaml -f backend/docker-compose.public.yaml up -d
```

If another Caddy owns port 80 on the new host, import the ACME proxy there and update its path and process environment instead. The host Caddy does not read Avian's `.env` automatically. Preserve its existing environment when reloading it.

Moving the backend requires updating `AVIAN_API_HOST`, the certificate challenge route, and clients' `apiBaseUrl`. The default game URL references follow the host IP and HTTPS port; update `WORLD_ENDPOINT` and `ROOM_PUBLIC_BASE_URL` too if you replaced them with literal overrides. If using a domain, point its DNS to the new host. Caddy does not open firewall/router ports automatically.

## Dynamic rooms

`ROOM_MODE=k8s` creates one server per match on ports 30081?30084, using `GAME_TRANSPORT=websocket`. Each endpoint is `ROOM_PUBLIC_BASE_URL/<node_port>`; Caddy permits only `/game/30080` through `/game/30084`. Keep the configured port pool within that range or update the proxy allowlist with it. Each room Job receives its own seed and uses a TCP Service.

## Safe zone

Singleplayer and multiplayer share a server-controlled, three-stage safe zone.
The clock starts after the first player has spawned locally, finished loading the
world, and notified the server that gameplay is ready. Creating a room, accepting
a connection, or waiting at character selection does not start it. When the last
ready player disconnects, the static server resets the zone and waits for the
next entry; the next game starts with the full initial 90-second wait. A player
joining an ongoing shared world inherits that world's clock. Each seeded target circle
is fully inside its parent circle and has a different center.

| Match time | Action | Radius on the default 1000-unit map |
| --- | --- | --- |
| 0:00–1:30 | Wait and show the first target | 500 |
| 1:30–3:00 | First shrink | 500 → 225 |
| 3:00–4:00 | Hold and show the second target | 225 |
| 4:00–5:30 | Second shrink | 225 → 90 |
| 5:30–6:30 | Hold and show the final target | 90 |
| 6:30–8:00 | Final shrink | 90 → 25 |
| After 8:00 | Keep the final circle fixed | 25 |

Outside players and active enemies take 2 / 5 / 10 HP per second in stages 1 / 2 / 3.
Damage continues after the final shrink. The outside world is tinted translucent
light blue. Both map sizes show the current circle in cyan, the next target as a
dashed white circle and cross, and a countdown, even over unexplored areas.

Configure duration, final radius, damage and tint in `Assets/Resources/SafeZone.prefab`.
Singleplayer pause freezes the simulation; multiplayer menus do not pause the shared
match. Rebuild clients and the dedicated server together after updating the network prefab.

## Connection checks

- **API URL:** `https://140.136.151.88:8443`; `/health` returns `ok`, while `/` returns 404.
- **Matched game address:** It must start with `wss://` and include `/game/30080` for the static server. Recreate `avian-matchmaker` after editing `.env`.
- **Old client/server builds:** Rebuild and deploy both after changing transport or network prefabs. Updating only `.env` cannot convert an old UDP binary to WebSockets.
- **WSS upgrade fails:** Check `docker logs avian_caddy`, the Kind game Pod, and TCP service endpoints. A 502 indicates that Caddy cannot reach a ready WebSocket server. Certificate errors require fixing the certificate route/trust, not disabling validation.
- **StartClient=True:** This starts an attempt. Look for transport connected, server accepted, and local player spawned. The client logs progress and times out after 30 seconds.
- **Public access:** HTTPS/WSS need TCP 8443 from the other computer. Port 80 is for certificate validation. Public UDP 30080 is not used by this deployment.

The built Windows client can run an opt-in connection check without UI interaction:

```powershell
.\Builds\WindowsClient\Avian.exe -batchmode -nographics -logFile connection-check.log --avian-smoke-endpoint wss://140.136.151.88:8443/game/30080
```

`[ConnectionSmoke] PASS` means the actual Unity client completed WSS/TLS, Netcode joining, and local player spawning, and retained an enabled character sprite. It exits after holding the connection for 30 seconds; two checks can run simultaneously. Optional arguments include `--avian-smoke-seconds 60`, `--avian-smoke-frame-rate 5`, and `--avian-smoke-character bird_02` for longer sessions, slow frame processing, and a different character.
