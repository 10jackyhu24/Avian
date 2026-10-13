# Avian

## Prerequisites

* **Unity Version:** `6000.3.15f1 LTS` (Please check `ProjectSettings/ProjectVersion.txt` for the exact version).
* **Git LFS:** Ensure you have [Git Large File Storage](https://git-lfs.github.com/) installed to handle high-quality assets.

## Getting Started

### Unity

**Clone the Repository:**
1. git clone https://github.com/10jackyhu24/Avian.git

2. **Add to Unity Hub:**
    * Open Unity Hub.
    * Click **Add -> Add project from disk**.
    * Select the cloned folder.

3. **Select Correct Version:**
    * Ensure the version dropdown in Unity Hub is set to 6000.3.15f1.

4. **Add the following to your `.git/config` file:**
    ```
    [merge]
    tool = unityyamlmerge

    [mergetool "unityyamlmerge"]
    trustExitCode = false
    cmd = '<path to UnityYAMLMerge>' merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"
    ```
    The location of unityYAMLMerge.exe may be `"C:/Program Files/Unity/Hub/Editor/6000.3.15f1/Editor/Data/Tools/UnityYAMLMerge.exe"`

### Backend Setup

There are two backend paths:

* **Docker Compose** runs auth, ngrok exposure, database, and matchmaker.
* **Kubernetes / Kind** now keeps only the world/game-room pool.

### Current Tested Multiplayer Flow

The current local multiplayer test flow is:

1. Docker Compose runs the account/login backend, matchmaker, Postgres database, Caddy, and optional ngrok tunnel.
2. Kind/Kubernetes runs the Unity dedicated server as a game-room pod.
3. Unity clients call the backend to log in and join matchmaking.
4. When two players are matched, the matchmaker returns:

```json
{
  "status": "matched",
  "room_id": "room-1",
  "seed": 100000,
  "world_endpoint": "127.0.0.1:30080"
}
```

5. Unity reads `world_endpoint` from the matchmaking response and connects to the Unity dedicated server.

For local testing, the important ports are:

* `localhost:8000` for Unity HTTP requests to auth and matchmaking through Caddy.
* `127.0.0.1:30080` for Unity Netcode UDP traffic to the Kubernetes game-room server.

For public two-computer testing, the important public addresses are:

* an **ngrok HTTPS URL** for Unity HTTP requests to auth and matchmaking.
* a **Playit UDP address** for Unity Netcode traffic to the dedicated server.

Example public flow:

```text
Computer B Unity client
-> ngrok HTTPS backend URL
-> Docker Compose matchmaker on Computer A
-> matchmaker returns Playit world_endpoint
-> Playit UDP tunnel
-> Kind/K8s Unity dedicated server on Computer A
```

### Host vs Player Machine

The host machine is the computer running the backend and dedicated server. A player machine only needs the client build.

**Host machine needs:**

* Docker Desktop.
* Docker Compose backend services.
* Kind/Kubernetes game server.
* Unity Linux Dedicated Server build.
* Docker image `avian-dedicated-server:v1`.
* Playit agent open for UDP game traffic.
* ngrok tunnel open for public HTTP backend traffic, if testing across different networks.
* `backend/.env` configured with the room mode and endpoint values.

**Player machine needs:**

* Windows Unity client `.exe`.
* Internet connection.
* The Windows client needs `network-config.json` beside its `.exe` with a reachable API URL.

The player machine does **not** need Docker, Kubernetes, Playit, ngrok, `backend/.env`, or a Linux dedicated server build.

### Docker Compose + ngrok

Use this when you want the quickest working login/register flow and a public HTTPS URL for Unity.

**1. Install prerequisites**

Make sure Docker Desktop is installed and fully running.

**2. Configure local environment**

Copy `backend/.env.example` to `backend/.env`, then fill in your local values. Example:

```env
DB_USER=avian_user
DB_PASSWORD=avian_password
DB_NAME=avian_db
DB_HOST=postgres-service
DB_PORT=5432
PASSWORD_HASH_ITERATIONS=210000
```

`backend/.env` is ignored by Git and should remain local.

If another app on the host uses one of Avian's published TCP ports, set
`AVIAN_HTTP_PORT`, `AVIAN_MATCHMAKER_PORT`, `AVIAN_NGROK_INSPECTOR_PORT`, or
`AVIAN_POSTGRES_PORT` in `backend/.env` to free host ports. The container ports
and ngrok's route to `avian-caddy:8000` stay the same. For example, if a file
converter uses host port 4040, set `AVIAN_NGROK_INSPECTOR_PORT=4041`.

Then set your [ngrok authtoken](https://ngrok.com/) in the same file or export it before starting Docker:

```env
NGROK_AUTHTOKEN=your_ngrok_token
ROOM_MODE=static
WORLD_ENDPOINT=127.0.0.1:30080
```

For public Playit testing, replace `WORLD_ENDPOINT` with your Playit UDP address:

```env
WORLD_ENDPOINT=your-playit-address.ply.gg:12345
```

Do not include `http://` or `https://` in `WORLD_ENDPOINT`. It is a UDP game endpoint, not an HTTP URL.

**3. Start the services**

From the project root:

```powershell
docker compose --env-file backend/.env -f backend/docker-compose.yaml up -d --build
```

This starts:

* `postgres_db` on `localhost:5432`
* `avian_lobby` on the internal Docker network
* `avian_ngrok` exposing a public HTTPS tunnel to the lobby
* `avian_matchmaker` on `localhost:8080`

`WORLD_ENDPOINT` is used by static room mode. Dynamic k8s rooms use `ROOM_ENDPOINT_HOST` plus the allocated NodePort instead.

For local static testing, use:

```env
ROOM_MODE=static
WORLD_ENDPOINT=127.0.0.1:30080
```

For local dynamic k8s room testing, use:

```env
ROOM_MODE=k8s
ROOM_ENDPOINT_HOST=127.0.0.1
ROOM_NODE_PORT_START=30081
ROOM_NODE_PORT_END=30084
```

**4. Verify the API**

Open `localhost:4040` on the browser to get the public HTTPS URL. Then test:

```powershell
curl.exe https://your-ngrok-url.ngrok.app/health
```

**5. Configure the Windows client**

For local testing in the Unity Editor, no file is needed: the API defaults to
`http://localhost:8000`. For a Windows build, put `network-config.json` beside
the game's `.exe` (next to its `_Data` folder):

```json
{
  "apiBaseUrl": "https://your-ngrok-domain.ngrok-free.app"
}
```

Copy `network-config.example.json` and replace the URL with the Avian tunnel's
HTTPS address from `http://localhost:4040` (or the configured inspector port).
The client uses the matchmaker's
`world_endpoint` for game UDP traffic; the config file only contains the HTTP
API address. `URL.WorldServer` is empty by default so a missing match endpoint
cannot silently connect to the player's own computer.

After configuring the public UDP endpoint on the host, you can write the
client file directly into an existing Windows build folder:

```powershell
python export_client_config.py --output "Builds/WindowsClient/network-config.json"
```

This checks that port 4040 reports the **Avian** tunnel to `avian-caddy:8000`
and that the matchmaker is no longer advertising a loopback game address.
If you changed the host ports, add `--ngrok-port 4041` and/or
`--matchmaker-port <port>` to the command.

Changing `backend/.env` does not require rebuilding the Unity client. Restart the matchmaker instead:

```powershell
docker compose --env-file backend/.env -f backend/docker-compose.yaml up -d --no-deps --force-recreate avian-matchmaker
```

Changing the API URL only requires editing `network-config.json`; the same
Windows build can be copied to another player computer. If the ngrok URL
changes when the host moves, update this file in the copied build.

To move the backend to a different host computer, create its own
`backend/.env` from `.env.example`, set its ngrok token and reachable UDP
`WORLD_ENDPOINT` (or `ROOM_ENDPOINT_HOST` for dynamic rooms), build the Linux
dedicated server there, and start Compose and Kind. The Unity `Builds` folder
and `backend/.env` are ignored by Git, so they do not come with a code clone.
Run `export_client_config.py` on the new host and copy the resulting
`network-config.json` beside each Windows client `.exe`. The C# source and
Windows client binary do not need host-specific edits.

### Kubernetes / Kind + Unity Dedicated Server

Use this path if you want to test the world/game-room deployment model with a Unity dedicated server running inside Kubernetes.

Current tested flow:

* Docker Compose still runs auth, lobby, matchmaking, Postgres, Caddy, and ngrok.
* Kind/Kubernetes runs one Unity game-room server per match.
* Each match gets its own Kubernetes Job, Service, seed, and NodePort.
* Unity clients authenticate and match through the backend, then connect to their assigned room endpoint.

Open PowerShell **as Administrator** and install Kind and Kubectl if needed:
```powershell
# Install Chocolatey (if you don't have it)
Set-ExecutionPolicy Bypass -Scope Process -Force; [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; iex ((New-Object System.Net.WebClient).DownloadString('https://chocolatey.org/install.ps1'))

# Install Kind and Kubectl
choco install kind kubernetes-cli -y
```

**1. Build the Unity Linux Dedicated Server**

In Unity, open **File > Build Profiles**.

For the Linux dedicated server build:

* Use the `Linux Dedicated Server` profile.
* Make sure `Assets/Scenes/GameRoon_Server.unity` is included.
* Make sure `GameRoon_Server` is the first scene for the server build.
* Build to:

```text
Builds/LinuxServer
```

The output should include files like:

```text
Builds/LinuxServer/AvianServer.x86_64
Builds/LinuxServer/AvianServer_Data/
Builds/LinuxServer/UnityPlayer.so
```

**2. Build the Docker image**

From the project root:

```powershell
docker build -t avian-dedicated-server:v1 -f backend/Linux_Dedicated_Server/Dockerfile Builds/LinuxServer
```

Optional local Docker smoke test:

```powershell
docker run --rm -p 7777:7777/udp -e GAME_PORT=7777 -e MAP_SEED=100000 avian-dedicated-server:v1
```

If this works, the server logs should show:

```text
[ServerBootstrap] Loaded room seed=100000
[ServerBootstrap] Listening on 0.0.0.0:7777
[ServerBootstrap] StartServer=True
```

**3. Create the cluster and load the image**

In the project root, create the Kind cluster and inject your image:

```powershell
kind delete cluster --name avian-cluster

kind create cluster --config backend/kind-config.yaml

kind export kubeconfig --name avian-cluster

kind load docker-image avian-dedicated-server:v1 --name avian-cluster
```

**4. Configure dynamic room allocation**

Set the matchmaker to create one Kubernetes room server for each match:

```env
ROOM_MODE=k8s
ROOM_ENDPOINT_HOST=127.0.0.1
ROOM_NODE_PORT_START=30081
ROOM_NODE_PORT_END=30084
ROOM_IMAGE=avian-dedicated-server:v1
ROOM_NAMESPACE=default
```

For local testing, `ROOM_ENDPOINT_HOST=127.0.0.1` is correct. For a public host later, replace it with the IP or host name that player computers can reach.

Restart the matchmaker after changing these values:

```powershell
docker compose --env-file backend/.env -f backend/docker-compose.yaml up -d --build avian-matchmaker
```

**5. Verify the matchmaker config**

```powershell
Invoke-RestMethod http://127.0.0.1:8080/match/diagnostics | ConvertTo-Json -Depth 10
```

Expected local k8s signs:

```text
room_mode: k8s
room_endpoint_host: 127.0.0.1
room_endpoint_example: 127.0.0.1:30081
allocated_node_ports: []
```

**6. Build and test the Windows client**

For the Windows client build:

* Make sure `Assets/Scenes/SampleScene.unity` is the first scene.
* Keep `Assets/Scenes/GameRoon_Server.unity` included in the build list so Netcode scene synchronization can load it.
* Keep `URL.WorldServer` empty unless you intentionally need a local fallback override.
* Confirm the matchmaker returns a room endpoint through `/match/rooms`:

```text
room-1 -> 127.0.0.1:30081
room-2 -> 127.0.0.1:30082
```

Then start two Windows clients, log in with different accounts, and press **Match** on both clients.

Successful k8s test signs:

* Both clients enter the same server scene.
* Both clients can see each other.
* Movement and jumping sync between clients.
* Kubernetes shows one room pod and service per match.
* Kubernetes logs show client connections, for example:

```text
[ServerBootstrap] room=room-1 Client connected: 1
[ServerBootstrap] room=room-1 Client connected: 2
```

### Public Two-Computer Test

Use this when two players are on different internet connections.

The backend HTTP API and the UDP game-room traffic are separate:

* `URL.BaseUrl` points to the public backend HTTP address, usually ngrok.
* `ROOM_ENDPOINT_HOST` points to the public UDP room host.
* Each k8s room uses a different UDP NodePort, such as `30081`, `30082`, `30083`.

For multiple k8s rooms across different networks, the player computers must be able to reach the host on every configured room port.

**Computer A: host**

1. Start Docker Compose:

```powershell
docker compose --env-file backend/.env -f backend/docker-compose.yaml up -d --build
```

2. Make sure the Kind cluster and room image are ready:

```powershell
kubectl cluster-info
kind load docker-image avian-dedicated-server:v1 --name avian-cluster
```

3. Configure the public room host.

For a public VPS, cloud machine, or router port-forwarding setup:

```env
ROOM_MODE=k8s
ROOM_ENDPOINT_HOST=your-public-ip-or-hostname
ROOM_NODE_PORT_START=30081
ROOM_NODE_PORT_END=30084
```

Then allow/forward UDP ports `30081` through `30084` to the host machine.

For Playit-style tunnels, one tunnel usually maps to one local UDP port. That works for one room, but multiple simultaneous k8s rooms need one public tunnel per NodePort, or a later endpoint-mapping feature.

4. Restart the matchmaker:

```powershell
docker compose --env-file backend/.env -f backend/docker-compose.yaml up -d --build avian-matchmaker
```

5. Check the endpoint preview:

```powershell
Invoke-RestMethod http://127.0.0.1:8080/match/diagnostics | ConvertTo-Json -Depth 10
```

Expected:

```text
room_mode: k8s
room_endpoint_example: your-public-ip-or-hostname:30081
```

6. Open the ngrok inspector and copy the HTTPS forwarding URL:

```text
http://localhost:4040
```

Example:

```text
https://your-ngrok-url.ngrok-free.app
```

7. Put `network-config.json` beside the Windows client `.exe`, with
   `apiBaseUrl` set to the Avian ngrok HTTPS URL. You can use
   `export_client_config.py` after the matchmaker advertises a public UDP
   address.

8. Send the Windows build together with `network-config.json` to Computer B.
   When the host's public HTTP URL changes, edit that file on each player
   computer; recompiling the client is not required.

**Computer B: player**

1. Run the Windows client `.exe`.
2. Log in or register through the ngrok backend.
3. Press **Match** with a different account from Computer A.

Expected result:

* Computer A and Computer B can log in through the same backend.
* Both clients are matched by the same matchmaker.
* Both clients connect through the Playit UDP endpoint.
* Both clients enter the same Unity dedicated server room and can see each other.

Known current limitations:

* Two clients using the same account should not be used for matchmaking. The matchmaker keys queue entries by `user_id`, so use different accounts.
* Public tunnel tests may lag because traffic is routed through tunneling services, Docker, and Kind.
* Local `127.0.0.1` room endpoints only work on the host machine. Other computers need a reachable public `ROOM_ENDPOINT_HOST`.

### Authentication Flow

The lobby backend exposes a simple account system for Unity:

* `POST /auth/register`
  * Creates a new user account.
  * Stores only a salted password hash in PostgreSQL.
  * Returns a `session_token` after success.

* `POST /auth/login`
  * Verifies the username and password.
  * Returns a new `session_token` after success.

* `GET /auth/validate?token=...`
  * Confirms whether a session token is still valid.

* `GET /health`
  * Lightweight health check for local debugging.

Recommended Unity flow:

1. Show a login panel first.
2. Ask the player for username and password.
3. Call `/auth/login`.
4. If login succeeds, store the returned `session_token` in memory for the current session.
5. If login fails, show the error and keep the login panel open.
6. Only continue into the main menu or multiplayer flow after authentication succeeds.

Notes about the current Unity auth implementation:

* `BackendAuthClient` can optionally persist the session token in `PlayerPrefs`.
* If `Persist Session Token` is enabled, clicking `Multiplayer` can validate a locally stored token before entering the multiplayer flow.
* For repeated auth testing, you can disable `Persist Session Token` in the Unity Inspector.

Why this design:

* Passwords are never stored as plain text.
* A token lets Unity stay logged in after the first request without sending the password repeatedly.
* Keeping auth inside `lobby` matches the intended architecture: lobby owns player identity, room servers focus on gameplay.
* The database stays authoritative, which makes future features like bans, profiles, and saved progress much easier.

### How to Update the Backend Deployment

Whenever you modify Python backend code or the Unity dedicated server, use the matching update path:

**Docker Compose backend**

```powershell
docker compose --env-file backend/.env -f backend/docker-compose.yaml up -d --build
```

If you change `WORLD_ENDPOINT`, restart Docker Compose so the matchmaker gets the new value:

```powershell
docker compose --env-file backend/.env -f backend/docker-compose.yaml up -d --build avian-matchmaker
```

**Kubernetes / Kind Unity dedicated server**

First rebuild the Linux Dedicated Server from Unity into:

```text
Builds/LinuxServer
```

Then rebuild and reload the Docker image:

```powershell
docker build -t avian-dedicated-server:v1 -f backend/Linux_Dedicated_Server/Dockerfile Builds/LinuxServer
kind load docker-image avian-dedicated-server:v1 --name avian-cluster
kubectl rollout restart deployment avian-rooms-pool-deployment
kubectl get pods -w
```

## Quick Start

If you don't want to type commands line by line, you can execute the following instructions:

```python
# Docker compose only
python compose.py --docker

# K8s only
python compose.py --k8s

# Docker compose + K8s
python compose.py --all
```

## Common Issues & Troubleshooting

### Unity Issues

1. **Missing Scenes or Scripts (Version Conflict)**

    If the scene appears empty or objects show "Missing Script," you might be using an incompatible Unity version (e.g., Unity 6).

    * **Solution:** Close Unity, delete the `/Library` folder in your project directory, and re-open the project specifically with Unity 6000.3.15f1.

2. **Script Compilation Errors (Naming Conflict)**

    You may see an error: `Script 'Terrain' has the same name as built-in Unity component.`

    * **Cause:** A custom script is named `Terrain.cs`, which conflicts with Unity's internal system.

    * **Solution:** No need to do anything or rename your `Terrain.cs` file and its class name to something unique (e.g., `MyTerrain.cs`).

3. **Burst Compilation Error (Error 4551)**

    You may see an error: `Unexpected error in Burst compilation... Unable to load the unmanaged library... error code 4551`.

    * **Cause:** Unity Burst cache signature verification failed or files are corrupted (often happens during version switching).

    * **Solution:**
        1. Close Unity Editor.
        2. Go to your project folder and delete the directory: `Library/BurstCache`.
        3. Restart Unity. The cache will be rebuilt automatically.

4. **Code Editor Only Opens Single File (Missing Project Context)**

    If your text editor (e.g., VS Code) only opens the double-clicked C# file instead of the whole project folder (causing autocomplete/IntelliSense to fail):

    * **Solution:** Go to Unity **Preferences > External Tools** and change **External Script Editor Args** from `$(File)` to `$(ProjectPath) $(File)`.

5. **Missing 'Unity.Netcode' Namespace / IDE0005 Reference Error**

    If your code editor shows errors like `The type or namespace name 'Netcode' does not exist` or `Using directive is unnecessary` on `using Unity.Netcode;`.

    * **Cause:** The IDE project files (`.csproj`) are out of sync with Unity's Package Manager.
    * **Solution:**
        1. In Unity Editor, go to **Edit > Preferences > External Tools**.
        2. Under **Generate .csproj files for:**, ensure both **Registry packages** and **Git packages** are checked.
        3. Click the **Regenerate project files** button.

### K8s Issue

1. **Cluster Creation Timeout or "cgroups disabled" Error**

    * **Cause:** WSL/Docker memory is full, or there's a compatibility issue with the newest Kubernetes version on Windows.
    * **Solution (Update WSL):**
        1. Close Docker Desktop completely. (Make sure to click the up-arrow in the Windows system tray at the bottom right, right-click the Docker icon, and select **Quit Docker Desktop**).
        2. Open PowerShell **as Administrator** and run:
           ```powershell
           wsl --update
           wsl --shutdown
           ```
        3. Reopen Docker Desktop and wait for it to fully start.
