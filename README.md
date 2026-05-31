# Avian

## Prerequisites

* **Unity Version:** `6000.3.15f1 LTS` (Please check `ProjectSettings/ProjectVersion.txt` for the exact version).
*   **Git LFS:** Ensure you have [Git Large File Storage](https://git-lfs.github.com/) installed to handle high-quality assets.

## Getting Started

### Unity

**Clone the Repository:**
1. git clone [https://github.com/10jackyhu24/Avian.git](https://github.com/10jackyhu24/Avian.git)

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

To run the game backend locally, we use **Docker** and **Kind** (Kubernetes IN Docker).

**Step 1: Install Required Tools**

Open PowerShell **as Administrator** and run the following to install Chocolatey, Kind, and Kubectl:
```powershell
# Install Chocolatey (if you don't have it)
Set-ExecutionPolicy Bypass -Scope Process -Force; [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; iex ((New-Object System.Net.WebClient).DownloadString('https://chocolatey.org/install.ps1'))

# Install Kind and Kubectl
choco install kind kubernetes-cli -y
```

**Step 2: Create the Local Cluster Ensure Docker Desktop is opened and running.**

Open a normal PowerShell terminal in the project folder:

```powershell
# Create the cluster using our custom config
kind create cluster --config backend/kind-config.yaml

# Link kubectl to the new cluster
kind export kubeconfig --name avian-cluster
```

**Step 3: Deploy the Services Deploy the database and game servers:**

Before deploying, you need to set up your database credentials:

* Rename or copy the file `./backend/secret.yaml.example` to `./backend/secret.yaml`.

* Open `./backend/secret.yaml` in your editor and change the placeholder values (username, password, db name) to your own.

Once configured, deploy the database and game servers:

```powershell
kubectl apply -f backend/secret.yaml
kubectl apply -f backend/avian-cluster.yaml
```

**Step 4: Verify the Deployment** 

Because Kubernetes runs inside a single Docker container, you won't see the individual database or game servers in the Docker Desktop UI. Use the following `kubectl` commands to check their status:
```powershell
# Check if servers are running (Wait for the STATUS to become 'Running')
kubectl get pods

# Check network and ports (Ensure lobby-service is mapped to 30080)
kubectl get svc

# Check console logs for a specific server (Replace with your actual pod name from 'get pods')
kubectl logs <pod-name>
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

### K8s Issue
1. **`kubectl` Error: Connection Refused (TCP localhost:8080)**

    * **Cause**: `kubectl` lost connection to the Kind cluster.
    * **Solution**: Run `kind export kubeconfig --name avian-cluster` to reconnect.

2. **Cluster Creation Timeout or "cgroups disabled" Error**

    * **Cause**: WSL/Docker memory is full, or there's a compatibility issue with the newest Kubernetes version on Windows.
    * **Solution A (Use Stable Version)**: Delete the broken cluster and force a stable version:
    ```powershell
    kind delete cluster --all
    kind create cluster --config backend/kind-config.yaml --name avian-cluster --image kindest/node:v1.30.0
    ```
    * **Solution B (Restart WSL)**: Open PowerShell and run `wsl --shutdown`, then restart Docker Desktop.