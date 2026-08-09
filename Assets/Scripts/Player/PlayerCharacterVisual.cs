using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Animator))]
public class PlayerCharacterVisual : NetworkBehaviour {
    [Serializable]
    private class CharacterVisualOption {
        public string characterId;
        public Sprite sprite;
        public RuntimeAnimatorController animatorController;
    }

    [SerializeField] private string defaultCharacterId = "bird_01";
    [SerializeField] private CharacterVisualOption[] visualOptions;

    private readonly NetworkVariable<FixedString64Bytes> syncedCharacterId = new NetworkVariable<FixedString64Bytes>(
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server
    );

    private SpriteRenderer spriteRenderer;
    private Animator animator;

    private void Awake() {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    public override void OnNetworkSpawn() {
        syncedCharacterId.OnValueChanged += HandleCharacterChanged;

        if (IsServer && syncedCharacterId.Value.Length == 0) {
            syncedCharacterId.Value = defaultCharacterId;
        }

        ApplyCharacterVisual(syncedCharacterId.Value.ToString());

        if (IsOwner && !string.IsNullOrWhiteSpace(GameState.selectedCharacterId)) {
            SubmitCharacterSelectionServerRpc(GameState.selectedCharacterId);
        }
    }

    public override void OnNetworkDespawn() {
        syncedCharacterId.OnValueChanged -= HandleCharacterChanged;
    }

    [ServerRpc]
    private void SubmitCharacterSelectionServerRpc(FixedString64Bytes characterId) {
        if (characterId.Length == 0) {
            return;
        }

        syncedCharacterId.Value = characterId;
    }

    private void HandleCharacterChanged(FixedString64Bytes previousValue, FixedString64Bytes newValue) {
        ApplyCharacterVisual(newValue.ToString());
    }

    private void ApplyCharacterVisual(string characterId) {
        CharacterVisualOption option = FindVisualOption(characterId);
        if (option == null) {
            Debug.LogWarning($"[PlayerCharacterVisual] No visual option found for character id '{characterId}'.", this);
            return;
        }

        if (option.sprite != null) {
            spriteRenderer.sprite = option.sprite;
        }

        if (option.animatorController != null) {
            animator.runtimeAnimatorController = option.animatorController;
        }
    }

    private CharacterVisualOption FindVisualOption(string characterId) {
        if (visualOptions == null || visualOptions.Length == 0) {
            return null;
        }

        foreach (CharacterVisualOption option in visualOptions) {
            if (option != null && option.characterId == characterId) {
                return option;
            }
        }

        foreach (CharacterVisualOption option in visualOptions) {
            if (option != null && option.characterId == defaultCharacterId) {
                return option;
            }
        }

        return visualOptions[0];
    }
}
