using System;
using UnityEngine;

public static class PrefabSpawn {
    public static T SpawnInit<T>(T prefab, Transform parent = null) where T : Component {
        if (prefab == null) {
            Debug.LogError("[PrefabUtility] Target prefab is null!");
            return null;
        }

        GameObject spawnedObj = SpawnInit(prefab.gameObject, parent);
        return spawnedObj != null ? spawnedObj.GetComponent<T>() : null;
    }

    public static GameObject SpawnInit(GameObject prefab, Transform parent = null) {
        if (prefab == null) {
            Debug.LogError("[PrefabUtility] Target prefab is null!");
            return null;
        }

        GameObject spawnedInstance = UnityEngine.Object.Instantiate(prefab);

        if (parent != null) {
            spawnedInstance.transform.SetParent(parent, false);
        }

        spawnedInstance.transform.localPosition = Vector3.zero;
        spawnedInstance.transform.localRotation = Quaternion.identity;
        spawnedInstance.transform.localScale = Vector3.one;

        RectTransform rectTransform = spawnedInstance.GetComponent<RectTransform>();
        if (rectTransform != null) {
            rectTransform.anchoredPosition3D = Vector3.zero;
            rectTransform.localScale = Vector3.one;
        }

        return spawnedInstance;
    }
}
