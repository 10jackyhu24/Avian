using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using UnityEngine.InputSystem;

public class OptionsMenu : MonoBehaviour {
    public static event Action BindingOverridesChanged;

    [SerializeField] private GameObject keyBindPrefab;
    [SerializeField] private GameObject keyBindPrefabParent;

    // Define a simple structure to hold action information
    [System.Serializable]
    public struct KeyBindInfo {
        public string actionName; // e.g., "Move" or "Jump"
        public int bindingIndex;  // 0 for single keys; 1,2,3,4 for composites (Up, Down, Left, Right)
    }

    // Display name -> Input System action/binding location.
    private readonly Dictionary<string, KeyBindInfo> keyBindDict = new Dictionary<string, KeyBindInfo> {
        { "Move Up",   new KeyBindInfo { actionName = "Move", bindingIndex = 1 } },
        { "Move Down", new KeyBindInfo { actionName = "Move", bindingIndex = 2 } },
        { "Move Left", new KeyBindInfo { actionName = "Move", bindingIndex = 3 } },
        { "Move Right", new KeyBindInfo { actionName = "Move", bindingIndex = 4 } },
        { "Run", new KeyBindInfo { actionName = "Run", bindingIndex = 0 } },
        { "Attack", new KeyBindInfo { actionName = "Attack", bindingIndex = 0 } },
        { "Bag", new KeyBindInfo { actionName = "Bag", bindingIndex = 0 } },
        { "Interact", new KeyBindInfo { actionName = "Interact", bindingIndex = 0 } },
        { "Map", new KeyBindInfo { actionName = "Map", bindingIndex = 0 } },
        { "Item 1", new KeyBindInfo { actionName = "Item", bindingIndex = 0 } },
        { "Item 2", new KeyBindInfo { actionName = "Item", bindingIndex = 1 } },
        { "Item 3", new KeyBindInfo { actionName = "Item", bindingIndex = 2 } },
    };

    // Display name -> current key shown/used by the binding.
    public IReadOnlyDictionary<string, string> KeyBindings => keyBindings;

    private readonly Dictionary<string, string> keyBindings = new Dictionary<string, string>();
    private readonly List<BindingRow> bindingRows = new List<BindingRow>();
    private GameInput inputActions;
    private InputActionRebindingExtensions.RebindingOperation currentRebindOperation;

    [System.Serializable]
    private class BindingRow {
        public string displayName;
        public TMP_Text bindBtnText;
        public InputAction action;
        public int bindingIndex;
    }

    private void Awake() {
        inputActions = new GameInput();
        KeyBinding.LoadBindingOverrides(inputActions);
        RefreshKeyBindingCache();
    }

    private void OnEnable() {
        RefreshDisplayedBindings();
    }

    void Start() {
        foreach (var pair in keyBindDict) {
            string displayName = pair.Key;
            KeyBindInfo info = pair.Value;

            GameObject spawnPrefab = PrefabSpawn.SpawnInit(keyBindPrefab, keyBindPrefabParent.transform);
            if (spawnPrefab == null) continue;
            
            // Find UI Components using your utility
            TMP_Text keyNameText = spawnPrefab.FindComponentInChildren<TMP_Text>("Key_Name_Text");
            TMP_Text bindBtnText = spawnPrefab.FindComponentInChildren<TMP_Text>("Key_Bind_Text");
            Button keyBindButton = spawnPrefab.FindComponentInChildren<Button>("Key_Bind_Button");

            // 1. Set the label (e.g., "Move Up")
            if (keyNameText != null) {
                keyNameText.text = displayName;
            }

            // 2. Display current key string (e.g., "W", "Space")
            InputAction action = inputActions.FindAction(info.actionName);
            if (action != null) {
                // Show active key name on the button
                if (bindBtnText != null) {
                    bindBtnText.text = GetBindingDisplayString(action, info.bindingIndex);
                }
            }

            // 3. Add Click Listener for interactive rebinding
            if (keyBindButton != null && action != null) {
                keyBindButton.onClick.AddListener(() => {
                    StartRebind(action, info.bindingIndex, bindBtnText);
                });
            }

            bindingRows.Add(new BindingRow {
                displayName = displayName,
                bindBtnText = bindBtnText,
                action = action,
                bindingIndex = info.bindingIndex
            });
        }

        RefreshDisplayedBindings();
    }

    private void StartRebind(InputAction action, int bindingIndex, TMP_Text btnText) {
        currentRebindOperation?.Cancel();
        currentRebindOperation?.Dispose();

        bool wasEnabled = action.enabled;
        action.Disable();
        if (btnText != null) btnText.text = "Press Any Key...";

        currentRebindOperation = action.PerformInteractiveRebinding(bindingIndex)
            // .WithControlsExcluding("Mouse")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnComplete(operation => {
            // Save custom binding to disk
                KeyBinding.SaveBindingOverrides(inputActions);
                RefreshKeyBindingCache();
                RefreshDisplayedBindings();
                BindingOverridesChanged?.Invoke();

                // Update button UI text immediately
                if (btnText != null) {
                    btnText.text = GetBindingDisplayString(action, bindingIndex);
                }

                operation.Dispose();
                currentRebindOperation = null;
                if (wasEnabled) action.Enable();
            })
            .OnCancel(operation => {
                if (btnText != null) {
                    btnText.text = GetBindingDisplayString(action, bindingIndex);
                }

                operation.Dispose();
                currentRebindOperation = null;
                RefreshDisplayedBindings();
                if (wasEnabled) action.Enable();
            })
            .Start();
    }

    private void RefreshKeyBindingCache() {
        keyBindings.Clear();

        foreach (var pair in keyBindDict) {
            InputAction action = inputActions.FindAction(pair.Value.actionName);
            if (action == null) continue;

            keyBindings[pair.Key] = GetBindingDisplayString(action, pair.Value.bindingIndex);
        }
    }

    private string GetBindingDisplayString(InputAction action, int bindingIndex) {
        if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count) {
            return string.Empty;
        }

        return action.GetBindingDisplayString(bindingIndex);
    }

    private void RefreshDisplayedBindings() {
        if (bindingRows.Count == 0 || inputActions == null) {
            return;
        }

        foreach (BindingRow row in bindingRows) {
            if (row == null || row.action == null || row.bindBtnText == null) {
                continue;
            }

            row.bindBtnText.text = GetBindingDisplayString(row.action, row.bindingIndex);
        }
    }

    private void OnDestroy() {
        currentRebindOperation?.Cancel();
        currentRebindOperation?.Dispose();
        inputActions?.Disable();
        inputActions?.Dispose();
    }
}
