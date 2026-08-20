using UnityEngine;

public class Pickupable : MonoBehaviour {
    [SerializeField] private ItemObject itemObject;

    public ItemObject ItemObject => itemObject;
    public bool HasItem => itemObject != null;

    private void Reset() {
        // Intentionally left blank; ItemObject is a ScriptableObject and should be assigned manually.
    }
}
