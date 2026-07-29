using UnityEngine;

public static class MoveObject {
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
}