using UnityEngine;

public class SelectCover : MonoBehaviour {
    [SerializeField] GameObject coverPrefab;
    GameObject coverObject;

    public void Init(GameObject parent) {
        coverObject = PrefabSpawn.SpawnInit(coverPrefab, parent.transform);
        Hide();
    }

    public void Show() {
        coverObject.SetActive(true);
    }

    public void Hide() {
        coverObject.SetActive(false);
    }
}