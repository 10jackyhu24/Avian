using UnityEngine;
using System.Collections.Generic;

public static class UIBindingUtility {
    public static T FindComponentInChildren<T>(this GameObject parent, string childName) where T : Component {
        if (parent == null) return null;

        T[] components = parent.GetComponentsInChildren<T>(true);
        foreach (var comp in components) {
            if (comp.gameObject.name == childName) {
                return comp;
            }
        }

        Debug.LogWarning($"[UIBindingUtility] could not find a child object named '{childName}' or the object does not have a {typeof(T).Name} component");
        return null;
    }

    public static GameObject FindChildObject(this GameObject parent, string childName) {
        if (parent == null) return null;

        Transform[] children = parent.GetComponentsInChildren<Transform>(true);
        foreach (var child in children) {
            if (child.name == childName) {
                return child.gameObject;
            }
        }

        Debug.LogWarning($"[UIBindingUtility] could not find a child GameObject named '{childName}'");
        return null;
    }

    public static List<GameObject> GetChildrenObjects<T>(T parent, bool includeInactive = true) where T : Object {
        List<GameObject> result = new List<GameObject>();
        Transform parentTransform = GetTransform(parent);

        if (parentTransform == null) {
            Debug.LogError("[TransformUtility] Parent object is null or missing Transform!");
            return result;
        }

        foreach (Transform child in parentTransform) {
            if (includeInactive || child.gameObject.activeSelf) {
                result.Add(child.gameObject);
            }
        }

        return result;
    }

    private static Transform GetTransform(Object unityObj) {
        if (unityObj == null) return null;
        if (unityObj is GameObject go) return go.transform;
        if (unityObj is Component comp) return comp.transform;
        return null;
    }
}
