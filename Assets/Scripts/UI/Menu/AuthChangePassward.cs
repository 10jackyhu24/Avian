using TMPro;
using UnityEngine;

public class AuthChangePassward : MonoBehaviour {
    [Header("Input")]
    [SerializeField] private TMP_InputField oldpasswordInput;
    [SerializeField] private TMP_InputField newpasswordInput;
    [SerializeField] private TMP_InputField newpasswardCheck;

    private MenuManager menuManager;
    private AuthMenu authMenu;

    private void Awake() {
        menuManager = FindFirstObjectByType<MenuManager>();
        authMenu = FindFirstObjectByType<AuthMenu>();
        ResolveInputFields();
        if (oldpasswordInput != null) oldpasswordInput.contentType = TMP_InputField.ContentType.Password;
        if (newpasswordInput != null) newpasswordInput.contentType = TMP_InputField.ContentType.Password;
        if (newpasswardCheck != null) newpasswardCheck.contentType = TMP_InputField.ContentType.Password;
    }

    private void OnDisable() {
        oldpasswordInput?.SetTextWithoutNotify(string.Empty);
        newpasswordInput?.SetTextWithoutNotify(string.Empty);
        newpasswardCheck?.SetTextWithoutNotify(string.Empty);
    }

    public void DoChangePassword() {
        ResolveInputFields();
        if (authMenu == null) {
            authMenu = FindFirstObjectByType<AuthMenu>(FindObjectsInactive.Include);
        }
        if (authMenu == null || oldpasswordInput == null || newpasswordInput == null || newpasswardCheck == null) {
            Debug.LogError($"Password change setup is incomplete: AuthMenu={authMenu != null}, OldPassword_Input={oldpasswordInput != null}, NewPassword_Input={newpasswordInput != null}, NewPassword_Check={newpasswardCheck != null}.", this);
            return;
        }
        authMenu.SetChangePasswordInput(oldpasswordInput, newpasswordInput, newpasswardCheck);
        authMenu.ChangePassward();
    }

    private void ResolveInputFields() {
        if (oldpasswordInput != null && newpasswordInput != null && newpasswardCheck != null) return;
        foreach (TMP_InputField field in GetComponentsInChildren<TMP_InputField>(true)) {
            if (oldpasswordInput == null && field.name == "OldPassword_Input") oldpasswordInput = field;
            if (newpasswordInput == null && field.name == "NewPassword_Input") newpasswordInput = field;
            if (newpasswardCheck == null && field.name == "NewPassword_Check") newpasswardCheck = field;
        }
    }

    public void Back() {
        menuManager?.Back();
    }
}
