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

### K8s (Local Backend Setup)

To run the game backend locally, we adopt the industry-standard architecture: the **PostgreSQL database runs in a standalone Docker container** (simulating a cloud-managed database like AWS RDS), while the **Game Lobby and Room Pool are deployed inside Kubernetes (Kind)**.

**Step 1: Install Required Tools**

Open PowerShell **as Administrator** and run the following to install Chocolatey, Kind, and Kubectl:
```powershell
# Install Chocolatey (if you don't have it)
Set-ExecutionPolicy Bypass -Scope Process -Force; [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; iex ((New-Object System.Net.WebClient).DownloadString('https://chocolatey.org/install.ps1'))

# Install Kind and Kubectl
choco install kind kubernetes-cli -y
```

**Step 2: Start the Standalone Database**

Before starting, configure your local environment credentials:
* Copy the file `./backend/.env.example` to `./backend/.env` and fill in your custom values. Ensure `DB_HOST` is set to `postgres-service`.

Now, Ensure Docker Desktop is running, spin up the standalone PostgreSQL database outside of Kubernetes:
```powershell
docker compose -f ./backend/docker-compose.yaml up -d
```

**Step 3: Build Python Docker Images (Scheme A)**

Build the isolated Docker images for the Lobby Manager and Game Room from their respective subdirectories (No local Python installation required):
```powershell
# Build Lobby Manager Image
docker build -t avian-lobby:v1 ./backend/lobby

# Build Game Room Image
docker build -t avian-world:v1 ./backend/world
```

**Step 4: Create the K8s Cluster & Load Local Images**

In your project root folder, generate a local secret file from your env, create the Kind cluster, and inject your images:
```powershell
# 1. Generate a real secret.yaml file from your local .env for project documentation
kubectl create secret generic avian-db-secret --from-env-file=backend/.env --dry-run=client -o yaml > backend/secret.yaml

# 2. Create the cluster using our custom config
kind create cluster --config backend/kind-config.yaml

# 3. Link kubectl to the new cluster
kind export kubeconfig --name avian-cluster

# 4. [CRITICAL] Load your locally built images into Kind's internal cache so K8s can find them
kind load docker-image avian-lobby:v1 --name avian-cluster
kind load docker-image avian-world:v1 --name avian-cluster
```

**Step 5: Deploy Game Servers and Secrets**

Deploy the secret blueprint and the game application components to Kubernetes:
```powershell
# 1. Deploy the secret insurance box into Kubernetes
kubectl apply -f backend/secret.yaml

# 2. Deploy External Database Bridge, Lobby Manager and Game Rooms into Kubernetes
kubectl apply -f backend/avian-cluster.yaml
```

**Step 6: Verify the Deployment**

Verify that all backend components are healthy and properly interconnected:
```powershell
# Check if the standalone database is active
docker compose -f backend/docker-compose.yaml ps

# Check if Kubernetes components are running (Wait for STATUS to become 'Running')
kubectl get pods

# Check network services (Ensure lobby-service nodePort is successfully mapped to 30080)
kubectl get svc
```

### How to Update the Backend Deployment

Whenever you or your team members modify any Python code in the `lobby/` or `world/` folders, run this simple script to update the running servers instantly without deleting or recreating the K8s cluster:

```powershell
# 1. Rebuild the images with your new code changes
docker build -t avian-lobby:v1 ./backend/lobby
docker build -t avian-world:v1 ./backend/world

# 2. Re-inject the updated images into Kind's cache
kind load docker-image avian-lobby:v1 --name avian-cluster
kind load docker-image avian-world:v1 --name avian-cluster

# 3. Force Kubernetes to perform a rolling update using the new images
kubectl rollout restart deployment avian-lobby-deployment
kubectl rollout restart deployment avian-rooms-pool-deployment

# 4. Monitor the restart progress until all pods are 'Running'
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