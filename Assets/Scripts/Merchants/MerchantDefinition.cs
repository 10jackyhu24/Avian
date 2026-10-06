using System;
using UnityEngine;

[Serializable]
public sealed class MerchantOffer {
    [Tooltip("Stable ItemObject itemId sold or purchased by this merchant.")]
    public string itemId;

    [Header("Buying from Merchant")]
    public bool merchantSellsThisItem = true;
    [Min(0)] public int buyPrice;

    [Header("Selling to Merchant")]
    public bool merchantBuysThisItem = true;
    [Min(0)] public int sellPrice;

    [Header("Stock")]
    public bool unlimitedStock = true;
    [Min(0)] public int startingStock;
}

[CreateAssetMenu(
    fileName = "NewMerchantDefinition",
    menuName = "Merchant/Merchant Definition"
)]
public sealed class MerchantDefinition : ScriptableObject {
    [Header("Identity")]
    [Tooltip("Stable identifier used by the server. Do not change after release.")]
    public string merchantId;
    public string displayName;

    [TextArea(2, 4)]
    public string greeting;

    [Header("Interaction")]
    [Min(0.1f)] public float interactionRadius = 4f;

    [Header("Catalogue")]
    public MerchantOffer[] offers = Array.Empty<MerchantOffer>();

    private void OnValidate() {
        interactionRadius = Mathf.Max(0.1f, interactionRadius);

        if (offers == null) {
            offers = Array.Empty<MerchantOffer>();
            return;
        }

        foreach (MerchantOffer offer in offers) {
            if (offer == null) {
                continue;
            }

            offer.buyPrice = Mathf.Max(0, offer.buyPrice);
            offer.sellPrice = Mathf.Max(0, offer.sellPrice);
            offer.startingStock = Mathf.Max(0, offer.startingStock);
        }
    }
}
