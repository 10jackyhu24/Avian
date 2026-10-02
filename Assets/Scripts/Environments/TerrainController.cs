using System.Collections.Generic;
using UnityEngine;

[System.Flags]
public enum TerrainBiomeMask {
    DeepOcean = 1 << 0,
    ShallowWater = 1 << 1,
    Desert = 1 << 2,
    Grassland = 1 << 3,
    Rainforest = 1 << 4,
    Mountain = 1 << 5
}

public class TerrainController : MonoBehaviour {
    private const int DesertBiomeIndex = 2;
    private const int GrasslandBiomeIndex = 3;
    private const int RainforestBiomeIndex = 4;
    private const int MountainBiomeIndex = 5;
    private const float DesertThreshold = 0.38f;
    private const float GrasslandThreshold = 0.64f;
    private const float RainforestMoistureThreshold = 0.55f;

    [System.Serializable]
    private class StructureSpawnOption {
        public string structureName = "Village";
        public GameObject prefab;
        public bool enabled = true;
        public int cellSize = 80;
        [Range(0f, 1f)]
        public float spawnChance = 0.03f;
        public float heightOffset = 0.05f;
        public int originClearRadius = 40;
        public float minimumSpacing = 40f;
        public TerrainBiomeMask allowedBiomes =
            TerrainBiomeMask.Desert |
            TerrainBiomeMask.Grassland |
            TerrainBiomeMask.Rainforest;
    }

    [Header("Basic Settings")]
    [SerializeField] private GameObject cubePrefab;
    [SerializeField] private int size = 1000;
    [SerializeField] private float flatHeight = 0f;
    [SerializeField] private float biomeScale = 0.006f;
    [SerializeField] private int updateStride = 4;
    [SerializeField] private int generationBiasZ = 40;
    
    [Header("Dynamic Loading")]
    [SerializeField] private int viewDistance = 50;
    [SerializeField] private float rotationThreshold = 3f;
    [SerializeField] private bool generateOnStartFromGameState = true;

    [Header("Structure Spawning")]
    [SerializeField] private GameObject housePrefab;
    [SerializeField] private bool spawnHouses = true;
    [SerializeField] private int structureCellSize = 24;
    [Range(0f, 1f)]
    [SerializeField] private float houseSpawnChance = 0.08f;
    [SerializeField] private float houseHeightOffset = 0.55f;
    [SerializeField] private int houseOriginClearRadius = 20;

    [Header("Structure Spawn Options")]
    [SerializeField] private StructureSpawnOption[] structureSpawnOptions;

    // Replace Color fields with Texture2D fields exposed to the Inspector
    [Header("Biome Textures")]
    [SerializeField] private Texture2D desertTexture;
    [SerializeField] private Texture2D[] desertTextureVariants;
    [SerializeField] private Texture2D grasslandTexture;
    [SerializeField] private Texture2D[] grasslandTextureVariants;
    [SerializeField] private Texture2D rainforestTexture;
    [SerializeField] private Texture2D[] rainforestTextureVariants;
    [SerializeField] private Texture2D mountainTexture;
    [SerializeField] private Texture2D[] mountainTextureVariants;

    [Header("Biome Material Variation")]
    [Min(1)]
    [SerializeField] private int materialVariationPatchSize = 3;
    [SerializeField] private bool rotateTerrainTiles = true;

    [Header("Rock Covers")]
    [SerializeField] private bool spawnRockCovers = true;
    [Min(1)]
    [SerializeField] private int rockSpawnCellSize = 4;
    [Range(0f, 1f)]
    [SerializeField] private float mountainRockSpawnChance = 0.7f;
    [Range(0f, 1f)]
    [SerializeField] private float grasslandRockSpawnChance = 0.12f;
    [Range(0f, 1f)]
    [SerializeField] private float desertRockSpawnChance = 0.12f;
    [Min(0)]
    [SerializeField] private int rockOriginClearRadius = 8;
    [SerializeField] private Vector3 rockSize = new Vector3(1f, 0.5f, 1f);
    [SerializeField] private float rockHeightOffset;

    private Camera mainCamera;
    private Transform mainCameraTransform;
    private GameObject[,] cubeGrid;
    private GameObject terrainParent;
    private GameObject structureParent;
    private Material[][] biomeMaterials;

    // Record the camera state last time to avoid repeated calculations
    private Vector3Int lastCameraGridPos = new Vector3Int(-9999, -9999, -9999);
    private readonly HashSet<Vector2Int> lastLoadCenters = new HashSet<Vector2Int>();
    private int currentSeed;

    // Track the currently loaded and displayed coordinates
    private HashSet<Vector2Int> inRangePositions = new HashSet<Vector2Int>();
    private readonly Dictionary<string, GameObject> spawnedStructuresByCell = new Dictionary<string, GameObject>();
    private readonly List<Vector3> spawnedStructurePositions = new List<Vector3>();
    private HashSet<string> activeStructureKeys = new HashSet<string>();
    public static TerrainController Instance { get; private set; }

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(this);
            return;
        }

        Instance = this;
    }
    /// <summary>
    /// 【伺服器專用】取得世界正中心的世界座標
    /// </summary>
    public Vector3 GetServerWorldCenter() {
        return new Vector3(0f, flatHeight, 0f);
    }

    /// <summary>
    /// 【伺服器專用】取得地圖的總邊長大小
    /// </summary>
    public int GetServerWorldSize() {
        return size;
    }

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

        biomeMaterials = new Material[6][];
        biomeMaterials[DesertBiomeIndex] = CreateBiomeMaterials(baseMaterial, desertTexture, desertTextureVariants);
        biomeMaterials[GrasslandBiomeIndex] = CreateBiomeMaterials(baseMaterial, grasslandTexture, grasslandTextureVariants);
        biomeMaterials[RainforestBiomeIndex] = CreateBiomeMaterials(baseMaterial, rainforestTexture, rainforestTextureVariants);
        biomeMaterials[MountainBiomeIndex] = CreateBiomeMaterials(baseMaterial, mountainTexture, mountainTextureVariants);
    }

    private Material[] CreateBiomeMaterials(Material baseMaterial, Texture2D primaryTexture, Texture2D[] textureVariants) {
        List<Texture2D> textures = new List<Texture2D>();
        AddUniqueTexture(textures, primaryTexture);

        if (textureVariants != null) {
            foreach (Texture2D texture in textureVariants) {
                AddUniqueTexture(textures, texture);
            }
        }

        if (textures.Count == 0) {
            Debug.LogWarning("[TerrainController] A biome has no assigned textures; the prefab material will be used.", this);
            return new[] { CreateBiomeMaterial(baseMaterial, null) };
        }

        Material[] materials = new Material[textures.Count];
        for (int i = 0; i < textures.Count; i++) {
            materials[i] = CreateBiomeMaterial(baseMaterial, textures[i]);
        }

        return materials;
    }

    private void AddUniqueTexture(List<Texture2D> textures, Texture2D texture) {
        if (texture != null && !textures.Contains(texture)) {
            textures.Add(texture);
        }
    }

    private Material CreateBiomeMaterial(Material baseMaterial, Texture2D texture) {
        Material material = new Material(baseMaterial);
        material.name = texture != null ? $"Terrain_{texture.name}" : "Terrain_Default";

        // Standard Built-in Pipeline property
        if (texture != null) {
            material.mainTexture = texture;
            
            // Universal Render Pipeline (URP) compatibility check
            if (material.HasProperty("_BaseMap")) {
                material.SetTexture("_BaseMap", texture);
            }
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

        structureParent = GameObject.Find("Structures");
        if (structureParent == null) {
            structureParent = new GameObject("Structures");
        }

        foreach (Transform child in structureParent.transform) Destroy(child.gameObject);

        // Initialize
        cubeGrid = new GameObject[size, size];
        inRangePositions.Clear();
        spawnedStructuresByCell.Clear();
        spawnedStructurePositions.Clear();
        activeStructureKeys.Clear();
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

    public bool IsWorldPositionLoaded(Vector3 worldPosition) {
        if (cubeGrid == null) {
            return false;
        }

        int gridX = Mathf.RoundToInt(worldPosition.x) + size / 2;
        int gridZ = Mathf.RoundToInt(worldPosition.z) + size / 2;
        if (gridX < 0 || gridX >= size || gridZ < 0 || gridZ >= size) {
            return false;
        }

        return inRangePositions.Contains(new Vector2Int(gridX, gridZ));
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

        UpdateStructureLoading(newInRange);

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
        Quaternion rotation = Quaternion.identity;
        if (rotateTerrainTiles) {
            int quarterTurns = DeterministicTerrainRange(x, z, 4001, 4);
            rotation = Quaternion.Euler(0f, quarterTurns * 90f, 0f);
        }

        GameObject newCube = Instantiate(cubePrefab, position, rotation);
        
        newCube.transform.parent = terrainParent.transform;
        Renderer renderer = newCube.GetComponent<Renderer>();
        int biomeIndex = GetBiomeIndex(biomeNoise, moistureNoise);
        renderer.sharedMaterial = GetBiomeMaterial(biomeIndex, x, z);
        TryCreateRockCover(newCube, renderer, biomeIndex, x, z);

        cubeGrid[x, z] = newCube;
    }

    private Material GetBiomeMaterial(int biomeIndex, int x, int z) {
        Material[] variants = biomeMaterials[biomeIndex];

        if (variants.Length == 1) {
            return variants[0];
        }

        int patchSize = Mathf.Max(1, materialVariationPatchSize);
        int patchX = x / patchSize;
        int patchZ = z / patchSize;
        int variantIndex = DeterministicTerrainRange(
            patchX,
            patchZ,
            3001 + biomeIndex * 101,
            variants.Length
        );
        return variants[variantIndex];
    }

    private void TryCreateRockCover(GameObject terrainBlock, Renderer terrainRenderer, int biomeIndex, int x, int z) {
        if (!ShouldSpawnRockCover(biomeIndex, x, z)) {
            return;
        }

        Vector3 safeRockSize = new Vector3(
            Mathf.Clamp(rockSize.x, 0.1f, 1f),
            Mathf.Max(0.1f, rockSize.y),
            Mathf.Clamp(rockSize.z, 0.1f, 1f)
        );
        Vector3 terrainPosition = GridToWorldPosition(x, z);
        float rockBottom = terrainRenderer.bounds.max.y + rockHeightOffset;

        GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rock.name = $"RockCover_{x}_{z}";
        rock.layer = terrainBlock.layer;
        rock.transform.position = new Vector3(
            terrainPosition.x,
            rockBottom + safeRockSize.y * 0.5f,
            terrainPosition.z
        );
        rock.transform.rotation = Quaternion.Euler(
            0f,
            DeterministicTerrainRange(x, z, 5209, 4) * 90f,
            0f
        );
        rock.transform.localScale = safeRockSize;

        Renderer rockRenderer = rock.GetComponent<Renderer>();
        rockRenderer.sharedMaterial = GetBiomeMaterial(MountainBiomeIndex, x, z);

        // Keep the rock's world-space size while making it follow terrain streaming.
        rock.transform.SetParent(terrainBlock.transform, true);
    }

    private bool ShouldSpawnRockCover(int biomeIndex, int x, int z) {
        float spawnChance = GetRockSpawnChance(biomeIndex);
        if (!spawnRockCovers || spawnChance <= 0f) {
            return false;
        }

        Vector3 worldPosition = GridToWorldPosition(x, z);
        if (Mathf.Abs(worldPosition.x) < rockOriginClearRadius &&
            Mathf.Abs(worldPosition.z) < rockOriginClearRadius) {
            return false;
        }

        int cellSize = Mathf.Max(1, rockSpawnCellSize);
        int cellX = x / cellSize;
        int cellZ = z / cellSize;
        int biomeSalt = biomeIndex * 101;
        int chanceRoll = DeterministicTerrainRange(cellX, cellZ, 5003 + biomeSalt, 10000);
        if (chanceRoll >= Mathf.RoundToInt(spawnChance * 10000f)) {
            return false;
        }

        int margin = cellSize >= 3 ? 1 : 0;
        int candidateRange = Mathf.Max(1, cellSize - margin * 2);
        int candidateX = cellX * cellSize + margin +
            DeterministicTerrainRange(cellX, cellZ, 5011 + biomeSalt, candidateRange);
        int candidateZ = cellZ * cellSize + margin +
            DeterministicTerrainRange(cellX, cellZ, 5021 + biomeSalt, candidateRange);
        return x == candidateX && z == candidateZ;
    }

    private float GetRockSpawnChance(int biomeIndex) {
        return biomeIndex switch {
            DesertBiomeIndex => desertRockSpawnChance,
            GrasslandBiomeIndex => grasslandRockSpawnChance,
            MountainBiomeIndex => mountainRockSpawnChance,
            _ => 0f
        };
    }

    private int GetBiomeIndex(float h, float m) {
        if (h < DesertThreshold) return DesertBiomeIndex;
        if (h < GrasslandThreshold) return GrasslandBiomeIndex;
        if (m >= RainforestMoistureThreshold) return RainforestBiomeIndex;
        return MountainBiomeIndex;
    }

    private void UpdateStructureLoading(HashSet<Vector2Int> loadedTerrainPositions) {
        List<StructureSpawnOption> options = GetActiveStructureOptions();
        if (options.Count == 0) {
            return;
        }

        HashSet<string> newActiveStructureKeys = new HashSet<string>();

        for (int optionIndex = 0; optionIndex < options.Count; optionIndex++) {
            StructureSpawnOption option = options[optionIndex];
            HashSet<Vector2Int> activeCellsForOption = new HashSet<Vector2Int>();

            foreach (Vector2Int terrainPosition in loadedTerrainPositions) {
                activeCellsForOption.Add(new Vector2Int(
                    terrainPosition.x / option.cellSize,
                    terrainPosition.y / option.cellSize
                ));
            }

            foreach (Vector2Int cell in activeCellsForOption) {
                string structureKey = GetStructureKey(optionIndex, cell);
                newActiveStructureKeys.Add(structureKey);

                if (!spawnedStructuresByCell.TryGetValue(structureKey, out GameObject structure)) {
                    structure = TryCreateStructureInCell(option, optionIndex, cell);
                    spawnedStructuresByCell[structureKey] = structure;
                }

                if (structure != null && !structure.activeSelf) {
                    structure.SetActive(true);
                }
            }
        }

        foreach (string structureKey in activeStructureKeys) {
            if (newActiveStructureKeys.Contains(structureKey) ||
                !spawnedStructuresByCell.TryGetValue(structureKey, out GameObject structure) ||
                structure == null) {
                continue;
            }

            structure.SetActive(false);
        }

        activeStructureKeys = newActiveStructureKeys;
    }

    private GameObject TryCreateStructureInCell(StructureSpawnOption option, int optionIndex, Vector2Int cell) {
        if (DeterministicValue01(cell.x, cell.y, GetStructureSalt(optionIndex, 13)) > option.spawnChance) {
            return null;
        }

        int minGridX = cell.x * option.cellSize;
        int minGridZ = cell.y * option.cellSize;
        int localX = DeterministicRange(cell.x, cell.y, GetStructureSalt(optionIndex, 31), 2, Mathf.Max(3, option.cellSize - 2));
        int localZ = DeterministicRange(cell.x, cell.y, GetStructureSalt(optionIndex, 47), 2, Mathf.Max(3, option.cellSize - 2));
        int gridX = Mathf.Clamp(minGridX + localX, 0, size - 1);
        int gridZ = Mathf.Clamp(minGridZ + localZ, 0, size - 1);

        Vector3 worldPosition = GridToWorldPosition(gridX, gridZ);
        if (Mathf.Abs(worldPosition.x) < option.originClearRadius &&
            Mathf.Abs(worldPosition.z) < option.originClearRadius) {
            return null;
        }

        if (IsTooCloseToExistingStructure(worldPosition, option.minimumSpacing)) {
            return null;
        }

        int biomeIndex = GetBiomeIndex(
            GetBiomeNoise(gridX, gridZ),
            GetMoistureNoise(gridX, gridZ)
        );

        if (!CanSpawnStructureInBiome(option, biomeIndex)) {
            return null;
        }

        float yRotation = DeterministicRange(cell.x, cell.y, GetStructureSalt(optionIndex, 61), 0, 4) * 90f;
        Vector3 spawnPosition = worldPosition + Vector3.up * option.heightOffset;
        GameObject structure = Instantiate(option.prefab, spawnPosition, Quaternion.Euler(0f, yRotation, 0f));
        structure.transform.parent = structureParent.transform;
        structure.name = $"{option.structureName}_{cell.x}_{cell.y}";
        spawnedStructurePositions.Add(worldPosition);
        return structure;
    }

    private Vector3 GridToWorldPosition(int x, int z) {
        return new Vector3(x - (size / 2), flatHeight, z - (size / 2));
    }

    private float GetBiomeNoise(int x, int z) {
        return Mathf.PerlinNoise(
            (x + currentSeed) * biomeScale,
            (z + currentSeed) * biomeScale
        );
    }

    private float GetMoistureNoise(int x, int z) {
        return Mathf.PerlinNoise(
            (x + currentSeed * 2) * (biomeScale * 1.5f),
            (z + currentSeed * 2) * (biomeScale * 1.5f)
        );
    }

    private bool CanSpawnStructureInBiome(StructureSpawnOption option, int biomeIndex) {
        TerrainBiomeMask biome = GetBiomeMask(biomeIndex);
        return (option.allowedBiomes & biome) != 0;
    }

    private TerrainBiomeMask GetBiomeMask(int biomeIndex) {
        return biomeIndex switch {
            0 => TerrainBiomeMask.DeepOcean,
            1 => TerrainBiomeMask.ShallowWater,
            2 => TerrainBiomeMask.Desert,
            3 => TerrainBiomeMask.Grassland,
            4 => TerrainBiomeMask.Rainforest,
            _ => TerrainBiomeMask.Mountain
        };
    }

    private List<StructureSpawnOption> GetActiveStructureOptions() {
        List<StructureSpawnOption> options = new List<StructureSpawnOption>();

        if (structureSpawnOptions != null) {
            foreach (StructureSpawnOption option in structureSpawnOptions) {
                if (IsValidStructureOption(option)) {
                    options.Add(option);
                }
            }
        }

        if (options.Count == 0 && spawnHouses && housePrefab != null && structureCellSize > 0) {
            options.Add(new StructureSpawnOption {
                structureName = "House",
                prefab = housePrefab,
                enabled = true,
                cellSize = structureCellSize,
                spawnChance = houseSpawnChance,
                heightOffset = houseHeightOffset,
                originClearRadius = houseOriginClearRadius,
                minimumSpacing = Mathf.Max(8f, structureCellSize),
                allowedBiomes = TerrainBiomeMask.Desert | TerrainBiomeMask.Grassland | TerrainBiomeMask.Rainforest
            });
        }

        return options;
    }

    private bool IsValidStructureOption(StructureSpawnOption option) {
        return option != null &&
            option.enabled &&
            option.prefab != null &&
            option.cellSize > 0 &&
            option.spawnChance > 0f;
    }

    private bool IsTooCloseToExistingStructure(Vector3 worldPosition, float minimumSpacing) {
        if (minimumSpacing <= 0f) {
            return false;
        }

        float minimumSpacingSquared = minimumSpacing * minimumSpacing;
        foreach (Vector3 existingPosition in spawnedStructurePositions) {
            Vector2 delta = new Vector2(
                worldPosition.x - existingPosition.x,
                worldPosition.z - existingPosition.z
            );

            if (delta.sqrMagnitude < minimumSpacingSquared) {
                return true;
            }
        }

        return false;
    }

    private string GetStructureKey(int optionIndex, Vector2Int cell) {
        return $"{optionIndex}:{cell.x}:{cell.y}";
    }

    private int GetStructureSalt(int optionIndex, int salt) {
        return 1009 + optionIndex * 101 + salt;
    }

    private float DeterministicValue01(int x, int z, int salt) {
        uint hash = DeterministicHash(x, z, salt);
        return (hash & 0x00FFFFFF) / (float)0x01000000;
    }

    private int DeterministicRange(int x, int z, int salt, int minInclusive, int maxExclusive) {
        if (maxExclusive <= minInclusive) {
            return minInclusive;
        }

        uint hash = DeterministicHash(x, z, salt);
        return minInclusive + (int)(hash % (uint)(maxExclusive - minInclusive));
    }

    private uint DeterministicHash(int x, int z, int salt) {
        unchecked {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)currentSeed) * 16777619u;
            hash = (hash ^ (uint)x) * 16777619u;
            hash = (hash ^ (uint)z) * 16777619u;
            hash = (hash ^ (uint)salt) * 16777619u;
            return hash;
        }
    }

    private int DeterministicTerrainRange(int x, int z, int salt, int maxExclusive) {
        if (maxExclusive <= 1) {
            return 0;
        }

        unchecked {
            uint hash = DeterministicHash(x, z, salt);
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return (int)(hash % (uint)maxExclusive);
        }
    }

    private void OnDestroy() {
        if (biomeMaterials != null) {
            foreach (Material[] variants in biomeMaterials) {
                if (variants == null) {
                    continue;
                }

                foreach (Material material in variants) {
                    if (material != null) {
                        Destroy(material);
                    }
                }
            }
        }

        if (Instance == this) {
            Instance = null;
        }
    }

}
