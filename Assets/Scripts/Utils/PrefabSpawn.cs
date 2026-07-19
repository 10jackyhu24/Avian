using System;
using UnityEngine;

public static class PrefabSpawn {
    public static GameObject SpawnInit(GameObject prefab, Transform parent = null) {
        if (prefab == null) {
            Debug.LogError("[PrefabUtility] Target prefab is null!");
            return null;
        }
        GameObject spawnedInstance = UnityEngine.Object.Instantiate(prefab, parent);
        spawnedInstance.transform.localPosition = Vector3.zero;
        spawnedInstance.transform.localRotation = Quaternion.identity;
        return spawnedInstance;
    }
}