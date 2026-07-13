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
* `backend/.env` configured with `WORLD_ENDPOINT`.

**Player machine needs:**

* Windows Unity client `.exe`.
* Internet connection.
* The client must be built with a reachable `URL.BaseUrl`.

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

Then set your [ngrok authtoken](https://ngrok.com/) in the same file or export it before starting Docker:

```env
NGROK_AUTHTOKEN=your_ngrok_token
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

`WORLD_ENDPOINT` is the game-room endpoint that matchmaking returns to Unity after two players are matched. For local Kind testing, use:

```env
WORLD_ENDPOINT=127.0.0.1:30080
```

**4. Verify the API**

Open `localhost:4040` on the browser to get the public HTTPS URL. Then test:

```powershell
curl.exe https://your-ngrok-url.ngrok.app/health
```

**5. Point Unity to the local API**

For same-computer local testing, set `URL.cs` to:

```text
BaseUrl = http://localhost:8000
```

If you need another computer to reach your backend through ngrok, set `URL.cs` to your ngrok URL instead:

```text
BaseUrl = https://your-ngrok-url.ngrok.app
```

`URL.WorldServer` should normally stay empty. The client should use `world_endpoint` from the matchmaking response.

Changing `backend/.env` does not require rebuilding the Unity client. Restart the matchmaker instead:

```powershell
docker compose --env-file backend/.env -f backend/docker-compose.yaml up -d --build avian-matchmaker
```

Changing `URL.BaseUrl` does require rebuilding the Windows client because that value is compiled into the client build.

### Kubernetes / Kind + Unity Dedicated Server

Use this path if you want to test the world/game-room deployment model with a Unity dedicated server running inside Kubernetes.

Current tested flow:

* Docker Compose still runs auth, lobby, matchmaking, Postgres, Caddy, and ngrok.
* Kind/Kubernetes runs the Unity game-room server.
* Unity clients authenticate and match through the backend, then connect to the game-room server through UDP port `30080`.

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

**4. Deploy the world workload**

```powershell
kubectl apply -f backend/avian-cluster.yaml
```

**5. Verify the deployment**

Verify that the world components are healthy:
```powershell
kubectl get pods

kubectl get svc

kubectl logs -f deployment/avian-rooms-pool-deployment
```

Expected service output should include:

```text
world-service   NodePort   ...   7777:30080/UDP
```

Expected server logs should include:

```text
[ServerBootstrap] Loaded room seed=100000
[ServerBootstrap] Listening on 0.0.0.0:7777
[ServerBootstrap] StartServer=True
```

The K8s path currently hosts one game-room pod. Lobby auth and matchmaking stay in Docker.

The deployment intentionally uses one replica:

```yaml
replicas: 1
```

For the current proof-of-concept, one replica means one Unity dedicated server room. This keeps both matched players on the same server. Do not raise this to `3` yet unless the matchmaker also knows how to assign players to specific pods/endpoints. Multiple replicas behind one UDP service can route players unpredictably for room-based gameplay.

**6. Build and test the Windows client**

For the Windows client build:

* Make sure `Assets/Scenes/SampleScene.unity` is the first scene.
* Keep `Assets/Scenes/GameRoon_Server.unity` included in the build list so Netcode scene synchronization can load it.
* Keep `URL.WorldServer` empty unless you intentionally need a local fallback override.
* Confirm the matchmaker returns this endpoint through `WORLD_ENDPOINT`:

```text
127.0.0.1:30080
```

Then start two Windows clients, log in with different accounts, and press **Match** on both clients.

Successful k8s test signs:

* Both clients enter the same server scene.
* Both clients can see each other.
* Movement and jumping sync between clients.
* Kubernetes logs show client connections, for example:

```text
[ServerBootstrap] Client connected: 1
[ServerBootstrap] Client connected: 2
```

### Public Two-Computer Test With ngrok + Playit

Use this when two players are on different internet connections.

**Computer A: host**

1. Start Docker Compose:

```powershell
docker compose --env-file backend/.env -f backend/docker-compose.yaml up -d --build
```

2. Make sure the Kind game server is running:

```powershell
kubectl get pods
kubectl get svc
```

Expected:

```text
avian-rooms-pool-deployment ... Running
world-service ... 7777:30080/UDP
```

3. Keep the Playit agent open. The tunnel should point to:

```text
127.0.0.1:30080
```

Example:

```text
your-playit-address.ply.gg:12345 -> 127.0.0.1:30080
```

4. Set `backend/.env`:

```env
WORLD_ENDPOINT=your-playit-address.ply.gg:12345
```

5. Restart the matchmaker:

```powershell
docker compose --env-file backend/.env -f backend/docker-compose.yaml up -d --build avian-matchmaker
```

6. Open the ngrok inspector and copy the HTTPS forwarding URL:

```text
http://localhost:4040
```

Example:

```text
https://your-ngrok-url.ngrok-free.app
```

7. Set `URL.cs` before building the Windows client:

```csharp
public static string BaseUrl = "https://your-ngrok-url.ngrok-free.app";
public static string WorldServer = "";
```

8. Rebuild the Windows client and send the same build to Computer B.

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
* The public tunnel test may lag because traffic is routed through ngrok, Playit, Docker, and Kind.
* The current setup exposes one room server, not multiple simultaneous room pods.

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
