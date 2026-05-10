# Avian

## Prerequisites

* **Unity Version:** `2022.3.4f1 LTS` (Please check `ProjectSettings/ProjectVersion.txt` for the exact version).
*   **Git LFS:** Ensure you have [Git Large File Storage](https://git-lfs.github.com/) installed to handle high-quality assets.

## Getting Started

**Clone the Repository:**
1. git clone [https://github.com/10jackyhu24/Avian.git](https://github.com/10jackyhu24/Avian.git)

2. **Add to Unity Hub:**
    * Open Unity Hub.
    * Click **Add -> Add project from disk**.
    * Select the cloned folder.

3. **Select Correct Version:**
    * Ensure the version dropdown in Unity Hub is set to 2022.3.4f1.
    * Do not upgrade to Unity 6 (6000) unless instructed by the team.

## Common Issues & Troubleshooting

1. **Missing Scenes or Scripts (Version Conflict)**

    If the scene appears empty or objects show "Missing Script," you might be using an incompatible Unity version (e.g., Unity 6).

    * Solution: Close Unity, delete the /Library folder in your project directory, and re-open the project specifically with Unity 2022.3.x.

2. **Script Compilation Errors (Naming Conflict)**

    You may see an error: `Script 'Terrain' has the same name as built-in Unity component.`

    * **Cause:** A custom script is named `Terrain.cs`, which conflicts with Unity's internal system.

    * **Solution:** No need to do anything or rename your `Terrain.cs` file and its class name to something unique (e.g., `MyTerrain.cs`).

3. **Layout Loading Error**

    If you see an error regarding `default-6000.dwlt` layout:

    * *Solution:* Go to the top-right corner of the Unity Editor -> **Layout** -> **Default**. This will reset the interface to match your current Unity version.