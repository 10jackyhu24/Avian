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

    private Camera mainCamera;
    private Transform mainCameraTransform;
    private GameObject[,] cubeGrid;
    private GameObject terrainParent;
    private Material[] biomeMaterials;

    // Record the camera state last time to avoid repeated calculations
    private Vector3Int lastCameraGridPos = new Vector3Int(-9999, -9999, -9999);
    private Quaternion lastCameraRotation = Quaternion.identity;
    private int currentSeed;

    // Track the currently loaded and displayed coordinates
    private HashSet<Vector2Int> inRangePositions = new HashSet<Vector2Int>();

    // Color definitions (fixed constants, no need to generate new Color objects every time)
    private readonly Color deepOcean = new Color(0f, 0.2f, 0.4f);
    private readonly Color shallowWater = new Color(0.2f, 0.4f, 1f);
    private readonly Color desert = new Color(0.9f, 0.8f, 0.5f);
    private readonly Color grassland = new Color(0.3f, 1f, 0.3f);
    private readonly Color rainforest = new Color(0f, 0.5f, 0f);
    private readonly Color mountain = new Color(0.4f, 0.2f, 0f);
    private readonly Color snowPeak = Color.white;

    private void Start() {
        mainCamera = Camera.main;
        mainCameraTransform = mainCamera.transform;
    }

    private void EnsureBiomeMaterials() {
        if (biomeMaterials != null) return;

        Renderer prefabRenderer = cubePrefab.GetComponent<Renderer>();
        Material baseMaterial = prefabRenderer.sharedMaterial;

        biomeMaterials = new Material[6];
        biomeMaterials[0] = CreateBiomeMaterial(baseMaterial, deepOcean);
        biomeMaterials[1] = CreateBiomeMaterial(baseMaterial, shallowWater);
        biomeMaterials[2] = CreateBiomeMaterial(baseMaterial, desert);
        biomeMaterials[3] = CreateBiomeMaterial(baseMaterial, grassland);
        biomeMaterials[4] = CreateBiomeMaterial(baseMaterial, rainforest);
        biomeMaterials[5] = CreateBiomeMaterial(baseMaterial, mountain);
    }

    private Material CreateBiomeMaterial(Material baseMaterial, Color color) {
        Material material = new Material(baseMaterial);
        material.color = color;
        return material;
    }

    public void GenerateWorld(int seed) {
        currentSeed = seed;
        EnsureBiomeMaterials();
        mainCameraTransform.position = new Vector3(0f, mainCameraTransform.position.y, 0f);

        // Clean up old terrain
        terrainParent = GameObject.Find("Terrain");
        foreach (Transform child in terrainParent.transform) Destroy(child.gameObject);

        // Initialize
        cubeGrid = new GameObject[size, size];
        inRangePositions.Clear();
        // Initialize lastCameraGridPos using integer grid coordinates consistent with Update()
        lastCameraGridPos = new Vector3Int(0, 0, 0);
        lastCameraRotation = Quaternion.identity;

        // Trigger the first terrain load
        UpdateDynamicLoading();
    }

    private void Update() {
        if (!GameState.isPlaying) return;
        // Calculate the camera's current grid coordinates
        int camGridX = Mathf.RoundToInt(mainCameraTransform.position.x) + size / 2;
        int camGridZ = Mathf.RoundToInt(mainCameraTransform.position.z) + size / 2;
        int coarseCamX = camGridX / updateStride;
        int coarseCamZ = camGridZ / updateStride;

        bool posChanged = (coarseCamX != lastCameraGridPos.x || coarseCamZ != lastCameraGridPos.z);
        bool rotChanged = Quaternion.Angle(mainCameraTransform.rotation, lastCameraRotation) > rotationThreshold;

        // Only refresh terrain when the camera crosses a larger update cell.
        if (posChanged || rotChanged) {
            lastCameraGridPos = new Vector3Int(coarseCamX, 0, coarseCamZ);
            lastCameraRotation = mainCameraTransform.rotation;
            UpdateDynamicLoading();
        }
    }

    private void UpdateDynamicLoading() {
        int camX = lastCameraGridPos.x * updateStride;
        int camZ = lastCameraGridPos.z * updateStride + generationBiasZ;

        // Define loading boundary
        int startX = Mathf.Max(0, camX - viewDistance);
        int endX   = Mathf.Min(size - 1, camX + viewDistance);
        int startZ = Mathf.Max(0, camZ - viewDistance);
        int endZ   = Mathf.Min(size - 1, camZ + viewDistance);

        HashSet<Vector2Int> newInRange = new HashSet<Vector2Int>();
        Plane[] cameraPlanes = GeometryUtility.CalculateFrustumPlanes(mainCamera);

        // Build or reuse blocks in the active range.
        for (int x = startX; x <= endX; x++) {
            for (int z = startZ; z <= endZ; z++) {
                // Check if the block is within the camera's view frustum
                Vector3 blockCenter = new Vector3(x - (size / 2f), flatHeight, z - (size / 2f));
                // Expand bounds by a larger margin (e.g., 20) to generate an extra ring/padding outside the view
                Bounds blockBounds = new Bounds(blockCenter, Vector3.one * 20f); 

                if (GeometryUtility.TestPlanesAABB(cameraPlanes, blockBounds)) {
                    var pos = new Vector2Int(x, z);
                    newInRange.Add(pos);
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