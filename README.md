# Avian

Avian is a Unity multiplayer game. The backend handles login and matchmaking over HTTP; a Unity dedicated server runs each game over UDP. You need both services for a complete multiplayer match.

## Play on an existing server

You need [Git LFS](https://git-lfs.com/), Unity **6000.3.15f1**, and a working API URL from the host.

1. Clone and open the project:

   ```powershell
   git lfs install
   git clone https://github.com/10jackyhu24/Avian.git
   cd Avian
   git lfs pull
   ```

   In Unity Hub, choose **Add project from disk** and select this folder. Open `Assets/Scenes/SampleScene.unity`.

2. Copy the client configuration and set the host's current API URL:

   ```powershell
   Copy-Item network-config.example.json network-config.json
   ```

   ```json
   { "apiBaseUrl": "https://2d6f-140-136-151-88.ngrok-free.app" }
   ```

   The Unity Editor reads `network-config.json` from the project root. A Windows build reads it from beside the game's `.exe`, next to its `_Data` folder. The example URL may change; ask the host for the current one. Do not edit `URL.cs` to change hosts.

3. Press **Play**, register or log in, and use **Match**. Two players must use different accounts. The host must also have a reachable UDP game server; a working login alone does not guarantee that a match can start.

Only have a Windows build? Put the same `network-config.json` beside its `.exe` and run it. To make a build, use **File > Build Profiles** in Unity, keep `SampleScene` first, and build to `Builds/WindowsClient`. A Git clone does not include a built client or a running server.

## Host a local match

You need Docker Desktop and Unity's Linux Dedicated Server build support. Run these commands in PowerShell from the project root.

1. Copy `backend/.env.example` to `backend/.env`:

   ```powershell
   Copy-Item backend/.env.example backend/.env
   ```

   Edit the new file. Set a database username and password, your `NGROK_AUTHTOKEN`, and these local room values:

   ```env
   ROOM_MODE=static
   STATIC_ROOM_SEED=100000
   WORLD_ENDPOINT=127.0.0.1:30080
   ```

2. Start the HTTP backend:

   ```powershell
   docker compose --env-file backend/.env -f backend/docker-compose.yaml up -d --build avian-ngrok
   curl.exe http://localhost:8000/health
   ```

   This starts ngrok, Caddy, and their backend dependencies. `/health` should return `ok`. Open `http://localhost:4040` on the host to find the current ngrok HTTPS URL. The API has no home page, so `/` returning `404` is normal.

3. In Unity, build the **Linux Dedicated Server** with `Assets/Scenes/GameRoon_Server.unity` as its first scene. Output it to `Builds/LinuxServer`; the folder must contain `AvianServer.x86_64`. Then build and start the game server:

   ```powershell
   docker build -t avian-dedicated-server:v1 -f backend/Linux_Dedicated_Server/Dockerfile Builds/LinuxServer
   docker run --rm -p 30080:7777/udp -e ROOM_ID=static-room -e MAP_SEED=100000 -e GAME_PORT=7777 avian-dedicated-server:v1
   ```

   Keep this terminal open while playing. For clients on this computer, set `apiBaseUrl` in `network-config.json` to `http://localhost:8000`, then open two clients with different accounts.

## Play across different networks

The API and the game use different addresses:

| Purpose | Client setting | Host requirement |
| --- | --- | --- |
| Login and matchmaking (HTTPS) | `apiBaseUrl`: the current ngrok HTTPS URL | Docker Compose and ngrok running |
| Game room (UDP) | Supplied by matchmaking as `world_endpoint` | A public UDP address that reaches port `30080` on the host |

Use ngrok HTTPS for login and matchmaking, and direct UDP for gameplay. Playit is not required. Set the following in **the game host's** `backend/.env`:

```env
ROOM_MODE=static
WORLD_ENDPOINT=140.136.151.88:30080
```

Allow incoming UDP `30080` on the host and forward it if the host is behind a router. The ngrok API tunnel does not require opening incoming TCP `8000`. Campus network rules may require an administrator. Editing these files does not open firewall ports. Do **not** add `http://` or `https://` to `WORLD_ENDPOINT`. Recreate the matchmaker on the host so it reads the change:

```powershell
docker compose --env-file backend/.env -f backend/docker-compose.yaml up -d --no-deps --force-recreate avian-matchmaker
curl.exe http://localhost:8080/match/diagnostics
```

Check that `static_world_endpoint` is `140.136.151.88:30080`. On another computer, open the current ngrok HTTPS URL with `/health` appended, then test a match. An API health check does not test UDP. Changing a local `.env` does not update a backend running on another computer.

Keep the public client's `apiBaseUrl` on HTTPS to protect login data in transit. ngrok HTTPS protects the API connection; it does not encrypt the separate UDP game connection. The bare URL `http://140.136.151.88/` uses port 80, which currently belongs to another service.

If Python is installed, the host can write a checked client config after both addresses are ready:

```powershell
python export_client_config.py --output "Builds/WindowsClient/network-config.json"
```

Run this on the backend host. It finds the active Avian ngrok HTTPS tunnel and rejects a local-only game endpoint. It does not test UDP. The output folder must already exist. Copy the resulting config beside each player's executable and restart the client.

If you already know the HTTPS URL, you can also pass `--api-url https://your-host.ngrok-free.app` to the export script from another computer. The default export workflow uses ngrok HTTPS; game UDP connects directly without Playit.

## Safe zone

Singleplayer and multiplayer share a server-controlled, three-stage safe zone.
The clock starts after the first player enters the world. Each seeded target circle
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

- **The IP opens the wrong site:** This project exposes Caddy on port **8000**, not the default HTTP port 80. Test `http://HOST:8000/health`, or use the ngrok HTTPS URL. `http://HOST/` may reach another service entirely.
- **The API works, but login fails in Unity:** Check the `apiBaseUrl` and placement of `network-config.json`. The file must be named exactly `network-config.json`.
- **Login works, but the room does not connect:** Check `static_world_endpoint` with `/match/diagnostics`, confirm the dedicated server is running, and verify that its UDP port is reachable. `127.0.0.1` is only valid for a client on the host machine.
- **The ngrok URL fails:** Check that Docker Compose is running and get the current URL from `http://localhost:4040` on the host.

For dynamic Kubernetes rooms, use `backend/kind-config.yaml` and set `ROOM_MODE=k8s` with a reachable `ROOM_ENDPOINT_HOST`. This is an optional deployment path; the static room above is the shortest way to run a match.
