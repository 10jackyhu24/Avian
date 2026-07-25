using UnityEngine;
using UnityEngine.InputSystem;

public static class KeyBinding {
    public const string BindingOverridesPrefsKey = "InputBindingOverrides";

    public static void LoadBindingOverrides(GameInput inputActions) {
        if (inputActions == null) {
            return;
        }

        string overridesJson = PlayerPrefs.GetString(BindingOverridesPrefsKey, string.Empty);
        if (string.IsNullOrEmpty(overridesJson)) {
            return;
        }

        bool wasEnabled = inputActions.Player.enabled;
        if (wasEnabled) {
            inputActions.Player.Disable();
        }

        inputActions.asset.LoadBindingOverridesFromJson(overridesJson);

        if (wasEnabled) {
            inputActions.Player.Enable();
        }
    }

    public static void SaveBindingOverrides(GameInput inputActions) {
        if (inputActions == null) {
            return;
        }

        PlayerPrefs.SetString(BindingOverridesPrefsKey, inputActions.asset.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
    }
}
