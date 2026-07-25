using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Bag))]
public class ItemManager : MonoBehaviour {
    private Bag bag;
    public ItemData[] itemDatas;

    private void Awake() {
        bag = GetComponent<Bag>();
    }

    private void Start() {
        if (bag == null) {
            Debug.LogError("[ItemManager] Bag component not found in the scene.", this);
            return;
        }

        bag.BagInit();
    }
}
