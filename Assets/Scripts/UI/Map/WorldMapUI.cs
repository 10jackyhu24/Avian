using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class WorldMapUI : MonoBehaviour {
    [Header("World")]
    [SerializeField] private int worldSize = 1000;
    [SerializeField] private float biomeScale = 0.03f;

    [Header("Map")]
    [SerializeField] private int mapResolution = 192;
    [SerializeField] private float revealRadius = 70f;
    [SerializeField] private float discoveryTickInterval = 0.15f;
    [SerializeField] private float markerRefreshInterval = 0.2f;

    [Header("UI")]
    [SerializeField] private float minimapSize = 180f;
    [SerializeField] private float largeMapSize = 620f;
    [SerializeField] private Vector2 screenOffset = new Vector2(-20f, -20f);
    [SerializeField] private bool debugLogging = true;

    private Canvas canvas;
    private RawImage mapImage;
    private RectTransform panelRect;
    private RectTransform mapRect;
    private RectTransform playerMarker;
    private RectTransform markerLayer;
    private Image panelImage;
    private Texture2D mapTexture;
    private Color32[] biomePixels;
    private Color32[] displayPixels;
    private bool[] discoveredCells;
    private GameInput inputActions;
    private InputAction mapAction;
    private int activeSeed = int.MinValue;
    private float nextDiscoveryTickTime;
    private float nextMarkerRefreshTime;
    private bool isLargeMapOpen;
    private readonly Dictionary<MapMarker, Image> markerImages = new();
    private bool loggedWaitingForGameState;
    private bool loggedShowing;
    private bool loggedMissingLocalPlayer;
    private bool loggedLocalPlayerFound;
    private bool loggedFirstReveal;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeMapExists() {
        EnsureExists();
    }

    public static WorldMapUI EnsureExists() {
        if (Application.isBatchMode) {
            return null;
        }

        WorldMapUI existingMap = FindFirstObjectByType<WorldMapUI>();
        if (existingMap != null) {
            return existingMap;
        }

        GameObject mapObject = new GameObject("Runtime_WorldMapUI");
        DontDestroyOnLoad(mapObject);
        return mapObject.AddComponent<WorldMapUI>();
    }

    private void Awake() {
        CreateRuntimeUi();
        InitializeInput();
        SetVisible(false);

        if (debugLogging) {
            Debug.Log("[WorldMapUI] Runtime minimap UI created.", this);
        }
    }

    private void OnEnable() {
        OptionsMenu.BindingOverridesChanged -= HandleBindingOverridesChanged;
        OptionsMenu.BindingOverridesChanged += HandleBindingOverridesChanged;
    }

    private void OnDisable() {
        OptionsMenu.BindingOverridesChanged -= HandleBindingOverridesChanged;
    }

    private void OnDestroy() {
        OptionsMenu.BindingOverridesChanged -= HandleBindingOverridesChanged;

        if (mapAction != null) {
            mapAction.performed -= OnMapPerformed;
        }

        inputActions?.Disable();
        inputActions?.Dispose();
    }

    private void Update() {
        if (Application.isBatchMode) {
            Destroy(gameObject);
            return;
        }

        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening &&
            NetworkManager.Singleton.IsServer &&
            !NetworkManager.Singleton.IsClient) {
            Destroy(gameObject);
            return;
        }

        bool shouldShow = GameState.isPlaying && GameState.hasWorldSeed;
        SetVisible(shouldShow);
        if (!shouldShow) {
            if (isLargeMapOpen) {
                SetLargeMapOpen(false);
            }

            if (debugLogging && !loggedWaitingForGameState) {
                loggedWaitingForGameState = true;
                Debug.Log(
                    $"[WorldMapUI] Waiting for active world. isPlaying={GameState.isPlaying}, hasWorldSeed={GameState.hasWorldSeed}.",
                    this
                );
            }

            return;
        }

        if (debugLogging && !loggedShowing) {
            loggedShowing = true;
            Debug.Log($"[WorldMapUI] Showing minimap for seed={GameState.currentSeed}.", this);
        }

        EnsureMapForSeed(GameState.currentSeed);

        Transform localPlayer = FindLocalPlayer();
        if (localPlayer == null) {
            if (debugLogging && !loggedMissingLocalPlayer) {
                loggedMissingLocalPlayer = true;
                Debug.Log("[WorldMapUI] Waiting for local player.", this);
            }

            return;
        }

        if (debugLogging && !loggedLocalPlayerFound) {
            loggedLocalPlayerFound = true;
            Debug.Log($"[WorldMapUI] Found local player at {localPlayer.position}.", this);
        }

        UpdatePlayerMarker(localPlayer.position);

        if (Time.time >= nextDiscoveryTickTime) {
            nextDiscoveryTickTime = Time.time + discoveryTickInterval;
            if (RevealAround(localPlayer.position)) {
                RefreshDisplayTexture();

                if (debugLogging && !loggedFirstReveal) {
                    loggedFirstReveal = true;
                    Debug.Log($"[WorldMapUI] Revealed map around {localPlayer.position}.", this);
                }
            }
        }

        if (Time.time >= nextMarkerRefreshTime) {
            nextMarkerRefreshTime = Time.time + markerRefreshInterval;
            RefreshDynamicMarkers(localPlayer.position);
        }
    }

    private void CreateRuntimeUi() {
        GameObject canvasObject = new GameObject("WorldMap_Canvas");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler canvasScaler = canvasObject.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.matchWidthOrHeight = 0.5f;

        GameObject panelObject = new GameObject("Minimap_Panel");
        panelObject.transform.SetParent(canvasObject.transform, false);
        panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0.02f, 0.025f, 0.03f, 0.84f);

        panelRect = panelObject.GetComponent<RectTransform>();

        GameObject mapObject = new GameObject("Biome_Map");
        mapObject.transform.SetParent(panelObject.transform, false);
        mapImage = mapObject.AddComponent<RawImage>();
        mapImage.color = Color.white;
        mapRect = mapObject.GetComponent<RectTransform>();
        mapRect.anchorMin = new Vector2(0.5f, 0.5f);
        mapRect.anchorMax = new Vector2(0.5f, 0.5f);
        mapRect.pivot = new Vector2(0.5f, 0.5f);
        mapRect.anchoredPosition = Vector2.zero;
        mapRect.sizeDelta = new Vector2(minimapSize - 12f, minimapSize - 12f);

        GameObject markerLayerObject = new GameObject("Dynamic_Markers");
        markerLayerObject.transform.SetParent(panelObject.transform, false);
        markerLayer = markerLayerObject.AddComponent<RectTransform>();
        markerLayer.anchorMin = new Vector2(0.5f, 0.5f);
        markerLayer.anchorMax = new Vector2(0.5f, 0.5f);
        markerLayer.pivot = new Vector2(0.5f, 0.5f);
        markerLayer.anchoredPosition = Vector2.zero;

        GameObject markerObject = new GameObject("Player_Marker");
        markerObject.transform.SetParent(panelObject.transform, false);
        Image markerImage = markerObject.AddComponent<Image>();
        markerImage.color = Color.white;
        playerMarker = markerObject.GetComponent<RectTransform>();
        playerMarker.anchorMin = new Vector2(0.5f, 0.5f);
        playerMarker.anchorMax = new Vector2(0.5f, 0.5f);
        playerMarker.pivot = new Vector2(0.5f, 0.5f);
        playerMarker.sizeDelta = new Vector2(8f, 8f);

        ApplyMapLayout();
    }

    private void InitializeInput() {
        inputActions = new GameInput();
        KeyBinding.LoadBindingOverrides(inputActions);
        mapAction = inputActions.FindAction("Map");
        if (mapAction != null) {
            mapAction.performed -= OnMapPerformed;
            mapAction.performed += OnMapPerformed;
        }
        else {
            Debug.LogWarning("[WorldMapUI] Map input action not found.", this);
        }

        inputActions.Player.Enable();
    }

    private void HandleBindingOverridesChanged() {
        KeyBinding.LoadBindingOverrides(inputActions);
    }

    private void OnMapPerformed(InputAction.CallbackContext context) {
        if (!context.performed || !GameState.isPlaying || PauseMenu.isOpen) {
            return;
        }

        SetLargeMapOpen(!isLargeMapOpen);
    }

    private void SetLargeMapOpen(bool shouldOpen) {
        if (isLargeMapOpen == shouldOpen) {
            return;
        }

        isLargeMapOpen = shouldOpen;
        ApplyMapLayout();
    }

    private void ApplyMapLayout() {
        if (panelRect == null || mapRect == null || playerMarker == null) {
            return;
        }

        float currentSize = isLargeMapOpen ? largeMapSize : minimapSize;
        panelRect.anchorMin = isLargeMapOpen ? new Vector2(0.5f, 0.5f) : new Vector2(1f, 1f);
        panelRect.anchorMax = panelRect.anchorMin;
        panelRect.pivot = isLargeMapOpen ? new Vector2(0.5f, 0.5f) : new Vector2(1f, 1f);
        panelRect.anchoredPosition = isLargeMapOpen ? Vector2.zero : screenOffset;
        panelRect.sizeDelta = new Vector2(currentSize, currentSize);
        mapRect.sizeDelta = new Vector2(currentSize - 12f, currentSize - 12f);
        markerLayer.sizeDelta = mapRect.sizeDelta;
        playerMarker.sizeDelta = isLargeMapOpen ? new Vector2(12f, 12f) : new Vector2(8f, 8f);

        if (panelImage != null) {
            panelImage.color = isLargeMapOpen
                ? new Color(0.02f, 0.025f, 0.03f, 0.94f)
                : new Color(0.02f, 0.025f, 0.03f, 0.84f);
        }
    }

    private void EnsureMapForSeed(int seed) {
        if (activeSeed == seed && mapTexture != null) {
            return;
        }

        activeSeed = seed;
        ClearDynamicMarkers();
        mapResolution = Mathf.Max(16, mapResolution);
        int pixelCount = mapResolution * mapResolution;
        biomePixels = new Color32[pixelCount];
        displayPixels = new Color32[pixelCount];
        discoveredCells = new bool[pixelCount];

        mapTexture = new Texture2D(mapResolution, mapResolution, TextureFormat.RGBA32, false) {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        float halfWorldSize = worldSize * 0.5f;
        for (int y = 0; y < mapResolution; y++) {
            for (int x = 0; x < mapResolution; x++) {
                float worldX = ((x + 0.5f) / mapResolution) * worldSize - halfWorldSize;
                float worldZ = ((y + 0.5f) / mapResolution) * worldSize - halfWorldSize;
                TerrainBiomeMask biome = WorldBiomeUtility.GetBiomeAtWorldPosition(
                    seed,
                    new Vector3(worldX, 0f, worldZ),
                    worldSize,
                    biomeScale
                );

                int index = ToIndex(x, y);
                biomePixels[index] = GetBiomeColor(biome);
                displayPixels[index] = GetUndiscoveredColor();
            }
        }

        mapTexture.SetPixels32(displayPixels);
        mapTexture.Apply(false);
        mapImage.texture = mapTexture;

        if (debugLogging) {
            Debug.Log(
                $"[WorldMapUI] Generated biome map texture. seed={seed}, resolution={mapResolution}, worldSize={worldSize}.",
                this
            );
        }
    }

    private bool RevealAround(Vector3 worldPosition) {
        Vector2Int center = WorldToMapCell(worldPosition);
        int revealCells = Mathf.Max(1, Mathf.CeilToInt((revealRadius / worldSize) * mapResolution));
        int revealCellsSquared = revealCells * revealCells;
        bool changed = false;

        int minX = Mathf.Max(0, center.x - revealCells);
        int maxX = Mathf.Min(mapResolution - 1, center.x + revealCells);
        int minY = Mathf.Max(0, center.y - revealCells);
        int maxY = Mathf.Min(mapResolution - 1, center.y + revealCells);

        for (int y = minY; y <= maxY; y++) {
            for (int x = minX; x <= maxX; x++) {
                int dx = x - center.x;
                int dy = y - center.y;
                if (dx * dx + dy * dy > revealCellsSquared) {
                    continue;
                }

                int index = ToIndex(x, y);
                if (discoveredCells[index]) {
                    continue;
                }

                discoveredCells[index] = true;
                displayPixels[index] = biomePixels[index];
                changed = true;
            }
        }

        return changed;
    }

    private void RefreshDisplayTexture() {
        mapTexture.SetPixels32(displayPixels);
        mapTexture.Apply(false);
    }

    private Transform FindLocalPlayer() {
        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        if (players.Length == 1 && !players[0].IsSpawned) {
            return players[0].transform;
        }

        foreach (PlayerController player in players) {
            if (player != null && player.IsSpawned && player.IsOwner) {
                return player.transform;
            }
        }

        return null;
    }

    private void UpdatePlayerMarker(Vector3 worldPosition) {
        Vector2 normalized = WorldToNormalizedMapPosition(worldPosition);
        Vector2 size = mapRect.sizeDelta;
        playerMarker.anchoredPosition = new Vector2(
            (normalized.x - 0.5f) * size.x,
            (normalized.y - 0.5f) * size.y
        );
    }

    private void RefreshDynamicMarkers(Vector3 playerPosition) {
        MapMarker[] worldMarkers = FindObjectsByType<MapMarker>(FindObjectsSortMode.None);
        HashSet<MapMarker> visibleMarkers = new();

        foreach (MapMarker marker in worldMarkers) {
            if (!ShouldShowMarker(marker, playerPosition)) {
                continue;
            }

            visibleMarkers.Add(marker);
            if (!markerImages.TryGetValue(marker, out Image markerImage)) {
                markerImage = CreateMarkerImage(marker);
                markerImages.Add(marker, markerImage);
            }

            UpdateMarkerImage(marker, markerImage);
        }

        List<MapMarker> markersToRemove = new();
        foreach (KeyValuePair<MapMarker, Image> entry in markerImages) {
            if (!visibleMarkers.Contains(entry.Key)) {
                Destroy(entry.Value.gameObject);
                markersToRemove.Add(entry.Key);
            }
        }

        foreach (MapMarker marker in markersToRemove) {
            markerImages.Remove(marker);
        }
    }

    private bool ShouldShowMarker(MapMarker marker, Vector3 playerPosition) {
        if (marker == null ||
            (marker.MarkerType != MapMarkerType.Enemy &&
             marker.MarkerType != MapMarkerType.DroppedItem)) {
            return false;
        }

        Vector3 offset = marker.WorldPosition - playerPosition;
        offset.y = 0f;
        float visibilityRange = Mathf.Max(0f, marker.DiscoveryRadius);
        return offset.sqrMagnitude <= visibilityRange * visibilityRange &&
               IsDiscovered(marker.WorldPosition);
    }

    private Image CreateMarkerImage(MapMarker marker) {
        GameObject markerObject = new GameObject($"{marker.MarkerType}_Marker");
        markerObject.transform.SetParent(markerLayer, false);

        Image markerImage = markerObject.AddComponent<Image>();
        markerImage.raycastTarget = false;
        return markerImage;
    }

    private void UpdateMarkerImage(MapMarker marker, Image markerImage) {
        Vector2 normalized = WorldToNormalizedMapPosition(marker.WorldPosition);
        RectTransform markerRect = markerImage.rectTransform;
        markerRect.anchorMin = new Vector2(0.5f, 0.5f);
        markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.anchoredPosition = new Vector2(
            (normalized.x - 0.5f) * markerLayer.sizeDelta.x,
            (normalized.y - 0.5f) * markerLayer.sizeDelta.y
        );

        bool isEnemy = marker.MarkerType == MapMarkerType.Enemy;
        float markerSize = isEnemy
            ? (isLargeMapOpen ? 10f : 7f)
            : (isLargeMapOpen ? 9f : 6f);
        markerRect.sizeDelta = new Vector2(markerSize, markerSize);
        markerImage.color = isEnemy
            ? new Color(0.9f, 0.15f, 0.12f, 1f)
            : new Color(1f, 0.78f, 0.15f, 1f);
        markerImage.sprite = marker.Icon;
    }

    private bool IsDiscovered(Vector3 worldPosition) {
        if (discoveredCells == null) {
            return false;
        }

        Vector2Int cell = WorldToMapCell(worldPosition);
        return discoveredCells[ToIndex(cell.x, cell.y)];
    }

    private void ClearDynamicMarkers() {
        foreach (Image markerImage in markerImages.Values) {
            if (markerImage != null) {
                Destroy(markerImage.gameObject);
            }
        }

        markerImages.Clear();
    }

    private Vector2Int WorldToMapCell(Vector3 worldPosition) {
        Vector2 normalized = WorldToNormalizedMapPosition(worldPosition);
        int x = Mathf.Clamp(Mathf.FloorToInt(normalized.x * mapResolution), 0, mapResolution - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(normalized.y * mapResolution), 0, mapResolution - 1);
        return new Vector2Int(x, y);
    }

    private Vector2 WorldToNormalizedMapPosition(Vector3 worldPosition) {
        float halfWorldSize = worldSize * 0.5f;
        return new Vector2(
            Mathf.InverseLerp(-halfWorldSize, halfWorldSize, worldPosition.x),
            Mathf.InverseLerp(-halfWorldSize, halfWorldSize, worldPosition.z)
        );
    }

    private void SetVisible(bool isVisible) {
        if (canvas != null && canvas.enabled != isVisible) {
            canvas.enabled = isVisible;
        }
    }

    private int ToIndex(int x, int y) {
        return y * mapResolution + x;
    }

    private static Color32 GetBiomeColor(TerrainBiomeMask biome) {
        return biome switch {
            TerrainBiomeMask.DeepOcean => new Color32(20, 58, 112, 255),
            TerrainBiomeMask.ShallowWater => new Color32(45, 130, 165, 255),
            TerrainBiomeMask.Desert => new Color32(203, 176, 92, 255),
            TerrainBiomeMask.Grassland => new Color32(75, 143, 76, 255),
            TerrainBiomeMask.Rainforest => new Color32(34, 106, 64, 255),
            TerrainBiomeMask.Mountain => new Color32(235, 238, 240, 255),
            _ => new Color32(80, 80, 80, 255)
        };
    }

    private static Color32 GetUndiscoveredColor() {
        return new Color32(4, 7, 10, 255);
    }
}
