using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class ShopItemData
{
    public string itemId;
    public int buyPrice;
    public int stock = -1;
    public int minDay;
    public string unlockFlag;
}

[Serializable]
public class ShopData
{
    public string id;
    public string name;
    public List<ShopItemData> goods = new List<ShopItemData>();
}

[Serializable]
public class ShopDataList
{
    public List<ShopData> shops = new List<ShopData>();
}

/// <summary>
/// 商店购买与出售界面。
/// 第一版只做单个物品购买/出售，有限库存写入 PlayerState，卖给商店的物品不回流到货架。
/// </summary>
public class ShopManager : MonoBehaviour
{
    public static bool IsShopOpen { get; private set; }

    [SerializeField] private string shopDataResourcePath = "Data/shops";

    private readonly Dictionary<string, ShopData> shopById = new Dictionary<string, ShopData>();
    private readonly List<GameObject> backpackRows = new List<GameObject>();
    private readonly List<GameObject> goodsRows = new List<GameObject>();

    private GameManager gameManager;
    private InventoryManager inventoryManager;
    private LocationUIManager locationUIManager;
    private LocationActionManager locationActionManager;

    private GameObject panelObject;
    private GameObject confirmPanelObject;
    private RectTransform backpackContentRect;
    private RectTransform goodsContentRect;
    private Text titleText;
    private Text stoneText;
    private Text tipText;
    private Text emptyBackpackText;
    private Text emptyGoodsText;
    private Text confirmText;
    private Font cachedFont;
    private ShopData currentShop;
    private Action confirmAction;
    private Action cancelAction;

    private const float PanelWidth = 1120f;
    private const float PanelHeight = 720f;
    private const float RowHeight = 142f;
    private const float RowGap = 10f;

    private void Start()
    {
        BindReferences();
        InventoryItemDatabase.EnsureLoaded();
        LoadShops();
        EnsurePanel();
        HideImmediately();
    }

    private void Update()
    {
        if (IsShopOpen && Input.GetKeyDown(KeyCode.Escape)) CloseShop();
    }

    private void BindReferences()
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        if (inventoryManager == null) inventoryManager = GetComponent<InventoryManager>();
        if (locationUIManager == null) locationUIManager = GetComponent<LocationUIManager>();
        if (locationActionManager == null) locationActionManager = GetComponent<LocationActionManager>();
    }

    public void LoadShops()
    {
        shopById.Clear();
        TextAsset jsonAsset = Resources.Load<TextAsset>(shopDataResourcePath);
        if (jsonAsset == null)
        {
            Debug.LogError("找不到商店数据：Resources/" + shopDataResourcePath + ".json");
            return;
        }

        ShopDataList dataList = JsonUtility.FromJson<ShopDataList>(jsonAsset.text);
        if (dataList == null || dataList.shops == null)
        {
            Debug.LogError("商店数据格式不正确：" + shopDataResourcePath);
            return;
        }

        foreach (ShopData shop in dataList.shops)
        {
            if (shop == null || string.IsNullOrEmpty(shop.id)) continue;
            shopById[shop.id] = shop;
        }
    }

    public void OpenShop(string shopId)
    {
        BindReferences();
        InventoryItemDatabase.EnsureLoaded();
        if (shopById.Count == 0) LoadShops();

        if (string.IsNullOrEmpty(shopId)) shopId = "qingshi_shop";
        if (!shopById.TryGetValue(shopId, out currentShop))
        {
            ShowMessage("找不到商店：" + shopId);
            return;
        }

        CloseOtherPanels();
        EnsurePanel();
        if (panelObject == null) return;

        IsShopOpen = true;
        panelObject.SetActive(true);
        panelObject.transform.SetAsLastSibling();
        HideConfirm();
        Refresh();
    }

    public void CloseShop()
    {
        HideImmediately();
        if (locationActionManager != null) locationActionManager.RefreshCurrentLocation();
    }

    public void HideImmediately()
    {
        IsShopOpen = false;
        currentShop = null;
        confirmAction = null;
        cancelAction = null;
        if (confirmPanelObject != null) confirmPanelObject.SetActive(false);
        if (panelObject != null) panelObject.SetActive(false);
    }

    public void RefreshIfOpen()
    {
        if (IsShopOpen && panelObject != null && panelObject.activeSelf) Refresh();
    }

    public void Refresh()
    {
        BindReferences();
        if (currentShop == null || panelObject == null) return;

        PlayerState state = GetState();
        if (state == null) return;
        state.EnsureLists();

        if (titleText != null) titleText.text = currentShop.name;
        if (stoneText != null) stoneText.text = "灵石：" + state.spiritStones;

        RefreshBackpack(state);
        RefreshGoods(state);
        RefreshTopStatus(state);
    }

    private void RefreshBackpack(PlayerState state)
    {
        ClearRows(backpackRows);
        int index = 0;

        if (state.inventoryItems != null)
        {
            for (int i = 0; i < state.inventoryItems.Count; i++)
            {
                InventoryItemRecord record = state.inventoryItems[i];
                if (record == null || string.IsNullOrEmpty(record.id) || record.count <= 0) continue;
                CreateBackpackRow(record, index);
                index++;
            }
        }

        if (emptyBackpackText != null) emptyBackpackText.gameObject.SetActive(index == 0);
        if (backpackContentRect != null)
        {
            backpackContentRect.sizeDelta = new Vector2(0f, index > 0 ? Mathf.Max(420f, index * (RowHeight + RowGap) + 20f) : 420f);
            backpackContentRect.anchoredPosition = Vector2.zero;
        }
    }

    private void RefreshGoods(PlayerState state)
    {
        ClearRows(goodsRows);
        int index = 0;

        if (currentShop.goods != null)
        {
            for (int i = 0; i < currentShop.goods.Count; i++)
            {
                ShopItemData goods = currentShop.goods[i];
                if (goods == null || string.IsNullOrEmpty(goods.itemId)) continue;
                if (!IsGoodsVisible(goods, state)) continue;
                CreateGoodsRow(goods, index, state);
                index++;
            }
        }

        if (emptyGoodsText != null) emptyGoodsText.gameObject.SetActive(index == 0);
        if (goodsContentRect != null)
        {
            goodsContentRect.sizeDelta = new Vector2(0f, index > 0 ? Mathf.Max(420f, index * (RowHeight + RowGap) + 20f) : 420f);
            goodsContentRect.anchoredPosition = Vector2.zero;
        }
    }

    private bool IsGoodsVisible(ShopItemData goods, PlayerState state)
    {
        if (goods == null || state == null) return false;
        if (goods.minDay > 0 && state.day < goods.minDay) return false;
        if (!string.IsNullOrEmpty(goods.unlockFlag) && !state.HasFlag(goods.unlockFlag)) return false;
        return true;
    }

    public void BuyItem(ShopItemData goods)
    {
        PlayerState state = GetState();
        if (state == null || goods == null) return;

        InventoryItemData item = InventoryItemDatabase.GetItem(goods.itemId);
        if (item == null)
        {
            ShowShopTip("掌柜翻了翻账本：“这货物记错了。”");
            return;
        }

        if (!IsGoodsVisible(goods, state))
        {
            ShowShopTip("掌柜摇头：“这东西现在还不能卖。”");
            return;
        }

        int remainingStock = state.GetShopStock(currentShop.id, goods.itemId, goods.stock);
        if (remainingStock == 0)
        {
            ShowShopTip("掌柜摊手道：“这东西已经卖完了。”");
            Refresh();
            return;
        }

        if (state.spiritStones < goods.buyPrice)
        {
            ShowShopTip("掌柜摇摇头：“小兄弟，灵石不够啊。”");
            return;
        }

        CurrencyManager.SpendSpiritStones(state, goods.buyPrice);
        if (inventoryManager != null) inventoryManager.AddItem(goods.itemId, 1);
        else state.AddItem(goods.itemId, 1);

        if (goods.stock > 0)
        {
            state.SetShopStock(currentShop.id, goods.itemId, Mathf.Max(0, remainingStock - 1));
        }

        Refresh();
        ToastManager.TryShowSuccess("购买成功：" + item.name);
        ShowShopTip("你花费 " + goods.buyPrice + " 灵石，买下" + item.name + "。");
    }

    public void RequestSellItem(string itemId)
    {
        PlayerState state = GetState();
        if (state == null || string.IsNullOrEmpty(itemId)) return;

        InventoryItemData item = InventoryItemDatabase.GetItem(itemId);
        if (item == null)
        {
            ShowShopTip("找不到物品数据：" + itemId);
            return;
        }

        if (!state.HasItem(itemId))
        {
            ShowShopTip("背包中没有该物品。");
            Refresh();
            return;
        }

        if (!item.sellable)
        {
            ShowShopTip(item.name + "不可出售。");
            return;
        }

        if (!string.IsNullOrEmpty(state.equippedWeaponId) && state.equippedWeaponId == itemId)
        {
            ShowShopTip("该物品正在装备中，请先卸下再出售。");
            return;
        }

        ShowConfirm("确认要出售" + item.name + "？", delegate { SellItem(itemId); }, null);
    }

    public void SellItem(string itemId)
    {
        PlayerState state = GetState();
        if (state == null || string.IsNullOrEmpty(itemId)) return;

        InventoryItemData item = InventoryItemDatabase.GetItem(itemId);
        if (item == null)
        {
            ShowShopTip("找不到物品数据：" + itemId);
            return;
        }

        if (!state.HasItem(itemId))
        {
            ShowShopTip("背包中没有该物品。");
            Refresh();
            return;
        }

        if (!item.sellable)
        {
            ShowShopTip(item.name + "不可出售。");
            return;
        }

        if (!string.IsNullOrEmpty(state.equippedWeaponId) && state.equippedWeaponId == itemId)
        {
            ShowShopTip("该物品正在装备中，请先卸下再出售。");
            return;
        }

        if (inventoryManager != null) inventoryManager.RemoveItem(itemId, 1);
        else state.RemoveItem(itemId, 1);
        CurrencyManager.AddSpiritStones(state, Mathf.Max(0, item.sellPrice));

        Refresh();
        ToastManager.TryShowSuccess("出售成功：" + item.name);
        ShowShopTip("你出售了" + item.name + "，获得 " + item.sellPrice + " 灵石。");
    }

    private void EnsurePanel()
    {
        if (panelObject != null) return;
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        panelObject = new GameObject("ShopModalOverlay", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvas.transform, false);
        RectTransform overlayRect = panelObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        Image overlayImage = panelObject.GetComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.42f);
        overlayImage.raycastTarget = true;

        GameObject boxObject = new GameObject("ShopPanel", typeof(RectTransform), typeof(Image));
        boxObject.transform.SetParent(panelObject.transform, false);
        RectTransform boxRect = boxObject.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        boxRect.anchoredPosition = Vector2.zero;
        boxObject.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 0.99f);

        titleText = CreateText(boxObject.transform, "青石村商铺", 30, TextAnchor.MiddleCenter, Color.white);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(0f, 54f);
        titleRect.anchoredPosition = new Vector2(0f, -14f);

        stoneText = CreateText(boxObject.transform, "灵石：0", 20, TextAnchor.MiddleLeft, new Color(0.95f, 0.86f, 0.45f, 1f));
        RectTransform stoneRect = stoneText.rectTransform;
        stoneRect.anchorMin = new Vector2(0f, 1f);
        stoneRect.anchorMax = new Vector2(0f, 1f);
        stoneRect.pivot = new Vector2(0f, 1f);
        stoneRect.sizeDelta = new Vector2(220f, 38f);
        stoneRect.anchoredPosition = new Vector2(36f, -24f);

        Button closeButton = CreateButton(boxObject.transform, "关闭", new Vector2(-36f, -24f), new Vector2(96f, 38f), AnchorMode.TopRight, true);
        closeButton.onClick.AddListener(CloseShop);

        CreateColumn(boxObject.transform, "我的背包", new Vector2(36f, 82f), true);
        CreateColumn(boxObject.transform, "青石村商铺", new Vector2(572f, 82f), false);

        tipText = CreateText(boxObject.transform, "", 18, TextAnchor.MiddleLeft, new Color(0.95f, 0.82f, 0.32f, 1f));
        RectTransform tipRect = tipText.rectTransform;
        tipRect.anchorMin = new Vector2(0f, 0f);
        tipRect.anchorMax = new Vector2(1f, 0f);
        tipRect.pivot = new Vector2(0.5f, 0f);
        tipRect.sizeDelta = new Vector2(0f, 38f);
        tipRect.offsetMin = new Vector2(36f, 20f);
        tipRect.offsetMax = new Vector2(-36f, 58f);

        EnsureConfirmPanel(boxObject.transform);
    }

    private void CreateColumn(Transform parent, string title, Vector2 offsetMin, bool backpackColumn)
    {
        GameObject columnObject = new GameObject(title + "Column", typeof(RectTransform), typeof(Image));
        columnObject.transform.SetParent(parent, false);
        RectTransform columnRect = columnObject.GetComponent<RectTransform>();
        columnRect.anchorMin = new Vector2(0f, 0f);
        columnRect.anchorMax = new Vector2(0f, 1f);
        columnRect.pivot = new Vector2(0f, 1f);
        columnRect.sizeDelta = new Vector2(512f, -166f);
        columnRect.anchoredPosition = new Vector2(offsetMin.x, -offsetMin.y);
        columnObject.GetComponent<Image>().color = new Color(0.12f, 0.135f, 0.16f, 0.96f);

        Text titleText = CreateText(columnObject.transform, title, 22, TextAnchor.MiddleCenter, Color.white);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(0f, 42f);
        titleRect.anchoredPosition = Vector2.zero;

        GameObject viewportObject = new GameObject(title + "Viewport", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
        viewportObject.transform.SetParent(columnObject.transform, false);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(14f, 14f);
        viewportRect.offsetMax = new Vector2(-14f, -50f);
        viewportObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.08f);
        viewportObject.GetComponent<Mask>().showMaskGraphic = false;

        GameObject contentObject = new GameObject(title + "Content", typeof(RectTransform));
        contentObject.transform.SetParent(viewportObject.transform, false);
        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 420f);

        Text emptyText = CreateText(contentObject.transform, backpackColumn ? "背包空空如也。" : "暂无货物。", 20, TextAnchor.MiddleCenter, new Color(0.78f, 0.80f, 0.84f, 1f));
        RectTransform emptyRect = emptyText.rectTransform;
        emptyRect.anchorMin = new Vector2(0f, 1f);
        emptyRect.anchorMax = new Vector2(1f, 1f);
        emptyRect.pivot = new Vector2(0.5f, 1f);
        emptyRect.sizeDelta = new Vector2(0f, 110f);
        emptyRect.anchoredPosition = new Vector2(0f, -120f);

        ScrollRect scrollRect = viewportObject.GetComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 26f;

        if (backpackColumn)
        {
            backpackContentRect = contentRect;
            emptyBackpackText = emptyText;
        }
        else
        {
            goodsContentRect = contentRect;
            emptyGoodsText = emptyText;
        }
    }

    private void CreateBackpackRow(InventoryItemRecord record, int index)
    {
        InventoryItemData item = InventoryItemDatabase.GetItem(record.id);
        string itemName = item != null ? item.name : record.id;
        string typeName = item != null ? InventoryItemDatabase.GetTypeName(item.type) : "未知";
        string description = item != null ? item.description : "找不到物品数据。";

        GameObject rowObject = CreateRow(backpackContentRect, backpackRows, "BackpackRow_" + record.id, index);
        Text title = CreateText(rowObject.transform, itemName + "  x" + record.count + "  【" + typeName + "】", 17, TextAnchor.UpperLeft, Color.white);
        SetRect(title.rectTransform, new Vector2(14f, -36f), new Vector2(-126f, -10f), true);

        Text desc = CreateText(rowObject.transform, description, 15, TextAnchor.UpperLeft, new Color(0.86f, 0.88f, 0.90f, 1f));
        SetRect(desc.rectTransform, new Vector2(14f, -98f), new Vector2(-126f, -40f), true);

        string priceText = item != null && item.sellable ? "出售价格：" + item.sellPrice + " 灵石" : "不可出售";
        Text price = CreateText(rowObject.transform, priceText, 15, TextAnchor.LowerLeft, item != null && item.sellable ? new Color(0.95f, 0.82f, 0.32f, 1f) : new Color(0.60f, 0.64f, 0.68f, 1f));
        SetRect(price.rectTransform, new Vector2(14f, -132f), new Vector2(-126f, -104f), true);

        if (item != null && item.sellable)
        {
            Button sellButton = CreateButton(rowObject.transform, "出售", new Vector2(-14f, -52f), new Vector2(92f, 36f), AnchorMode.TopRight, true);
            string capturedItemId = record.id;
            sellButton.onClick.AddListener(delegate { RequestSellItem(capturedItemId); });
        }
    }

    private void CreateGoodsRow(ShopItemData goods, int index, PlayerState state)
    {
        InventoryItemData item = InventoryItemDatabase.GetItem(goods.itemId);
        string itemName = item != null ? item.name : goods.itemId;
        string description = item != null ? item.description : "找不到物品数据。";
        int remainingStock = state.GetShopStock(currentShop.id, goods.itemId, goods.stock);
        bool soldOut = remainingStock == 0;

        GameObject rowObject = CreateRow(goodsContentRect, goodsRows, "GoodsRow_" + goods.itemId, index);
        Text title = CreateText(rowObject.transform, itemName, 17, TextAnchor.UpperLeft, Color.white);
        SetRect(title.rectTransform, new Vector2(14f, -36f), new Vector2(-126f, -10f), true);

        Text desc = CreateText(rowObject.transform, description, 15, TextAnchor.UpperLeft, new Color(0.86f, 0.88f, 0.90f, 1f));
        SetRect(desc.rectTransform, new Vector2(14f, -98f), new Vector2(-126f, -40f), true);

        string stockText = remainingStock < 0 ? "库存：无限" : (remainingStock == 0 ? "库存：售罄" : "库存：" + remainingStock);
        Text price = CreateText(rowObject.transform, "价格：" + goods.buyPrice + " 灵石    " + stockText, 15, TextAnchor.LowerLeft, new Color(0.95f, 0.82f, 0.32f, 1f));
        SetRect(price.rectTransform, new Vector2(14f, -132f), new Vector2(-126f, -104f), true);

        Button buyButton = CreateButton(rowObject.transform, soldOut ? "售罄" : "购买", new Vector2(-14f, -52f), new Vector2(92f, 36f), AnchorMode.TopRight, !soldOut);
        ShopItemData capturedGoods = goods;
        buyButton.onClick.AddListener(delegate { BuyItem(capturedGoods); });
    }

    private GameObject CreateRow(RectTransform parent, List<GameObject> cache, string name, int index)
    {
        GameObject rowObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        rowObject.transform.SetParent(parent, false);
        cache.Add(rowObject);
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.sizeDelta = new Vector2(0f, RowHeight);
        rowRect.anchoredPosition = new Vector2(0f, -10f - index * (RowHeight + RowGap));
        rowObject.GetComponent<Image>().color = new Color(0.15f, 0.17f, 0.20f, 0.96f);
        return rowObject;
    }

    private void EnsureConfirmPanel(Transform parent)
    {
        confirmPanelObject = new GameObject("ShopConfirmPanel", typeof(RectTransform), typeof(Image));
        confirmPanelObject.transform.SetParent(parent, false);
        RectTransform panelRect = confirmPanelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        Image panelImage = confirmPanelObject.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.50f);
        panelImage.raycastTarget = true;

        GameObject boxObject = new GameObject("ConfirmBox", typeof(RectTransform), typeof(Image));
        boxObject.transform.SetParent(confirmPanelObject.transform, false);
        RectTransform boxRect = boxObject.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = new Vector2(430f, 210f);
        boxRect.anchoredPosition = Vector2.zero;
        boxObject.GetComponent<Image>().color = new Color(0.10f, 0.11f, 0.13f, 1f);

        confirmText = CreateText(boxObject.transform, "", 22, TextAnchor.MiddleCenter, Color.white);
        RectTransform textRect = confirmText.rectTransform;
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.sizeDelta = new Vector2(0f, 92f);
        textRect.anchoredPosition = new Vector2(0f, -28f);

        Button confirmButton = CreateButton(boxObject.transform, "确认", new Vector2(-72f, 26f), new Vector2(110f, 40f), AnchorMode.BottomCenter, true);
        confirmButton.onClick.AddListener(OnConfirmClicked);

        Button cancelButton = CreateButton(boxObject.transform, "取消", new Vector2(72f, 26f), new Vector2(110f, 40f), AnchorMode.BottomCenter, true);
        cancelButton.onClick.AddListener(OnCancelClicked);

        confirmPanelObject.SetActive(false);
    }

    public void ShowConfirm(string message, Action onConfirm, Action onCancel)
    {
        EnsurePanel();
        confirmAction = onConfirm;
        cancelAction = onCancel;
        if (confirmText != null) confirmText.text = message;
        if (confirmPanelObject != null)
        {
            confirmPanelObject.SetActive(true);
            confirmPanelObject.transform.SetAsLastSibling();
        }
    }

    private void HideConfirm()
    {
        confirmAction = null;
        cancelAction = null;
        if (confirmPanelObject != null) confirmPanelObject.SetActive(false);
    }

    private void OnConfirmClicked()
    {
        Action action = confirmAction;
        HideConfirm();
        if (action != null) action();
    }

    private void OnCancelClicked()
    {
        Action action = cancelAction;
        HideConfirm();
        if (action != null) action();
    }

    private Button CreateButton(Transform parent, string text, Vector2 position, Vector2 size, AnchorMode anchorMode, bool interactable)
    {
        GameObject buttonObject = new GameObject(text + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        ApplyAnchor(rect, anchorMode);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Image image = buttonObject.GetComponent<Image>();
        image.color = interactable ? new Color(0.20f, 0.28f, 0.34f, 1f) : new Color(0.18f, 0.18f, 0.19f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.interactable = interactable;
        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.31f, 0.40f, 0.47f, 1f);
        colors.pressedColor = new Color(0.14f, 0.18f, 0.22f, 1f);
        colors.disabledColor = new Color(0.18f, 0.18f, 0.19f, 1f);
        button.colors = colors;

        Text label = CreateText(buttonObject.transform, text, 17, TextAnchor.MiddleCenter, Color.white);
        StretchToParent(label.rectTransform);
        return button;
    }

    private Text CreateText(Transform parent, string text, int fontSize, TextAnchor alignment, Color color)
    {
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text label = textObject.GetComponent<Text>();
        label.text = text;
        label.font = GetDefaultFont();
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = color;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        return label;
    }

    private void SetRect(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax, bool topLine)
    {
        if (topLine)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
        }
        else
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
        }
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private void ApplyAnchor(RectTransform rect, AnchorMode mode)
    {
        if (mode == AnchorMode.TopRight)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
        }
        else
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
        }
    }

    private enum AnchorMode
    {
        TopRight,
        BottomCenter
    }

    private void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void ClearRows(List<GameObject> rows)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i] != null) Destroy(rows[i]);
        }
        rows.Clear();
    }

    private void CloseOtherPanels()
    {
        InventoryUIManager inventoryUI = GetComponent<InventoryUIManager>();
        if (inventoryUI != null) inventoryUI.Hide();
        CharacterStatusUIManager statusUI = GetComponent<CharacterStatusUIManager>();
        if (statusUI != null) statusUI.Hide();
    }

    private void RefreshTopStatus(PlayerState state)
    {
        if (locationUIManager != null) locationUIManager.RefreshPlayerStatus(state);
        CharacterStatusUIManager statusUI = GetComponent<CharacterStatusUIManager>();
        if (statusUI != null) statusUI.RefreshIfOpen();
        InventoryUIManager inventoryUI = GetComponent<InventoryUIManager>();
        if (inventoryUI != null) inventoryUI.RefreshIfOpen();
    }

    private PlayerState GetState()
    {
        BindReferences();
        return gameManager != null ? gameManager.GetPlayerState() : null;
    }

    private void ShowShopTip(string message)
    {
        if (tipText != null) tipText.text = message;
        ShowMessage(message);
    }

    private void ShowMessage(string message)
    {
        if (locationUIManager == null) locationUIManager = GetComponent<LocationUIManager>();
        if (locationUIManager != null) locationUIManager.ShowMessage(message);
        else Debug.Log(message);
    }

    private Font GetDefaultFont()
    {
        if (cachedFont != null) return cachedFont;
        cachedFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 16);
        if (cachedFont != null) return cachedFont;
        cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return cachedFont;
    }
}
