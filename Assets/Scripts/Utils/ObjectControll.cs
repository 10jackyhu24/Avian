using UnityEngine;
using System.Collections.Generic;

public static class ObjectControll {
    public static void MoveToIndex<T>(T component, int targetIndex) where T : Component {
        if (component == null) {
            Debug.LogError("[MoveObject] Target component is null!");
            return;
        }

        MoveToIndex(component.gameObject, targetIndex);
    }

    public static void MoveToIndex(GameObject obj, int targetIndex) {
        if (obj == null) {
            Debug.LogError("[MoveObject] Target GameObject is null!");
            return;
        }

        Transform parentTransform = obj.transform.parent;
        if (parentTransform == null) {
            Debug.LogWarning("[MoveObject] Target GameObject does not have a parent!");
            return;
        }

        // Clamp targetIndex between 0 and parent's childCount - 1
        int childCount = parentTransform.childCount;
        int clampedIndex = Mathf.Clamp(targetIndex, 0, childCount - 1);

        // Move the object to the calculated sibling index
        obj.transform.SetSiblingIndex(clampedIndex);
    }

    public static int GetIndex<T>(T component, bool includeInactive = false) where T : Object {
        if (component == null) {
            Debug.LogError("[MoveObject] Target component is null!");
            return -1;
        }

        GameObject targetObj = GetGameObject(component);
        return GetIndex(targetObj, includeInactive);
    }

    public static int GetIndex(GameObject obj, bool includeInactive = false) {
        if (obj == null) {
            Debug.LogError("[MoveObject] Target GameObject is null!");
            return -1;
        }

        Transform parentTransform = obj.transform.parent;
        if (parentTransform == null) {
            Debug.LogWarning("[MoveObject] Target object does not have a parent!");
            return 0;
        }

        // If including inactive objects, use Unity's native GetSiblingIndex directly
        if (includeInactive) {
            return obj.transform.GetSiblingIndex();
        }

        // Calculate index by counting active children only
        int activeIndex = 0;
        for (int i = 0; i < parentTransform.childCount; i++) {
            Transform child = parentTransform.GetChild(i);

            // Skip inactive children
            if (!child.gameObject.activeSelf) {
                continue;
            }

            if (child.gameObject == obj) {
                return activeIndex;
            }

            activeIndex++;
        }

        return -1;
    }

    private static GameObject GetGameObject(Object unityObj) {
        if (unityObj == null) return null;
        if (unityObj is GameObject go) return go;
        if (unityObj is Component comp) return comp.gameObject;
        return null;
    }

    public static void MatchParentSize<T>(T target) where T : Object {
        if (target == null) {
            Debug.LogError("[UIUtility] Target object is null!");
            return;
        }

        RectTransform rectTransform = GetRectTransform(target);
        if (rectTransform == null) {
            Debug.LogWarning($"[UIUtility] Target '{target.name}' does not have a RectTransform!");
            return;
        }

        if (rectTransform.parent == null) {
            Debug.LogWarning($"[UIUtility] Target '{target.name}' does not have a parent!");
            return;
        }

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;

        rectTransform.offsetMin = Vector2.zero; // Left, Bottom
        rectTransform.offsetMax = Vector2.zero; // Right, Top

        rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    private static RectTransform GetRectTransform(Object unityObj) {
        if (unityObj == null) return null;
        if (unityObj is GameObject go) return go.GetComponent<RectTransform>();
        if (unityObj is Component comp) return comp.GetComponent<RectTransform>();
        return null;
    }

    public static void ClearChildren<T>(T parent, string targetName = null, bool immediate = false) where T : Object {
        Transform parentTransform = GetTransform(parent);

        if (parentTransform == null) {
            Debug.LogError("[TransformUtility] Parent object is null or missing Transform!");
            return;
        }

        // Collect matching child GameObjects to avoid index mismatch during destruction
        List<GameObject> childrenToDestroy = new List<GameObject>();
        bool filterByName = !string.IsNullOrEmpty(targetName);

        foreach (Transform child in parentTransform) {
            if (!filterByName || child.name.Equals(targetName)) {
                childrenToDestroy.Add(child.gameObject);
            }
        }

        // Destroy collected child GameObjects
        foreach (GameObject child in childrenToDestroy) {
            if (immediate) {
                Object.DestroyImmediate(child);
            } else {
                Object.Destroy(child);
            }
        }
    }

    private static Transform GetTransform(Object unityObj) {
        if (unityObj == null) return null;
        if (unityObj is GameObject go) return go.transform;
        if (unityObj is Component comp) return comp.transform;
        return null;
    }
}