using UnityEngine;

public class Quit_Game : MonoBehaviour
{
    public void OnButtonClick() {
        Debug.Log("Quit Game");
        Application.Quit();
    }
}
