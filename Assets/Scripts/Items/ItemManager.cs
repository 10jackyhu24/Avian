using System.Collections.Generic;
using UnityEngine;

public class ItemManager : MonoBehaviour {
    private Bag bag;
    
    public ItemData[] itemDatas;
    
    private void Start() {
        bag.BagInit();
    }
}
