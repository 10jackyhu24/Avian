using TMPro;
using Unity.Netcode;
using UnityEngine;

public class CoinTextUI : MonoBehaviour {
    [SerializeField] private TMP_Text coinText;
    [SerializeField] private string labelFormat = "Coins: {0}";

    private PlayerCurrency observedCurrency;

    private void Awake() {
        if (coinText == null) {
            coinText = GetComponent<TMP_Text>();
        }

        UpdateLabel(0);
    }

    private void OnDisable() {
        Unsubscribe();
    }

    private void Update() {
        if (observedCurrency != null && observedCurrency.IsSpawned) {
            return;
        }

        TryBindLocalPlayerCurrency();
    }

    private void TryBindLocalPlayerCurrency() {
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsClient ||
            NetworkManager.Singleton.LocalClient == null ||
            NetworkManager.Singleton.LocalClient.PlayerObject == null) {
            return;
        }

        PlayerCurrency playerCurrency = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerCurrency>();
        if (playerCurrency == null || playerCurrency == observedCurrency) {
            return;
        }

        Unsubscribe();
        observedCurrency = playerCurrency;
        observedCurrency.CoinsChanged += HandleCoinsChanged;
        UpdateLabel(observedCurrency.Coins.Value);
    }

    private void HandleCoinsChanged(int previousValue, int newValue) {
        UpdateLabel(newValue);
    }

    private void UpdateLabel(int coins) {
        if (coinText != null) {
            coinText.text = string.Format(labelFormat, coins);
        }
    }

    private void Unsubscribe() {
        if (observedCurrency != null) {
            observedCurrency.CoinsChanged -= HandleCoinsChanged;
            observedCurrency = null;
        }
    }
}
