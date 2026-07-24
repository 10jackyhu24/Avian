using System.Collections.Generic;
using UnityEngine;

public class TerrainController : MonoBehaviour {
    [Header("Basic Settings")]
    [SerializeField] private GameObject cubePrefab;
    [SerializeField] private int size = 1000;
    [SerializeField] private float flatHeight = 0f;
    [SerializeField] private float biomeScale = 0.03f;
    [SerializeField] private int updateStride = 4;
    [SerializeField] private int generationBiasZ = 40;
    
    [Header("Dynamic Loading")]
    [SerializeField] private int viewDistance = 50;
    [SerializeField] private float rotationThreshold = 3f;
    [SerializeField] private bool generateOnStartFromGameState = true;

    // Replace Color fields with Texture2D fields exposed to the Inspector
    [Header("Biome Textures")]
    [SerializeField] private Texture2D deepOceanTexture;
    [SerializeField] private Texture2D shallowWaterTexture;
    [SerializeField] private Texture2D desertTexture;
    [SerializeField] private Texture2D grasslandTexture;
    [SerializeField] private Texture2D rainforestTexture;
    [SerializeField] private Texture2D mountainTexture;

    private Camera mainCamera;
    private Transform mainCameraTransform;
    private GameObject[,] cubeGrid;
    private GameObject terrainParent;
    private Material[] biomeMaterials;

    // Record the camera state last time to avoid repeated calculations
    private Vector3Int lastCameraGridPos = new Vector3Int(-9999, -9999, -9999);
    private readonly HashSet<Vector2Int> lastLoadCenters = new HashSet<Vector2Int>();
    private int currentSeed;

    // Track the currently loaded and displayed coordinates
    private HashSet<Vector2Int> inRangePositions = new HashSet<Vector2Int>();

    private void Start() {
        EnsureCameraReference(false);

        if (generateOnStartFromGameState && GameState.hasWorldSeed) {
            GenerateWorld(GameState.currentSeed);
        }
    }

    private bool EnsureCameraReference(bool warnIfMissing = true) {
        if (mainCamera != null && mainCameraTransform != null) {
            return true;
        }

        mainCamera = Camera.main;
        if (mainCamera == null) {
            if (warnIfMissing) {
                Debug.LogWarning("[TerrainController] Main Camera not found. Terrain will fall back to player positions.", this);
            }

            return false;
        }

        mainCameraTransform = mainCamera.transform;
        return true;
    }

    private void EnsureBiomeMaterials() {
        if (biomeMaterials != null) return;

        if (cubePrefab == null) {
            Debug.LogError("[TerrainController] Cube Prefab is not assigned.", this);
            return;
        }

        Renderer prefabRenderer = cubePrefab.GetComponent<Renderer>();
        if (prefabRenderer == null) {
            Debug.LogError("[TerrainController] Cube Prefab is missing a Renderer.", this);
            return;
        }

        Material baseMaterial = prefabRenderer.sharedMaterial;

        biomeMaterials = new Material[6];
        biomeMaterials[0] = CreateBiomeMaterial(baseMaterial, deepOceanTexture);
        biomeMaterials[1] = CreateBiomeMaterial(baseMaterial, shallowWaterTexture);
        biomeMaterials[2] = CreateBiomeMaterial(baseMaterial, desertTexture);
        biomeMaterials[3] = CreateBiomeMaterial(baseMaterial, grasslandTexture);
        biomeMaterials[4] = CreateBiomeMaterial(baseMaterial, rainforestTexture);
        biomeMaterials[5] = CreateBiomeMaterial(baseMaterial, mountainTexture);
    }

    private Material CreateBiomeMaterial(Material baseMaterial, Texture2D texture) {
        Material material = new Material(baseMaterial);

        // Standard Built-in Pipeline property
        if (texture != null) {
            material.mainTexture = texture;
            
            // Universal Render Pipeline (URP) compatibility check
            if (material.HasProperty("_BaseMap")) {
                material.SetTexture("_BaseMap", texture);
            }
        } else {
            Debug.LogWarning("[TerrainController] A biome texture is null!", this);
        }

        return material;
    }

    public void GenerateWorld(int seed) {
        currentSeed = seed;
        EnsureCameraReference(false);
        EnsureBiomeMaterials();
        if (biomeMaterials == null) {
            return;
        }

        if (mainCameraTransform != null) {
            mainCameraTransform.position = new Vector3(0f, mainCameraTransform.position.y, 0f);
        }

        // Clean up old terrain
        terrainParent = GameObject.Find("Terrain");
        if (terrainParent == null) {
            terrainParent = new GameObject("Terrain");
        }

        foreach (Transform child in terrainParent.transform) Destroy(child.gameObject);

        // Initialize
        cubeGrid = new GameObject[size, size];
        inRangePositions.Clear();
        // Initialize lastCameraGridPos using integer grid coordinates consistent with Update()
        lastCameraGridPos = new Vector3Int(0, 0, 0);
        lastLoadCenters.Clear();

        // Trigger the first terrain load
        UpdateDynamicLoading();
        Debug.Log($"[TerrainController] Generated world with seed={seed}.", this);
    }

    private void Update() {
        if (!GameState.isPlaying) return;
        if (cubeGrid == null) return;

        HashSet<Vector2Int> loadCenters = GetLoadCenters();
        if (loadCenters.Count == 0) return;

        bool loadCentersChanged = !loadCenters.SetEquals(lastLoadCenters);

        if (loadCentersChanged) {
            UpdateDynamicLoading();
        }
    }

    private HashSet<Vector2Int> GetLoadCenters() {
        HashSet<Vector2Int> centers = new HashSet<Vector2Int>();
        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        foreach (PlayerController player in players) {
            if (!player.gameObject.activeInHierarchy) {
                continue;
            }

            centers.Add(WorldToCoarseGrid(player.transform.position, 0));
        }

        if (centers.Count > 0) {
            return centers;
        }

        if (EnsureCameraReference(false)) {
            centers.Add(WorldToCoarseGrid(mainCameraTransform.position, generationBiasZ));
        }

        return centers;
    }

    private Vector2Int WorldToCoarseGrid(Vector3 worldPosition, int zBias) {
        int gridX = Mathf.RoundToInt(worldPosition.x) + size / 2;
        int gridZ = Mathf.RoundToInt(worldPosition.z) + size / 2 + zBias;
        return new Vector2Int(gridX / updateStride, gridZ / updateStride);
    }

    private void UpdateDynamicLoading() {
        HashSet<Vector2Int> loadCenters = GetLoadCenters();
        if (loadCenters.Count == 0) {
            loadCenters.Add(new Vector2Int(size / (2 * updateStride), size / (2 * updateStride)));
        }

        HashSet<Vector2Int> newInRange = new HashSet<Vector2Int>();

        foreach (Vector2Int center in loadCenters) {
            int centerX = center.x * updateStride;
            int centerZ = center.y * updateStride;

            // Define loading boundary around each player/server authority position.
            int startX = Mathf.Max(0, centerX - viewDistance);
            int endX   = Mathf.Min(size - 1, centerX + viewDistance);
            int startZ = Mathf.Max(0, centerZ - viewDistance);
            int endZ   = Mathf.Min(size - 1, centerZ + viewDistance);

            for (int x = startX; x <= endX; x++) {
                for (int z = startZ; z <= endZ; z++) {
                    newInRange.Add(new Vector2Int(x, z));
                }
            }
        }

        foreach (Vector2Int pos in newInRange) {
            if (cubeGrid[pos.x, pos.y] == null) {
                CreateBlockAt(pos.x, pos.y);
            }
            if (!cubeGrid[pos.x, pos.y].activeSelf) {
                cubeGrid[pos.x, pos.y].SetActive(true);
            }
        }

        // Deactivate blocks that fall outside the active range.
        foreach (Vector2Int pos in inRangePositions) {
            if (!newInRange.Contains(pos) && cubeGrid[pos.x, pos.y] != null) {
                cubeGrid[pos.x, pos.y].SetActive(false);
            }
        }

        inRangePositions = newInRange;
        lastLoadCenters.Clear();
        foreach (Vector2Int center in loadCenters) {
            lastLoadCenters.Add(center);
        }
    }

    private void CreateBlockAt(int x, int z) {
        float biomeNoise = Mathf.PerlinNoise(
            (x + currentSeed) * biomeScale,
            (z + currentSeed) * biomeScale
        );
        float moistureNoise = Mathf.PerlinNoise(
            (x + currentSeed * 2) * (biomeScale * 1.5f),
            (z + currentSeed * 2) * (biomeScale * 1.5f)
        );

        Vector3 position = new Vector3(x - (size / 2), flatHeight, z - (size / 2));
        GameObject newCube = Instantiate(cubePrefab, position, Quaternion.identity);
        
        newCube.transform.parent = terrainParent.transform;
        Renderer renderer = newCube.GetComponent<Renderer>();
        renderer.sharedMaterial = GetBiomeMaterial(biomeNoise, moistureNoise);

        cubeGrid[x, z] = newCube;
    }

    private Material GetBiomeMaterial(float h, float m) {
        if (h < 0.18f) return biomeMaterials[0];
        if (h < 0.30f) return biomeMaterials[1];
        if (h < 0.48f) return biomeMaterials[2];
        if (h < 0.66f) return biomeMaterials[3];
        if (m < 0.55f) return biomeMaterials[4];
        return biomeMaterials[5];
    }
}