using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class MerchantMenu : MonoBehaviour {
    private const string PrefabPath = "UI/Merchant/Merchant_Main";

    private static MerchantMenu instance;

    private GameInput inputActions;
    private Merchant currentMerchant;
    private PlayerInventory localInventory;

    private GameObject mainPanel;
    private GameObject buyPanel;
    private GameObject sellPanel;
    private GameObject talkPanel;
    private TMP_Text titleText;
    private TMP_Text statusText;
    private Button[] offerButtons;
    private Button[] sellItemButtons;

    public static bool IsOpen => instance != null && instance.gameObject.activeSelf;

    public static void EnsureExists() {
        if (instance != null) {
            return;
        }

        GameObject prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null) {
            Debug.LogError($"[MerchantMenu] Could not load Resources/{PrefabPath}.");
            return;
        }

        GameObject menuObject = Instantiate(prefab);
        if (menuObject.GetComponent<MerchantMenu>() == null) {
            menuObject.AddComponent<MerchantMenu>();
        }
    }

    public static bool TryClose() {
        if (!IsOpen) {
            return false;
        }

        instance.Close();
        return true;
    }

    public static void ShowTransactionResult(string message, bool succeeded) {
        if (instance?.statusText == null) {
            return;
        }

        instance.statusText.text = message;
        instance.statusText.color = succeeded
            ? new Color(0.35f, 0.85f, 0.35f)
            : new Color(1f, 0.35f, 0.35f);
    }

    private void Awake() {
        if (instance != null && instance != this) {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (!FindUiReferences()) {
            Debug.LogError("[MerchantMenu] Merchant UI prefab is missing required objects.", this);
            gameObject.SetActive(false);
            return;
        }

        WireButtons();

        inputActions = new GameInput();
        inputActions.Player.Interact.performed += HandleInteract;
        KeyBinding.LoadBindingOverrides(inputActions);
        inputActions.Player.Enable();
        OptionsMenu.BindingOverridesChanged += ReloadBindings;

        ShowMainPanel();
        gameObject.SetActive(false);
    }

    private void OnDestroy() {
        UnbindLocalInventory();
        OptionsMenu.BindingOverridesChanged -= ReloadBindings;

        if (inputActions != null) {
            inputActions.Player.Interact.performed -= HandleInteract;
            inputActions.Disable();
            inputActions.Dispose();
        }

        if (instance == this) {
            instance = null;
        }
    }

    private bool FindUiReferences() {
        mainPanel = transform.Find("MainPanel")?.gameObject;
        buyPanel = transform.Find("BuyPanel")?.gameObject;
        sellPanel = transform.Find("SellPanel")?.gameObject;
        talkPanel = transform.Find("TalkPanel")?.gameObject;
        titleText = transform.Find("TextMeshPro_Title")?.GetComponent<TMP_Text>();
        statusText = transform.Find("TextMeshPro_Status")?.GetComponent<TMP_Text>();

        offerButtons = new[] {
            buyPanel?.transform.Find("OfferButton_1")?.GetComponent<Button>(),
            buyPanel?.transform.Find("OfferButton_2")?.GetComponent<Button>(),
            buyPanel?.transform.Find("OfferButton_3")?.GetComponent<Button>()
        };

        sellItemButtons = new[] {
            sellPanel?.transform.Find("SellItemButton_1")?.GetComponent<Button>(),
            sellPanel?.transform.Find("SellItemButton_2")?.GetComponent<Button>(),
            sellPanel?.transform.Find("SellItemButton_3")?.GetComponent<Button>()
        };

        return mainPanel != null &&
               buyPanel != null &&
               sellPanel != null &&
               talkPanel != null &&
               titleText != null &&
               statusText != null &&
               AllButtonsExist(offerButtons) &&
               AllButtonsExist(sellItemButtons);
    }

    private static bool AllButtonsExist(Button[] buttons) {
        foreach (Button button in buttons) {
            if (button == null) {
                return false;
            }
        }

        return true;
    }

    private void WireButtons() {
        mainPanel.transform.Find("Button_Buy").GetComponent<Button>().onClick.AddListener(ShowBuyPanel);

        Button sellButton = mainPanel.transform.Find("Button_Sell").GetComponent<Button>();
        Button talkButton = mainPanel.transform.Find("Button_Talk").GetComponent<Button>();
        sellButton.onClick.AddListener(ShowSellPanel);
        talkButton.interactable = false;

        mainPanel.transform.Find("Button_Close").GetComponent<Button>().onClick.AddListener(Close);
        buyPanel.transform.Find("Button_Back").GetComponent<Button>().onClick.AddListener(ShowMainPanel);
        sellPanel.transform.Find("Button_Back").GetComponent<Button>().onClick.AddListener(ShowMainPanel);
        talkPanel.transform.Find("Button_Back").GetComponent<Button>().onClick.AddListener(ShowMainPanel);
    }

    private void HandleInteract(InputAction.CallbackContext context) {
        if (!context.performed || !GameState.isPlaying || PauseMenu.isOpen || BagMenu.IsOpen) {
            return;
        }

        if (IsOpen) {
            Close();
            return;
        }

        Merchant merchant = FindNearestMerchant();
        if (merchant != null) {
            Open(merchant);
        }
    }

    private Merchant FindNearestMerchant() {
        NetworkObject localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (localPlayer == null) {
            return null;
        }

        Merchant nearestMerchant = null;
        float nearestDistanceSquared = float.MaxValue;

        foreach (Merchant merchant in FindObjectsByType<Merchant>(FindObjectsSortMode.None)) {
            if (!merchant.IsSpawned || merchant.Definition == null) {
                continue;
            }

            float distanceSquared =
                (merchant.transform.position - localPlayer.transform.position).sqrMagnitude;
            float interactionRadius = merchant.InteractionRadius;
            if (distanceSquared <= interactionRadius * interactionRadius &&
                distanceSquared < nearestDistanceSquared) {
                nearestMerchant = merchant;
                nearestDistanceSquared = distanceSquared;
            }
        }

        return nearestMerchant;
    }

    private void Open(Merchant merchant) {
        currentMerchant = merchant;
        BindLocalInventory();
        titleText.text = merchant.Definition.displayName;
        statusText.text = string.Empty;
        PopulateOffers();
        ShowMainPanel();
        gameObject.SetActive(true);
    }

    private void Close() {
        UnbindLocalInventory();
        currentMerchant = null;
        gameObject.SetActive(false);
    }

    private void ShowMainPanel() {
        ShowOnly(mainPanel);
    }

    private void ShowBuyPanel() {
        PopulateOffers();
        ShowOnly(buyPanel);
    }

    private void ShowSellPanel() {
        PopulateSellItems();
        ShowOnly(sellPanel);
    }

    private void ShowOnly(GameObject visiblePanel) {
        mainPanel.SetActive(visiblePanel == mainPanel);
        buyPanel.SetActive(visiblePanel == buyPanel);
        sellPanel.SetActive(visiblePanel == sellPanel);
        talkPanel.SetActive(visiblePanel == talkPanel);
        statusText.gameObject.SetActive(visiblePanel == buyPanel || visiblePanel == sellPanel);
    }

    private void PopulateOffers() {
        foreach (Button button in offerButtons) {
            button.onClick.RemoveAllListeners();
            button.gameObject.SetActive(false);
        }

        if (currentMerchant?.Definition?.offers == null) {
            return;
        }

        int buttonIndex = 0;
        foreach (MerchantOffer offer in currentMerchant.Definition.offers) {
            if (offer == null || !offer.merchantSellsThisItem || buttonIndex >= offerButtons.Length) {
                continue;
            }

            Button button = offerButtons[buttonIndex];
            string itemId = offer.itemId;
            button.GetComponentInChildren<TMP_Text>(true).text =
                $"{GetItemName(itemId)} - {offer.buyPrice} coins";
            button.onClick.AddListener(() => currentMerchant?.RequestPurchase(itemId));
            button.gameObject.SetActive(true);
            buttonIndex++;
        }
    }

    private void PopulateSellItems() {
        foreach (Button button in sellItemButtons) {
            button.onClick.RemoveAllListeners();
            button.interactable = true;
            button.gameObject.SetActive(false);
        }

        if (localInventory == null ||
            currentMerchant?.Definition?.offers == null) {
            return;
        }

        int buttonIndex = 0;
        for (int slotIndex = 0;
             slotIndex < localInventory.SlotCount && buttonIndex < sellItemButtons.Length;
             slotIndex++) {
            if (!localInventory.TryGetSlot(slotIndex, out InventorySlotState slot) || slot.IsEmpty) {
                continue;
            }

            string itemId = slot.Item.ItemId.ToString();
            if (!TryGetMerchantPurchaseOffer(itemId, out MerchantOffer offer)) {
                continue;
            }

            string countText = slot.Item.Count > 1 ? $" x{slot.Item.Count}" : string.Empty;
            Button button = sellItemButtons[buttonIndex];
            button.GetComponentInChildren<TMP_Text>(true).text =
                $"{GetItemName(itemId)}{countText} - sell for {offer.sellPrice} coins";
            ulong instanceId = slot.Item.InstanceId;
            button.onClick.AddListener(() => currentMerchant?.RequestSale(instanceId));
            button.gameObject.SetActive(true);
            buttonIndex++;
        }
    }

    private void BindLocalInventory() {
        UnbindLocalInventory();

        NetworkObject localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (localPlayer != null && localPlayer.TryGetComponent(out PlayerInventory inventory)) {
            localInventory = inventory;
            localInventory.InventoryChanged += HandleInventoryChanged;
        }
    }

    private void UnbindLocalInventory() {
        if (localInventory != null) {
            localInventory.InventoryChanged -= HandleInventoryChanged;
            localInventory = null;
        }
    }

    private void HandleInventoryChanged() {
        if (sellPanel.activeSelf) {
            PopulateSellItems();
        }
    }

    private bool TryGetMerchantPurchaseOffer(string itemId, out MerchantOffer matchingOffer) {
        matchingOffer = null;
        foreach (MerchantOffer offer in currentMerchant.Definition.offers) {
            if (offer != null && offer.merchantBuysThisItem && offer.itemId == itemId) {
                matchingOffer = offer;
                return true;
            }
        }

        return false;
    }

    private static string GetItemName(string itemId) {
        if (ItemManager.TryGetItemById(itemId, out ItemData itemData) &&
            itemData.itemObject != null &&
            !string.IsNullOrWhiteSpace(itemData.itemObject.itemName)) {
            return itemData.itemObject.itemName;
        }

        return itemId;
    }

    private void ReloadBindings() {
        if (inputActions != null) {
            KeyBinding.LoadBindingOverrides(inputActions);
        }
    }
}
