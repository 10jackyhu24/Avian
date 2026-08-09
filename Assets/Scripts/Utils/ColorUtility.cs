using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class ColorUtility {
    #region Set Alpha Only

    /// <summary>
    /// Sets only the alpha (transparency) of a target object without changing its RGB color.
    /// </summary>
    /// <typeparam name="T">Supported types: Image, Text, TextMeshProUGUI, SpriteRenderer, CanvasGroup, Renderer, Graphic, GameObject</typeparam>
    /// <param name="target">The target component or GameObject.</param>
    /// <param name="alpha">Alpha value between 0 (transparent) and 1 (opaque).</param>
    public static void SetAlpha<T>(T target, float alpha) where T : Object {
        if (target == null) {
            Debug.LogError("[ColorUtility] Target object is null!");
            return;
        }

        alpha = Mathf.Clamp01(alpha);

        // 1. UI CanvasGroup (Best for whole UI fade)
        if (TryGetTarget<CanvasGroup>(target, out var canvasGroup)) {
            canvasGroup.alpha = alpha;
            return;
        }

        // 2. UI Graphic (Image, Text, RawImage, etc.)
        if (TryGetTarget<Graphic>(target, out var graphic)) {
            Color c = graphic.color;
            c.a = alpha;
            graphic.color = c;
            return;
        }

        // 3. TextMeshPro
        if (TryGetTarget<TMP_Text>(target, out var tmpText)) {
            Color c = tmpText.color;
            c.a = alpha;
            tmpText.color = c;
            return;
        }

        // 4. 2D SpriteRenderer
        if (TryGetTarget<SpriteRenderer>(target, out var spriteRenderer)) {
            Color c = spriteRenderer.color;
            c.a = alpha;
            spriteRenderer.color = c;
            return;
        }

        // 5. 3D Renderer (Material)
        if (TryGetTarget<Renderer>(target, out var renderer)) {
            Color c = renderer.material.color;
            c.a = alpha;
            renderer.material.color = c;
            return;
        }

        Debug.LogWarning($"[ColorUtility] Unsupported target type or component missing on '{target.name}'.");
    }

    #endregion

    #region Set Full Color (RGB + Alpha)

    /// <summary>
    /// Sets the full color (RGB and Alpha) of a target object.
    /// </summary>
    public static void SetColor<T>(T target, Color color) where T : Object {
        if (target == null) {
            Debug.LogError("[ColorUtility] Target object is null!");
            return;
        }

        // 1. UI Graphic (Image, Text, etc.)
        if (TryGetTarget<Graphic>(target, out var graphic)) {
            graphic.color = color;
            return;
        }

        // 2. TextMeshPro
        if (TryGetTarget<TMP_Text>(target, out var tmpText)) {
            tmpText.color = color;
            return;
        }

        // 3. 2D SpriteRenderer
        if (TryGetTarget<SpriteRenderer>(target, out var spriteRenderer)) {
            spriteRenderer.color = color;
            return;
        }

        // 4. 3D Renderer (Material)
        if (TryGetTarget<Renderer>(target, out var renderer)) {
            renderer.material.color = color;
            return;
        }

        Debug.LogWarning($"[ColorUtility] Unsupported target type or component missing on '{target.name}'.");
    }

    #endregion

    #region Helper Methods

    // Helper method to extract component from either Component or GameObject
    private static bool TryGetTarget<TComponent>(Object unityObj, out TComponent result) where TComponent : Component {
        result = null;
        if (unityObj is GameObject go) {
            result = go.GetComponent<TComponent>();
        } else if (unityObj is Component comp) {
            result = comp is TComponent targetComp ? targetComp : comp.GetComponent<TComponent>();
        }
        return result != null;
    }

    #endregion
}