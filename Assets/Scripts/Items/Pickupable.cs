using UnityEngine;

public class Pickupable : MonoBehaviour {
    [SerializeField] private ItemObject itemObject;
    [SerializeField, Min(1)] private int count = 1;

    public ItemObject ItemObject => itemObject;
    public bool HasItem => itemObject != null;
    public int Count => count;

    public void SetCount(int value) {
        count = Mathf.Max(1, value);
    }

    private void Reset() {
        // Intentionally left blank; ItemObject is a ScriptableObject and should be assigned manually.
    }
}
