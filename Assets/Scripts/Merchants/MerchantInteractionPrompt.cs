using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MerchantInteractionPrompt : MonoBehaviour {
    private static readonly string[] ChineseFontNames = {
        "Microsoft JhengHei", "Microsoft JhengHei UI", "PingFang TC",
        "Noto Sans CJK TC", "Noto Sans TC",
        "Source Han Sans TC", "WenQuanYi Micro Hei"
    };

    private RectTransform canvasRect;
    private RectTransform promptRect;
    private Text promptText;
    private Font dynamicFont;
    private string currentBinding;

    private void Awake() {
        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -40;
        canvasRect = GetComponent<RectTransform>();

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject backgroundObject = new GameObject(
            "Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)
        );
        promptRect = backgroundObject.GetComponent<RectTransform>();
        promptRect.SetParent(canvasRect, false);
        promptRect.anchorMin = promptRect.anchorMax = new Vector2(0.5f, 0.5f);
        promptRect.pivot = new Vector2(0.5f, 0.5f);
        promptRect.sizeDelta = new Vector2(300f, 48f);

        Image background = backgroundObject.GetComponent<Image>();
        background.color = new Color(0.04f, 0.04f, 0.05f, 0.68f);
        background.raycastTarget = false;

        GameObject textObject = new GameObject(
            "Prompt Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)
        );
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(promptRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 2f);
        textRect.offsetMax = new Vector2(-10f, -2f);

        promptText = textObject.GetComponent<Text>();
        promptText.font = FindChineseFont();
        promptText.fontSize = 24;
        promptText.alignment = TextAnchor.MiddleCenter;
        promptText.color = Color.white;
        promptText.raycastTarget = false;
        promptText.resizeTextForBestFit = true;
        promptText.resizeTextMinSize = 16;
        promptText.resizeTextMaxSize = 24;

        promptRect.gameObject.SetActive(false);
    }

    private void LateUpdate() {
        if (!GameState.isPlaying || PauseMenu.isOpen || BagMenu.IsOpen || MerchantMenu.IsOpen) {
            promptRect.gameObject.SetActive(false);
            return;
        }

        Merchant merchant = MerchantMenu.FindNearestMerchant();
        Camera camera = Camera.main;
        if (merchant == null || camera == null) {
            promptRect.gameObject.SetActive(false);
            return;
        }

        // The merchant placeholder is 1.5 units tall; this sits below its middle.
        Vector3 screenPoint = camera.WorldToScreenPoint(
            merchant.transform.position + new Vector3(0f, 0.42f, 0f)
        );
        if (screenPoint.z <= 0f || screenPoint.x < 0f || screenPoint.x > Screen.width ||
            screenPoint.y < 0f || screenPoint.y > Screen.height ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPoint, null, out Vector2 canvasPoint)) {
            promptRect.gameObject.SetActive(false);
            return;
        }

        string binding = MerchantMenu.GetInteractBindingDisplayString();
        if (binding != currentBinding) {
            currentBinding = binding;
            promptText.text = $"按 ({binding}) 互動";
        }

        promptRect.anchoredPosition = canvasPoint;
        promptRect.gameObject.SetActive(true);
    }

    private Font FindChineseFont() {
        string[] installedFonts = Font.GetOSInstalledFontNames();
        foreach (string preferredFont in ChineseFontNames) {
            foreach (string installedFont in installedFonts) {
                if (string.Equals(installedFont, preferredFont, System.StringComparison.OrdinalIgnoreCase)) {
                    dynamicFont = Font.CreateDynamicFontFromOSFont(installedFont, 24);
                    return dynamicFont;
                }
            }
        }

        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void OnDestroy() {
        if (dynamicFont != null) {
            Destroy(dynamicFont);
        }
    }
}
