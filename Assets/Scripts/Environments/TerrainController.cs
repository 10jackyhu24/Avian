using System.Collections.Generic;
using UnityEngine;

public class TerrainController : MonoBehaviour {
    [Header("Basic Settings")]
    [SerializeField] private GameObject cubePrefab;
    [SerializeField] private int size = 1000;
    
    [Header("Dynamic Loading")]
    [SerializeField] private int viewDistance = 50;
    [SerializeField] private float rotationThreshold = 3f;

    private Camera mainCamera;
    private Transform mainCameraTransform;
    private GameObject[,] cubeGrid;
    private GameObject terrainParent;

    // Record the camera state last time to avoid repeated calculations
    private Vector3Int lastCameraGridPos = new Vector3Int(-9999, -9999, -9999);
    private Quaternion lastCameraRotation = Quaternion.identity;

    // Track the currently loaded and displayed coordinates
    private HashSet<Vector2Int> inRangePositions = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> visiblePositions = new HashSet<Vector2Int>();

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

    public void GenerateWorld(int seed) {
        mainCameraTransform.position = new Vector3(0f, mainCameraTransform.position.y, 0f);

        // Clean up old terrain
        terrainParent = GameObject.Find("Terrain");
        foreach (Transform child in terrainParent.transform) Destroy(child.gameObject);

        // Initialize
        cubeGrid = new GameObject[size, size];
        inRangePositions.Clear();
        visiblePositions.Clear();
        // Initialize lastCameraGridPos using integer grid coordinates consistent with Update()
        lastCameraGridPos = new Vector3Int(0, 0, 0);
        lastCameraRotation = Quaternion.identity;

        // Trigger the first terrain load
        UpdateDynamicLoading(true);
    }

    private void Update() {
        if (!GameState.isPlaying) return;
        // Calculate the camera's current grid coordinates
        int camGridX = Mathf.RoundToInt(mainCameraTransform.position.x) + size / 2;
        int camGridZ = Mathf.RoundToInt(mainCameraTransform.position.z) + size / 2;

        bool posChanged = (camGridX != lastCameraGridPos.x || camGridZ != lastCameraGridPos.z);
        bool rotChanged = Quaternion.Angle(mainCameraTransform.rotation, lastCameraRotation) > rotationThreshold;

        // When the camera position or rotation angle exceeds the threshold, update the frustum culling
        if (posChanged || rotChanged) {
            lastCameraGridPos = new Vector3Int(camGridX, 0, camGridZ);
            lastCameraRotation = mainCameraTransform.rotation;
            UpdateDynamicLoading(false);
        }
    }

    private void UpdateDynamicLoading(bool forceUpdate) {
        int camX = lastCameraGridPos.x;
        int camZ = lastCameraGridPos.z;

        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(mainCamera);

        // Define loading boundary
        int startX = Mathf.Max(0, camX - viewDistance);
        int endX   = Mathf.Min(size - 1, camX + viewDistance);
        int startZ = Mathf.Max(0, camZ - viewDistance);
        int endZ   = Mathf.Min(size - 1, camZ + viewDistance);

        HashSet<Vector2Int> newInRange = new HashSet<Vector2Int>();
        HashSet<Vector2Int> newVisible = new HashSet<Vector2Int>();

        // 1. Filter out the coordinates within the range and the coordinates within the frustum
        for (int x = startX; x <= endX; x++) {
            for (int z = startZ; z <= endZ; z++) {
                var pos = new Vector2Int(x, z);
                newInRange.Add(pos);
                if (IsInFrustum(x, z, frustumPlanes)) {
                    newVisible.Add(pos);
                }
            }
        }

        // 2. Instantiate new blocks that enter the range (hide them if they are outside the frustum)
        foreach (Vector2Int pos in newInRange) {
            if (cubeGrid[pos.x, pos.y] == null) {
                CreateBlockAt(pos.x, pos.y);
                if (!newVisible.Contains(pos)) {
                    cubeGrid[pos.x, pos.y].SetActive(false);
                }
            }
        }

        // 3. Show new blocks that enter the frustum
        foreach (Vector2Int pos in newVisible) {
            if (cubeGrid[pos.x, pos.y] != null && !cubeGrid[pos.x, pos.y].activeSelf) {
                cubeGrid[pos.x, pos.y].SetActive(true);
            }
        }

        // 4. Hide blocks that leave the frustum
        foreach (Vector2Int pos in visiblePositions) {
            if (!newVisible.Contains(pos) && cubeGrid[pos.x, pos.y] != null) {
                cubeGrid[pos.x, pos.y].SetActive(false);
            }
        }

        // 5. Hide blocks that leave the generation radius completely
        foreach (Vector2Int pos in inRangePositions) {
            if (!newInRange.Contains(pos) && cubeGrid[pos.x, pos.y] != null) {
                cubeGrid[pos.x, pos.y].SetActive(false);
            }
        }

        inRangePositions = newInRange;
        visiblePositions = newVisible;
    }

    private bool IsInFrustum(int x, int z, Plane[] planes) {
        float worldX = x - (size / 2);
        float worldZ = z - (size / 2);

        // Simplified bounding box calculation: assume block height is between 0 and 5
        Bounds blockBounds = new Bounds(new Vector3(worldX, 2.5f, worldZ), new Vector3(1f, 6f, 1f));
        return GeometryUtility.TestPlanesAABB(planes, blockBounds);
    }

    private void CreateBlockAt(int x, int z) {
        // Fixed noise scaling ratio to remove redundant multiplication
        float h = Mathf.PerlinNoise(x * 0.05f, z * 0.05f);
        float m = Mathf.PerlinNoise(x * 0.075f, z * 0.075f);

        // Calculate 3D wave height
        float yPos = (h >= 0.4f) ? (h - 0.4f) * 10f : 0f;

        Vector3 position = new Vector3(x - (size / 2), yPos, z - (size / 2));
        GameObject newCube = Instantiate(cubePrefab, position, Quaternion.identity);
        
        newCube.transform.parent = terrainParent.transform;
        newCube.GetComponent<Renderer>().material.color = GetBiomeColor(h, m);

        cubeGrid[x, z] = newCube;
    }

    private Color GetBiomeColor(float h, float m) {
        if (h < 0.4f) return (h < 0.25f) ? deepOcean : shallowWater;
        if (h > 0.65f) return (m > 0.55f) ? snowPeak : mountain;
        
        if (m < 0.35f) return desert;
        if (m < 0.65f) return grassland;
        return rainforest;
    }
}