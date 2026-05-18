using UnityEngine;

public class CreateWorldOptions : MonoBehaviour {
    private MenuManager menuManager;

    private void Awake() {
        menuManager = FindFirstObjectByType<MenuManager>();
    }
}