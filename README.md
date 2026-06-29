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

* **Docker Compose** is the recommended path for local auth testing.
* **Kubernetes / Kind** is the more future-facing path, but it is more complex to set up locally.

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
```

**3. Start the services**

From the project root:

```powershell
docker compose --env-file backend/.env -f backend/docker-compose.yaml up -d --build
```

This starts:

* `postgres_db` on `localhost:5432`
* `avian_lobby` on the internal Docker network
* `avian_ngrok` exposing a public HTTPS tunnel to the lobby

**4. Verify the API**

Open `localhost:4040` on the browser to get the public HTTPS URL. Then test:

```powershell
curl.exe https://your-ngrok-url.ngrok.app/health
```

**5. Point Unity to the local API**

Set `URL.cs` to:

```text
BaseUrl = https://your-ngrok-url.ngrok.app
```

After that, login and register should work against the backend through ngrok.

### Kubernetes / Kind

Use this path if you want to test the deployment model used by the larger backend architecture.

Open PowerShell **as Administrator** and install Kind and Kubectl if needed:
```powershell
# Install Chocolatey (if you don't have it)
Set-ExecutionPolicy Bypass -Scope Process -Force; [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; iex ((New-Object System.Net.WebClient).DownloadString('https://chocolatey.org/install.ps1'))

# Install Kind and Kubectl
choco install kind kubernetes-cli -y
```

**1. Configure local environment**

Copy `backend/.env.example` to `backend/.env` and fill in your local values.

The current K8s manifest expects the database bridge hostname `postgres-service`.

**2. Build the backend images**

Build the isolated Docker images for the lobby and game room from their respective subdirectories:

```powershell
docker build -t avian-lobby:v1 ./backend/lobby
docker build -t avian-world:v1 ./backend/world
```

**3. Create the cluster and load images**

In the project root, generate a local secret file from your env, create the Kind cluster, and inject your images:
```powershell
# Generate secret.yaml from your local .env
kubectl create secret generic avian-db-secret --from-env-file=backend/.env --dry-run=client -o yaml > backend/secret.yaml

# Create the cluster using the custom config
kind create cluster --config backend/kind-config.yaml

# Link kubectl to the new cluster
kind export kubeconfig --name avian-cluster

# Load your locally built images into Kind's internal cache
kind load docker-image avian-lobby:v1 --name avian-cluster
kind load docker-image avian-world:v1 --name avian-cluster
```

**4. Deploy secrets and workloads**

Deploy the secret and the game application components to Kubernetes:
```powershell
# Apply the generated secret
kubectl apply -f backend/secret.yaml

# Deploy the external database bridge, lobby manager, and room pool
kubectl apply -f backend/avian-cluster.yaml
```

**5. Verify the deployment**

Verify that all backend components are healthy and properly interconnected:
```powershell
# Check if Kubernetes components are running
kubectl get pods

# Check network services
kubectl get svc
```

If you test Unity against this path, the `BackendAuthClient` base URL should be:

```text
http://localhost:30080
```

The K8s path is still HTTP unless you add a separate HTTPS ingress or proxy in front of the NodePort service.

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

Whenever you modify Python code in the `lobby/` or `world/` folders, use one of these update paths:

**Docker Compose backend**

```powershell
docker compose --env-file backend/.env -f backend/docker-compose.yaml up -d --build
```

**Kubernetes / Kind backend**

```powershell
docker build -t avian-lobby:v1 ./backend/lobby
docker build -t avian-world:v1 ./backend/world
kind load docker-image avian-lobby:v1 --name avian-cluster
kind load docker-image avian-world:v1 --name avian-cluster
kubectl rollout restart deployment avian-lobby-deployment
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
