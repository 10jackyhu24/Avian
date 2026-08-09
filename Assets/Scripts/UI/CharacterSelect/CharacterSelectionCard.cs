using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public class CharacterSelectionCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private string characterId = "bird_01";
    [SerializeField] private string characterName = "Bird";
    [TextArea]
    [SerializeField] private string description = "A balanced explorer.";
    [SerializeField] private CharacterSelectionMenu selectionMenu;

    private Button button;

    private void Awake() {
        button = GetComponent<Button>();
        if (selectionMenu == null) {
            selectionMenu = GetComponentInParent<CharacterSelectionMenu>();
        }

        if (button != null) {
            button.onClick.RemoveListener(SelectCharacter);
            button.onClick.AddListener(SelectCharacter);
        }
    }

    private void OnDestroy() {
        if (button != null) {
            button.onClick.RemoveListener(SelectCharacter);
        }
    }

    public void OnPointerEnter(PointerEventData eventData) {
        if (selectionMenu != null) {
            selectionMenu.PreviewCharacter(characterName, description);
        }
    }

    public void OnPointerExit(PointerEventData eventData) {
        if (selectionMenu != null) {
            selectionMenu.ClearPreview();
        }
    }

    private void SelectCharacter() {
        if (selectionMenu != null) {
            selectionMenu.SelectCharacter(characterId, characterName);
        }
    }
}
