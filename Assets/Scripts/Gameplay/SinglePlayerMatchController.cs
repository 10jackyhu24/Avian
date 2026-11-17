using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public enum SinglePlayerMatchPhase {
    Preparing,
    Grace,
    Wave,
    FinalBoss,
    Victory,
    Defeat
}

[DisallowMultipleComponent]
public sealed class SinglePlayerMatchController : MonoBehaviour {
    private const int StartingLives = 3;
    private const int FinalBossPoints = 500;
    private const float FinalBossSpawnHeight = 5f;

    public static SinglePlayerMatchController Instance { get; private set; }

    private SeededEnemySpawner enemySpawner;
    private GameObject enemyPrefab;
    private int graceActiveLimit;
    private int waveActiveLimit;
    private int remainingLives = StartingLives;
    private int score;
    private int defeatedEnemies;
    private bool initialized;
    private bool finalBossSpawned;
    private SinglePlayerMatchPhase phase = SinglePlayerMatchPhase.Preparing;
    private EnemyHealthDrop finalBossHealth;

    private GameObject canvasObject;
    private Text statusText;
    private CanvasGroup bannerGroup;
    private Text bannerText;
    private GameObject resultPanel;
    private Text resultTitle;
    private Text resultDetails;
    private float bannerStartedAt = float.NegativeInfinity;

    public bool ControlsRespawning => initialized;
    public bool IsMatchOver => phase == SinglePlayerMatchPhase.Victory ||
                               phase == SinglePlayerMatchPhase.Defeat;

    public static void EnsureExists(
        GameObject owner,
        SeededEnemySpawner seededEnemySpawner,
        GameObject configuredEnemyPrefab,
        int graceLimit,
        int waveLimit) {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (owner == null || networkManager == null || !networkManager.IsServer) {
            return;
        }

        SinglePlayerMatchController controller = owner.GetComponent<SinglePlayerMatchController>();
        if (controller == null) {
            controller = owner.AddComponent<SinglePlayerMatchController>();
        }

        controller.Initialize(
            seededEnemySpawner,
            configuredEnemyPrefab,
            graceLimit,
            waveLimit
        );
    }

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(this);
            return;
        }

        Instance = this;
        EnemyHealthDrop.EnemyDefeated += HandleEnemyDefeated;
    }

    private void OnDestroy() {
        EnemyHealthDrop.EnemyDefeated -= HandleEnemyDefeated;
        if (Instance == this) {
            Instance = null;
        }

        if (canvasObject != null) {
            Destroy(canvasObject);
        }
    }

    private void Initialize(
        SeededEnemySpawner seededEnemySpawner,
        GameObject configuredEnemyPrefab,
        int graceLimit,
        int waveLimit) {
        if (initialized) {
            return;
        }

        enemySpawner = seededEnemySpawner;
        enemyPrefab = configuredEnemyPrefab;
        graceActiveLimit = Mathf.Max(0, graceLimit);
        waveActiveLimit = Mathf.Max(graceActiveLimit, waveLimit);
        remainingLives = StartingLives;
        initialized = true;

        if (!Application.isBatchMode) {
            CreateUi();
            ShowBanner("Prepare to survive");
            RefreshStatus(null);
        }

        Debug.Log("[SinglePlayerMatch] Match initialized with 3 lives.", this);
    }

    private void Update() {
        UpdateBannerFade();

        if (!initialized || IsMatchOver ||
            NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsServer) {
            return;
        }

        ZoneManager zone = ZoneManager.Instance;
        if (zone == null || !zone.TryGetSnapshot(out ZoneSnapshot snapshot)) {
            RefreshStatus(null);
            return;
        }

        if (snapshot.Phase == ZonePhase.Complete) {
            StartFinalBoss(snapshot);
        }
        else if (snapshot.Phase == ZonePhase.Shrinking) {
            SetPhase(SinglePlayerMatchPhase.Wave);
        }
        else {
            SetPhase(SinglePlayerMatchPhase.Grace);
        }

        RefreshStatus(snapshot);
    }

    public bool TryHandlePlayerDefeat(
        PlayerController player,
        out Vector3 respawnPosition) {
        respawnPosition = player != null ? player.SpawnPosition : Vector3.zero;
        if (!initialized || IsMatchOver || player == null) {
            return false;
        }

        remainingLives = Mathf.Max(0, remainingLives - 1);
        if (remainingLives == 0) {
            EndMatch(SinglePlayerMatchPhase.Defeat);
            return false;
        }

        TerrainController terrain = TerrainController.Instance;
        if (terrain != null &&
            terrain.TryGetNearestOutskirtRespawnPosition(
                player.transform.position,
                out Vector3 nearestOutskirtPosition)) {
            respawnPosition = nearestOutskirtPosition;
        }

        ShowBanner($"Life lost - {remainingLives} remaining");
        RefreshStatus(null);
        Debug.Log(
            $"[SinglePlayerMatch] Player will respawn at {respawnPosition}. " +
            $"Lives remaining={remainingLives}.",
            this
        );
        return true;
    }

    private void SetPhase(SinglePlayerMatchPhase newPhase) {
        if (phase == newPhase || IsMatchOver) {
            return;
        }

        phase = newPhase;
        switch (phase) {
            case SinglePlayerMatchPhase.Grace:
                enemySpawner?.SetActiveLimit(graceActiveLimit);
                ShowBanner("Grace period");
                break;
            case SinglePlayerMatchPhase.Wave:
                enemySpawner?.SetActiveLimit(waveActiveLimit);
                ShowBanner("Enemy wave incoming");
                break;
            case SinglePlayerMatchPhase.FinalBoss:
                ShowBanner("Final boss incoming");
                break;
        }

        Debug.Log($"[SinglePlayerMatch] Phase changed to {phase}.", this);
    }

    private void StartFinalBoss(ZoneSnapshot snapshot) {
        if (finalBossSpawned || enemyPrefab == null) {
            return;
        }

        finalBossSpawned = true;
        enemySpawner?.StopPopulation();
        SetPhase(SinglePlayerMatchPhase.FinalBoss);

        float groundHeight = TerrainController.Instance != null
            ? TerrainController.Instance.GetServerWorldCenter().y
            : 0f;
        Vector3 spawnPosition = new Vector3(
            snapshot.Center.x,
            groundHeight + FinalBossSpawnHeight,
            snapshot.Center.y
        );

        GameObject bossObject = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
        bossObject.GetComponent<EnemyController>()?.ConfigureArchetype(EnemyArchetype.Boss);
        finalBossHealth = bossObject.GetComponent<EnemyHealthDrop>();

        if (!bossObject.TryGetComponent(out NetworkObject networkObject)) {
            Debug.LogError("[SinglePlayerMatch] Final boss prefab is missing NetworkObject.", bossObject);
            Destroy(bossObject);
            finalBossHealth = null;
            return;
        }

        networkObject.Spawn(true);
        Debug.Log($"[SinglePlayerMatch] Final boss spawned at {spawnPosition}.", this);
    }

    private void HandleEnemyDefeated(
        EnemyHealthDrop enemy,
        ulong attackerClientId,
        bool awardsScore) {
        if (!initialized || IsMatchOver || enemy == null) {
            return;
        }

        bool isFinalBoss = enemy == finalBossHealth;
        if (awardsScore && IsLocalPlayer(attackerClientId)) {
            EnemyArchetype archetype = enemy.GetComponent<EnemyController>()?.Archetype
                                       ?? EnemyArchetype.Grunt;
            score += isFinalBoss ? FinalBossPoints : GetEnemyPoints(archetype);
            defeatedEnemies++;
        }

        if (isFinalBoss) {
            EndMatch(SinglePlayerMatchPhase.Victory);
        }
        else {
            RefreshStatus(null);
        }
    }

    private static bool IsLocalPlayer(ulong attackerClientId) {
        NetworkObject playerObject = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        return playerObject != null && playerObject.OwnerClientId == attackerClientId;
    }

    private static int GetEnemyPoints(EnemyArchetype archetype) {
        return archetype switch {
            EnemyArchetype.Raider => 20,
            EnemyArchetype.Spitter => 25,
            EnemyArchetype.MiniBoss => 100,
            EnemyArchetype.Boss => 250,
            _ => 10
        };
    }

    private void EndMatch(SinglePlayerMatchPhase result) {
        if (IsMatchOver) {
            return;
        }

        phase = result;
        enemySpawner?.StopPopulation();

        if (result == SinglePlayerMatchPhase.Defeat && finalBossHealth != null &&
            finalBossHealth.TryGetComponent(out NetworkObject bossNetworkObject) &&
            bossNetworkObject.IsSpawned) {
            bossNetworkObject.Despawn(true);
        }

        ShowBanner(result == SinglePlayerMatchPhase.Victory ? "Victory" : "Defeat");
        ShowResult();
        RefreshStatus(null);
        Debug.Log(
            $"[SinglePlayerMatch] Match ended: {result}. Score={score}, kills={defeatedEnemies}.",
            this
        );
    }

    private void CreateUi() {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        canvasObject = new GameObject(
            "Single Player Match UI",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform statusPanel = CreatePanel(
            "Match Status",
            canvasObject.transform,
            new Vector2(0f, 1f),
            new Vector2(24f, -24f),
            new Vector2(440f, 100f),
            new Color(0.04f, 0.05f, 0.08f, 0.78f)
        );
        statusText = CreateText(
            "Status",
            statusPanel,
            Vector2.zero,
            Vector2.zero,
            statusPanel.sizeDelta - new Vector2(24f, 16f),
            font,
            25,
            TextAnchor.MiddleLeft
        );

        RectTransform banner = CreatePanel(
            "Phase Banner",
            canvasObject.transform,
            new Vector2(0.5f, 0.82f),
            Vector2.zero,
            new Vector2(620f, 86f),
            new Color(0.05f, 0.05f, 0.08f, 0.88f)
        );
        bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
        bannerText = CreateText(
            "Message",
            banner,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            banner.sizeDelta,
            font,
            40,
            TextAnchor.MiddleCenter
        );
        bannerGroup.alpha = 0f;

        resultPanel = CreateStretchPanel(
            "Match Result",
            canvasObject.transform,
            new Color(0.02f, 0.02f, 0.04f, 0.88f)
        ).gameObject;
        resultTitle = CreateText(
            "Result Title",
            resultPanel.transform,
            new Vector2(0.5f, 0.62f),
            Vector2.zero,
            new Vector2(900f, 130f),
            font,
            72,
            TextAnchor.MiddleCenter
        );
        resultDetails = CreateText(
            "Result Details",
            resultPanel.transform,
            new Vector2(0.5f, 0.43f),
            Vector2.zero,
            new Vector2(900f, 220f),
            font,
            36,
            TextAnchor.UpperCenter
        );
        CreateButton(
            "Back to Main Menu",
            resultPanel.transform,
            new Vector2(0.5f, 0.22f),
            new Vector2(340f, 82f),
            font,
            ReturnToMainMenu
        );
        resultPanel.SetActive(false);
    }

    private void ReturnToMainMenu() {
        PauseMenu pauseMenu = FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);
        if (pauseMenu != null) {
            pauseMenu.Leave();
            Destroy(this);
            return;
        }

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null && networkManager.IsListening) {
            networkManager.Shutdown();
        }

        GameState.ResetWorld();
        MenuManager menuManager = FindFirstObjectByType<MenuManager>();
        if (menuManager != null) {
            menuManager.HideMenu();
            menuManager.ClearHistory();
            menuManager.SwitchToPanel(PanelType.MainMenu);
        }

        Destroy(this);
    }

    private void RefreshStatus(ZoneSnapshot? snapshot) {
        if (statusText == null) {
            return;
        }

        string timer = "--:--";
        if (snapshot.HasValue) {
            int seconds = Mathf.CeilToInt(snapshot.Value.SecondsRemaining);
            timer = $"{seconds / 60:00}:{seconds % 60:00}";
        }

        statusText.text =
            $"Lives: {remainingLives}/{StartingLives}    Score: {score}\n" +
            $"{GetPhaseLabel(phase)}    Time: {timer}";
    }

    private static string GetPhaseLabel(SinglePlayerMatchPhase value) {
        return value switch {
            SinglePlayerMatchPhase.Grace => "Grace period",
            SinglePlayerMatchPhase.Wave => "Enemy wave",
            SinglePlayerMatchPhase.FinalBoss => "Final boss",
            SinglePlayerMatchPhase.Victory => "Victory",
            SinglePlayerMatchPhase.Defeat => "Defeat",
            _ => "Preparing"
        };
    }

    private void ShowBanner(string message) {
        if (bannerGroup == null || bannerText == null) {
            return;
        }

        bannerText.text = message;
        bannerGroup.alpha = 1f;
        bannerStartedAt = Time.unscaledTime;
    }

    private void UpdateBannerFade() {
        if (bannerGroup == null) {
            return;
        }

        float elapsed = Time.unscaledTime - bannerStartedAt;
        if (elapsed < 2f) {
            bannerGroup.alpha = Mathf.Clamp01(elapsed / 0.2f);
        }
        else {
            bannerGroup.alpha = 1f - Mathf.Clamp01((elapsed - 2f) / 1f);
        }
    }

    private void ShowResult() {
        if (resultPanel == null) {
            return;
        }

        resultPanel.SetActive(true);
        resultTitle.text = phase == SinglePlayerMatchPhase.Victory ? "VICTORY" : "DEFEAT";
        resultTitle.color = phase == SinglePlayerMatchPhase.Victory
            ? new Color(0.45f, 1f, 0.45f)
            : new Color(1f, 0.35f, 0.35f);
        resultDetails.text =
            $"Score: {score}\n" +
            $"Enemies defeated: {defeatedEnemies}\n" +
            $"Lives remaining: {remainingLives}";
    }

    private static RectTransform CreatePanel(
        string name,
        Transform parent,
        Vector2 anchor,
        Vector2 position,
        Vector2 size,
        Color color) {
        RectTransform rect = CreateRect(name, parent, anchor, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return rect;
    }

    private static RectTransform CreateStretchPanel(
        string name,
        Transform parent,
        Color color) {
        RectTransform rect = new GameObject(name, typeof(RectTransform), typeof(Image))
            .GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.GetComponent<Image>().color = color;
        return rect;
    }

    private static RectTransform CreateRect(
        string name,
        Transform parent,
        Vector2 anchor,
        Vector2 position,
        Vector2 size) {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static Text CreateText(
        string name,
        Transform parent,
        Vector2 anchor,
        Vector2 position,
        Vector2 size,
        Font font,
        int fontSize,
        TextAnchor alignment) {
        RectTransform rect = CreateRect(name, parent, anchor, position, size);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        Shadow shadow = rect.gameObject.AddComponent<Shadow>();
        shadow.effectColor = Color.black;
        shadow.effectDistance = new Vector2(2f, -2f);
        return text;
    }

    private static Button CreateButton(
        string label,
        Transform parent,
        Vector2 anchor,
        Vector2 size,
        Font font,
        UnityEngine.Events.UnityAction onClick) {
        RectTransform rect = CreateRect(label, parent, anchor, Vector2.zero, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.88f, 0.88f, 0.92f, 1f);

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        Text text = CreateText(
            "Label",
            rect,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            size - new Vector2(20f, 12f),
            font,
            32,
            TextAnchor.MiddleCenter
        );
        text.color = Color.black;
        return button;
    }
}
