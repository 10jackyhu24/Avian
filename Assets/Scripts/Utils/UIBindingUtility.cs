using UnityEngine;

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
}