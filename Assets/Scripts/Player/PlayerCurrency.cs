using System;
using Unity.Netcode;
using UnityEngine;

public class PlayerCurrency : NetworkBehaviour {
    [SerializeField] private int startingCoins;

    public readonly NetworkVariable<int> Coins = new NetworkVariable<int>(
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server
    );

    public event Action<int, int> CoinsChanged;

    public override void OnNetworkSpawn() {
        Coins.OnValueChanged += HandleCoinsChanged;

        if (IsServer) {
            Coins.Value = Mathf.Max(0, startingCoins);
        }
    }

    public override void OnNetworkDespawn() {
        Coins.OnValueChanged -= HandleCoinsChanged;
    }

    public void ServerAddCoins(int amount) {
        if (!IsServer || amount <= 0) {
            return;
        }

        Coins.Value += amount;
    }

    public bool ServerTrySpendCoins(int amount) {
        if (!IsServer || amount <= 0 || Coins.Value < amount) {
            return false;
        }

        Coins.Value -= amount;
        return true;
    }

    private void HandleCoinsChanged(int previousValue, int newValue) {
        CoinsChanged?.Invoke(previousValue, newValue);
        Debug.Log($"[PlayerCurrency] Client {OwnerClientId} coins changed: {previousValue} -> {newValue}", this);
    }
}
