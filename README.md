# Avian

## Prerequisites

* **Unity Version:** `6000.3.15f1 LTS` (Please check `ProjectSettings/ProjectVersion.txt` for the exact version).
*   **Git LFS:** Ensure you have [Git Large File Storage](https://git-lfs.github.com/) installed to handle high-quality assets.

## Getting Started

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

## Common Issues & Troubleshooting

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
