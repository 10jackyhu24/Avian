using UnityEngine;
using UnityEngine.UI;

/// <summary>Creates a screen-space HUD only for the locally owned player.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController), typeof(PlayerEnemySensor))]
public class PlayerCombatHUD : MonoBehaviour {
    [Header("Layout (1920 x 1080 reference resolution)")]
    [SerializeField] private Vector2 portraitOffset = new Vector2(32f, 32f);
    [Min(80f)] [SerializeField] private float portraitSize = 220f;
    [Min(80f)] [SerializeField] private float playerHealthBarHeight = 440f;
    [Min(24f)] [SerializeField] private float playerHealthBarWidth = 56f;
    [SerializeField] private Vector2 enemyPanelSize = new Vector2(580f, 116f);
    [Min(0f)] [SerializeField] private float enemyPanelTopOffset = 30f;

    private static readonly Color PanelColor = new Color(0.06f, 0.07f, 0.12f, 0.93f);
    private static readonly Color PlayerAccent = new Color(0.25f, 0.65f, 0.95f);
    private static readonly Color EnemyAccent = new Color(0.6f, 0.3f, 0.8f);

    private PlayerController player;
    private PlayerEnemySensor sensor;
    private PlayerCharacterVisual characterVisual;
    private SpriteRenderer playerRenderer;
    private GameObject canvasObject;
    private RectTransform safeRoot;
    private RectTransform enemyPanel;
    private Image playerPortrait;
    private Image enemyPortrait;
    private RectTransform playerFill;
    private RectTransform enemyFill;
    private Image playerFillImage;
    private Image enemyFillImage;
    private Text playerHealthText;
    private Text enemyHealthText;
    private Text enemyNameText;
    private Text enemyStatusText;
    private Font font;
    private Texture2D circleTexture;
    private Sprite circleSprite;

    private void Awake() {
        player = GetComponent<PlayerController>();
        sensor = GetComponent<PlayerEnemySensor>();
        characterVisual = GetComponent<PlayerCharacterVisual>();
        playerRenderer = GetComponent<SpriteRenderer>();
    }

    private void LateUpdate() {
        bool visible = player.IsSpawned && player.IsOwner && player.IsClient;
        if (!visible) {
            if (canvasObject != null) {
                canvasObject.SetActive(false);
            }
            return;
        }

        if (canvasObject == null) {
            CreateUi();
        }
        canvasObject.SetActive(true);
        UpdateSafeArea();

        SetPortrait(playerPortrait, characterVisual != null ? characterVisual.Portrait : playerRenderer?.sprite);
        SetHealth(playerFill, playerFillImage, playerHealthText, player.CurrentHealth, player.MaxHealth, true);

        EnemyHealthDrop enemy = sensor.Target;
        bool showEnemy = player.IsAlive && enemy != null && enemy.isActiveAndEnabled && enemy.IsAlive && enemy.IsSpawned;
        enemyPanel.gameObject.SetActive(showEnemy);
        if (showEnemy) {
            SetPortrait(enemyPortrait, enemy.Portrait);
            SetHealth(enemyFill, enemyFillImage, enemyHealthText, enemy.CurrentHealth, enemy.MaxHealth, false);
            enemyNameText.text = enemy.DisplayName;
            float ratio = HealthRatio(enemy.CurrentHealth, enemy.MaxHealth);
            enemyStatusText.text = ratio > 0.5f ? "HEALTHY" : ratio > 0.25f ? "WOUNDED" : "CRITICAL";
            enemyStatusText.color = HealthColor(ratio);
        }
    }

    private void CreateUi() {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureCircleSprite();
        canvasObject = new GameObject("Local Player Combat HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -50; // Above world health bars, below menus.
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        safeRoot = Rect("Safe Area", canvasObject.transform, Vector2.zero, Vector2.zero, Vector2.zero);
        UpdateSafeArea();

        RectTransform frame = ImageRect("Player Portrait Frame", safeRoot, Vector2.zero,
            portraitOffset, Vector2.one * portraitSize, PlayerAccent);
        frame.GetComponent<Image>().sprite = circleSprite;
        RectTransform mask = ImageRect("Player Portrait Mask", frame, Vector2.zero,
            Vector2.one * 5f, Vector2.one * (portraitSize - 10f), PanelColor);
        mask.GetComponent<Image>().sprite = circleSprite;
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        RectTransform portrait = ImageRect("Player Portrait", mask, Vector2.zero,
            Vector2.one * 8f, Vector2.one * (portraitSize - 26f), Color.white);
        playerPortrait = portrait.GetComponent<Image>();
        playerPortrait.preserveAspect = true;

        Vector2 healthPosition = new Vector2(portraitOffset.x + (portraitSize - playerHealthBarWidth) * 0.5f,
            portraitOffset.y + portraitSize + 48f);
        RectTransform healthFrame = ImageRect("Player Vertical Health", safeRoot, Vector2.zero,
            healthPosition, new Vector2(playerHealthBarWidth, playerHealthBarHeight), PlayerAccent);
        RectTransform playerTrack = ImageRect("Track", healthFrame, Vector2.zero, Vector2.one * 4f,
            new Vector2(playerHealthBarWidth - 8f, playerHealthBarHeight - 8f), PanelColor);
        playerFill = ImageRect("Fill", playerTrack, Vector2.zero, Vector2.zero, Vector2.zero, Color.white);
        playerFillImage = playerFill.GetComponent<Image>();
        TextRect("HP", healthFrame, new Vector2(0.5f, 1f), new Vector2(0f, 22f),
            new Vector2(150f, 36f), "HP", 28, TextAnchor.MiddleCenter);
        playerHealthText = TextRect("Player Health", healthFrame, new Vector2(0.5f, 0f), new Vector2(0f, -10f),
            new Vector2(180f, 28f), "", 22, TextAnchor.MiddleCenter);
        playerHealthText.rectTransform.pivot = new Vector2(0.5f, 1f);

        enemyPanel = ImageRect("Enemy Information", safeRoot, new Vector2(0.5f, 1f),
            new Vector2(0f, -enemyPanelTopOffset), enemyPanelSize, EnemyAccent);
        RectTransform enemyBody = ImageRect("Panel", enemyPanel, Vector2.zero, Vector2.one * 3f,
            enemyPanelSize - Vector2.one * 6f, PanelColor);
        float portraitSide = enemyPanelSize.y - 24f;
        RectTransform enemyPortraitRect = ImageRect("Enemy Portrait", enemyBody, Vector2.zero, new Vector2(9f, 9f),
            Vector2.one * portraitSide, Color.white);
        enemyPortrait = enemyPortraitRect.GetComponent<Image>();
        enemyPortrait.preserveAspect = true;
        float contentX = portraitSide + 26f;
        float contentWidth = enemyPanelSize.x - contentX - 18f;
        enemyNameText = TextRect("Enemy Name", enemyBody, Vector2.zero, new Vector2(contentX, 76f),
            new Vector2(contentWidth - 130f, 28f), "", 24, TextAnchor.MiddleLeft);
        enemyStatusText = TextRect("Health Status", enemyBody, Vector2.zero, new Vector2(enemyPanelSize.x - 147f, 76f),
            new Vector2(126f, 28f), "", 16, TextAnchor.MiddleRight);
        RectTransform enemyTrack = ImageRect("Enemy Health", enemyBody, Vector2.zero,
            new Vector2(contentX, 32f), new Vector2(contentWidth, 34f), new Color(0.18f, 0.12f, 0.21f));
        enemyFill = ImageRect("Fill", enemyTrack, Vector2.zero, Vector2.zero, Vector2.zero, Color.white);
        enemyFillImage = enemyFill.GetComponent<Image>();
        enemyHealthText = TextRect("Enemy HP", enemyTrack, Vector2.zero, Vector2.zero,
            new Vector2(contentWidth, 34f), "", 22, TextAnchor.MiddleCenter);
        enemyPanel.gameObject.SetActive(false);
    }

    private void UpdateSafeArea() {
        UnityEngine.Rect area = Screen.safeArea;
        if (Screen.width <= 0 || Screen.height <= 0) {
            return;
        }
        safeRoot.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
        safeRoot.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
        safeRoot.offsetMin = Vector2.zero;
        safeRoot.offsetMax = Vector2.zero;
    }

    private static void SetPortrait(Image image, Sprite sprite) {
        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    private static float HealthRatio(float current, float maximum) {
        return maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
    }

    private static Color HealthColor(float ratio) {
        return ratio > 0.5f ? new Color(0.3f, 0.85f, 0.5f) :
            ratio > 0.25f ? new Color(1f, 0.65f, 0.15f) : new Color(0.95f, 0.2f, 0.2f);
    }

    private static void SetHealth(RectTransform fill, Image image, Text label, float current, float maximum, bool vertical) {
        float ratio = HealthRatio(current, maximum);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = vertical ? new Vector2(1f, ratio) : new Vector2(ratio, 1f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        image.color = HealthColor(ratio);
        label.text = $"{Mathf.CeilToInt(Mathf.Max(0f, current))} / {Mathf.CeilToInt(maximum)}";
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size) {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static RectTransform ImageRect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color) {
        RectTransform rect = Rect(name, parent, anchor, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private Text TextRect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size,
        string text, int fontSize, TextAnchor alignment) {
        RectTransform rect = Rect(name, parent, anchor, position, size);
        Text label = rect.gameObject.AddComponent<Text>();
        label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = FontStyle.Bold;
        label.alignment = alignment;
        label.color = Color.white;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        Shadow shadow = rect.gameObject.AddComponent<Shadow>();
        shadow.effectColor = Color.black;
        shadow.effectDistance = new Vector2(1f, -1f);
        return label;
    }

    private void EnsureCircleSprite() {
        if (circleSprite != null) {
            return;
        }
        const int size = 128;
        circleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        circleTexture.name = "HUD Circle";
        circleTexture.wrapMode = TextureWrapMode.Clamp;
        circleTexture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * size * 0.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(size * 0.5f - distance));
            }
        }
        circleTexture.SetPixels(pixels);
        circleTexture.Apply(false, true);
        circleSprite = Sprite.Create(circleTexture, new UnityEngine.Rect(0f, 0f, size, size), Vector2.one * 0.5f);
    }

    private void OnDisable() {
        if (canvasObject != null) {
            canvasObject.SetActive(false);
        }
    }

    private void OnDestroy() {
        if (canvasObject != null) Destroy(canvasObject);
        if (circleSprite != null) Destroy(circleSprite);
        if (circleTexture != null) Destroy(circleTexture);
    }
}
