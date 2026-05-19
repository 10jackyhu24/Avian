using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainController : MonoBehaviour {
    [Header("Basic settings")]
    [SerializeField] private GameObject cubePrefab; // Drag and drop a Cube Prefab here
    [SerializeField] private int size = 1000;
    [SerializeField] private float scale = 0.05f;   // Zoom scale
    [SerializeField] private float heightMultiplier = 10f; // Mountain height multiplier (for 3D mode)

    [Header("2D Map settings")]
    [SerializeField] private bool is2DMap = true;           // 2D map mode
    [SerializeField] private float wallHeight = 1.5f;       // Wall height (not too high)
    [SerializeField] private float waterDepth = 0.2f;       // Water depth (recessed height, 0 means same as ground)

    [Header("Dynamic Loading Settings")]
    [SerializeField] private bool enableDynamicLoading = true;  // Whether to enable dynamic loading (only display blocks within view)
    [SerializeField] private int viewDistance = 20;             // View distance (in grid units, radius)
    
    private Transform mainCameraTransform;    // Main camera Transform
    private GameObject[,] cubeGrid;           // Grid array to store all instantiated cube references
    private Vector3Int lastCameraGridPos = new Vector3Int(-9999, -9999, -9999);
    private int currentSeed;                  // Seed of the currently generated world
    private float actualScale;                // Actual Perlin noise scale after applying area multiplier
    private GameObject terrainParent;         // Parent GameObject to keep hierarchy organized
    private HashSet<Vector2Int> activePositions = new HashSet<Vector2Int>(); // Active coordinate positions


    [Header("Biome Settings")]
    [Tooltip("Area multiplier for biomes. 10 means area is 10 times larger (side length about 3.16 times)")]
    [SerializeField] private float biomeAreaMultiplier = 10f;

    [Header("Noise settings")]
    [SerializeField] private float offsetX = 0f;
    [SerializeField] private float offsetY = 0f;

    // Color definitions
    private Color deepOcean = new Color(0f, 0.2f, 0.4f);      // Deep blue
    private Color shallowWater = new Color(0.2f, 0.4f, 1f);   // Shallow blue
    private Color desert = new Color(0.9f, 0.8f, 0.5f);       // Desert yellow
    private Color grassland = new Color(0.3f, 1f, 0.3f);      // Grass green
    private Color rainforest = new Color(0f, 0.5f, 0f);       // Rainforest green
    private Color mountain = new Color(0.4f, 0.2f, 0f);       // Mountain brown
    private Color snowPeak = Color.white;                     // Snow peak white

    public void GenerateWorld(int seed) {
        currentSeed = seed;

        // Find main camera
        if (mainCameraTransform == null) {
            Camera mainCam = Camera.main;
            if (mainCam != null) {
                mainCameraTransform = mainCam.transform;
            }
        }

        // Find or create the "Terrain" parent GameObject
        terrainParent = GameObject.Find("Terrain");
        if (terrainParent != null) {
            // Destroy old terrain blocks
            foreach (Transform child in terrainParent.transform) {
                Destroy(child.gameObject);
            }
        } else {
            terrainParent = new GameObject("Terrain");
        }

        // Initialize grid and reset camera tracking position
        cubeGrid = new GameObject[size, size];
        activePositions.Clear();
        lastCameraGridPos = new Vector3Int(-9999, -9999, -9999);

        // Calculate the noise scale based on biome area multiplier
        actualScale = scale;
        if (biomeAreaMultiplier > 0f) {
            actualScale = scale / Mathf.Sqrt(biomeAreaMultiplier);
        }

        if (enableDynamicLoading) {
            // Under dynamic loading, do not generate all blocks at start.
            // Instead, generate blocks around the camera's current position immediately.
            if (mainCameraTransform != null) {
                int camX = Mathf.RoundToInt(mainCameraTransform.position.x);
                int camZ = Mathf.RoundToInt(mainCameraTransform.position.z);
                lastCameraGridPos = new Vector3Int(camX, 0, camZ);
                UpdateBlocksAroundCamera(camX, camZ);
            } else {
                // If main camera is not found, default to center of the map
                int centerX = size / 2;
                int centerZ = size / 2;
                lastCameraGridPos = new Vector3Int(centerX, 0, centerZ);
                UpdateBlocksAroundCamera(centerX, centerZ);
            }
        } else {
            // If dynamic loading is disabled, generate the entire world grid immediately
            for (int x = 0; x < size; x++) {
                for (int z = 0; z < size; z++) {
                    CreateBlockAt(x, z);
                }
            }
        }
    }

    void Update() {
        if (!enableDynamicLoading || cubeGrid == null) {
            return;
        }

        // Auto get camera transform if null
        if (mainCameraTransform == null) {
            Camera mainCam = Camera.main;
            if (mainCam != null) {
                mainCameraTransform = mainCam.transform;
            }
            return;
        }

        // Get camera's current grid position
        Vector3Int currentCamGridPos = new Vector3Int(
            Mathf.RoundToInt(mainCameraTransform.position.x),
            0,
            Mathf.RoundToInt(mainCameraTransform.position.z)
        );

        // Only update blocks when the camera crosses a grid boundary to optimize performance!
        if (currentCamGridPos.x != lastCameraGridPos.x || currentCamGridPos.z != lastCameraGridPos.z) {
            lastCameraGridPos = currentCamGridPos;
            UpdateBlocksAroundCamera(currentCamGridPos.x, currentCamGridPos.z);
        }
    }

    private void UpdateBlocksAroundCamera(int camX, int camZ) {
        if (cubeGrid == null) return;

        HashSet<Vector2Int> newVisiblePositions = new HashSet<Vector2Int>();

        // Calculate visible range bounded by the map size
        int startX = Mathf.Max(0, camX - viewDistance);
        int endX = Mathf.Min(size - 1, camX + viewDistance);
        int startZ = Mathf.Max(0, camZ - viewDistance);
        int endZ = Mathf.Min(size - 1, camZ + viewDistance);

        // 1. Identify all positions that should be active/generated in the view range
        for (int x = startX; x <= endX; x++) {
            for (int z = startZ; z <= endZ; z++) {
                newVisiblePositions.Add(new Vector2Int(x, z));
            }
        }

        // 2. Generate new blocks or activate existing ones
        foreach (Vector2Int pos in newVisiblePositions) {
            if (cubeGrid[pos.x, pos.y] == null) {
                CreateBlockAt(pos.x, pos.y);
            } else if (!cubeGrid[pos.x, pos.y].activeSelf) {
                cubeGrid[pos.x, pos.y].SetActive(true);
            }
        }

        // 3. Deactivate blocks that are no longer within the camera's view distance
        foreach (Vector2Int pos in activePositions) {
            if (!newVisiblePositions.Contains(pos)) {
                if (cubeGrid[pos.x, pos.y] != null && cubeGrid[pos.x, pos.y].activeSelf) {
                    cubeGrid[pos.x, pos.y].SetActive(false);
                }
            }
        }

        // 4. Track currently active positions
        activePositions = newVisiblePositions;
    }

    private void CreateBlockAt(int x, int z) {
        // Calculate height and moisture noise (simplified two-octave overlap)
        float h = CalculateNoise(x, z, currentSeed, actualScale);
        float m = CalculateNoise(x, z, currentSeed + 100, actualScale * 1.5f);

        // Determine biome and color
        Color biomeColor = GetBiomeColor(h, m);

        // Determine Y position and Y scale (compatible with 2D and 3D modes)
        float yPos = 0f;
        float yScale = 1f;

        if (is2DMap) {
            if (h > 0.65f) { // Mountains and snow peaks act as walls
                yScale = wallHeight;
                yPos = (wallHeight - 1f) / 2f;
            } else { // All other biomes (water, desert, grassland, rainforest) are flat ground
                yPos = 0f;
                yScale = 1f;
            }
        } else {
            // Original 3D terrain height fluctuation
            if (h >= 0.4f) {
                yPos = (h - 0.4f) * heightMultiplier; 
            }
        }

        // Instantiate block prefab
        Vector3 position = new Vector3(x, yPos, z);
        GameObject newCube = Instantiate(cubePrefab, position, Quaternion.identity);
        
        // Set scale (if 2D mode, scale Y to perfectly connect adjacent terrain heights)
        newCube.transform.localScale = new Vector3(1f, yScale, 1f);

        // Set parent to Terrain object to keep hierarchy clean
        if (terrainParent != null) {
            newCube.transform.parent = terrainParent.transform;
        }

        // Apply biome color
        newCube.GetComponent<Renderer>().material.color = biomeColor;

        // Store to grid map
        cubeGrid[x, z] = newCube;
    }

    // Calculate fractal noise
    float CalculateNoise(int x, int z, int currentSeed, float currentScale)
    {
        // Add offset and seed so each generation is unique
        float xCoord = (x + offsetX + currentSeed) * currentScale;
        float zCoord = (z + offsetY + currentSeed) * currentScale;

        // Unity's built-in Perlin noise returns a value between 0.0 and 1.0
        float noiseValue = Mathf.PerlinNoise(xCoord, zCoord);
        return noiseValue;
    }

    // Determine biome logic (ported from Python version)
    Color GetBiomeColor(float h, float m)
    {
        if (h < 0.4f)
        {
            return (h < 0.25f) ? deepOcean : shallowWater;
        }
        else if (h > 0.65f)
        {
            return (m > 0.55f) ? snowPeak : mountain;
        }
        else
        {
            if (m < 0.35f) return desert;
            else if (m < 0.65f) return grassland;
            else return rainforest;
        }
    }
}
