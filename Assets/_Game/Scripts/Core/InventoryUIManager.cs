using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUIManager : MonoBehaviour
{
    private GameManager gameManager;
    private InventoryManager inventoryManager;
    private LocationUIManager locationUIManager;

    private GameObject inventoryButtonObject;
    private GameObject panelObject;
    private RectTransform contentRect;
    private ScrollRect scrollRect;
    private Text emptyText;
    private Font cachedFont;

    private const float PanelWidth = 820f;
    private const float PanelHeight = 680f;
    private const float RowHeight = 118f;
    private const float RowGap = 10f;

    private void Start()
    {
        BindReferences();
        EnsureInventoryButton();
        EnsurePanel();
        Hide();
    }

    private void Update()
    {
        if (panelObject != null && panelObject.activeSelf && Input.GetKeyDown(KeyCode.Escape)) Hide();
        if (inventoryButtonObject != null) inventoryButtonObject.SetActive(!IsUiBlocked());
    }

    private void BindReferences()
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        if (inventoryManager == null) inventoryManager = GetComponent<InventoryManager>();
        if (locationUIManager == null) locationUIManager = GetComponent<LocationUIManager>();
    }

    public void Open()
    {
        if (BattleManager.IsBattleOpen)
        {
            ShowMessage("战斗中暂时不能打开背包。");
            return;
        }
        if (ShopManager.IsShopOpen)
        {
            ShowMessage("请先关闭商店。");
            return;
        }
        if (IsOtherWindowOpen())
        {
            ShowMessage("请先关闭当前窗口。");
            return;
        }

        EnsurePanel();
        if (panelObject == null) return;
        panelObject.SetActive(true);
        panelObject.transform.SetAsLastSibling();
        Refresh();
    }

    public void Hide()
    {
        if (panelObject != null) panelObject.SetActive(false);
    }

    public void RefreshIfOpen()
    {
        if (panelObject != null && panelObject.activeSelf) Refresh();
    }

    public void Refresh()
    {
        BindReferences();
        EnsurePanel();
        PlayerState state = gameManager != null ? gameManager.GetPlayerState() : null;
        if (state == null || contentRect == null) return;
        state.EnsureLists();

        ClearRows();
        int index = 0;
        if (state.inventoryItems != null)
        {
            for (int i = 0; i < state.inventoryItems.Count; i++)
            {
                InventoryItemRecord record = state.inventoryItems[i];
                if (record == null || string.IsNullOrEmpty(record.id) || record.count <= 0) continue;
                CreateRow(record, index);
                index++;
            }
        }

        if (emptyText != null) emptyText.gameObject.SetActive(index == 0);
        contentRect.sizeDelta = new Vector2(0f, index > 0 ? Mathf.Max(360f, index * (RowHeight + RowGap) + 20f) : 360f);
        contentRect.anchoredPosition = Vector2.zero;
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
    }

    private void ClearRows()
    {
        if (contentRect == null) return;
        List<GameObject> rows = new List<GameObject>();
        for (int i = 0; i < contentRect.childCount; i++)
        {
            Transform child = contentRect.GetChild(i);
            if (child != null && child.name.StartsWith("InventoryItemRow")) rows.Add(child.gameObject);
        }
        for (int i = 0; i < rows.Count; i++) Destroy(rows[i]);
    }

    private void CreateRow(InventoryItemRecord record, int index)
    {
        InventoryItemData item = InventoryItemDatabase.GetItem(record.id);
        string itemName = item != null ? item.name : record.id;
        string typeName = item != null ? InventoryItemDatabase.GetTypeName(item.type) : "未知";
        string description = item != null ? item.description : "找不到物品数据。";

        GameObject rowObject = new GameObject("InventoryItemRow_" + record.id, typeof(RectTransform), typeof(Image));
        rowObject.transform.SetParent(contentRect, false);
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.sizeDelta = new Vector2(0f, RowHeight);
        rowRect.anchoredPosition = new Vector2(0f, -10f - index * (RowHeight + RowGap));
        rowObject.GetComponent<Image>().color = new Color(0.14f, 0.16f, 0.19f, 0.96f);

        Text title = CreateText(rowObject.transform, itemName + "  x" + record.count + "  【" + typeName + "】", 18, TextAnchor.UpperLeft, Color.white);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(18f, -42f);
        titleRect.offsetMax = new Vector2(-170f, -10f);

        Text desc = CreateText(rowObject.transform, description, 15, TextAnchor.UpperLeft, new Color(0.86f, 0.88f, 0.90f, 1f));
        RectTransform descRect = desc.rectTransform;
        descRect.anchorMin = Vector2.zero;
        descRect.anchorMax = Vector2.one;
        descRect.offsetMin = new Vector2(18f, 12f);
        descRect.offsetMax = new Vector2(-180f, -46f);

        CreateOperationButton(rowObject.transform, record.id, item);
    }

    private void CreateOperationButton(Transform row, string itemId, InventoryItemData item)
    {
        if (item == null) return;
        PlayerState state = gameManager != null ? gameManager.GetPlayerState() : null;
        if (state == null) return;

        if (item.type == "weapon")
        {
            bool equipped = state.equippedWeaponId == itemId;
            Button button = CreateButton(row, equipped ? "卸下" : "装备", new Vector2(-54f, -50f), new Vector2(106f, 38f), true);
            if (equipped) button.onClick.AddListener(delegate { if (inventoryManager != null) inventoryManager.UnequipWeapon(); });
            else button.onClick.AddListener(delegate { if (inventoryManager != null) inventoryManager.EquipWeapon(itemId); });
            return;
        }

        if (item.usable && item.consumable)
        {
            Button button = CreateButton(row, "使用", new Vector2(-54f, -50f), new Vector2(106f, 38f), true);
            button.onClick.AddListener(delegate { if (inventoryManager != null) inventoryManager.UseItem(itemId); });
        }
    }

    private void EnsureInventoryButton()
    {
        if (inventoryButtonObject != null) return;
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        inventoryButtonObject = new GameObject("InventoryButton", typeof(RectTransform), typeof(Image), typeof(Button));
        inventoryButtonObject.transform.SetParent(canvas.transform, false);
        RectTransform rect = inventoryButtonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(90f, 38f);
        rect.anchoredPosition = new Vector2(-260f, -18f);
        inventoryButtonObject.GetComponent<Image>().color = new Color(0.20f, 0.26f, 0.32f, 1f);
        Button button = inventoryButtonObject.GetComponent<Button>();
        button.onClick.AddListener(Open);
        Text label = CreateText(inventoryButtonObject.transform, "背包", 18, TextAnchor.MiddleCenter, Color.white);
        StretchToParent(label.rectTransform);
    }

    private void EnsurePanel()
    {
        if (panelObject != null) return;
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        panelObject = new GameObject("InventoryModalOverlay", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvas.transform, false);
        RectTransform overlayRect = panelObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        Image overlayImage = panelObject.GetComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.34f);
        overlayImage.raycastTarget = true;

        GameObject boxObject = new GameObject("InventoryPanel", typeof(RectTransform), typeof(Image));
        boxObject.transform.SetParent(panelObject.transform, false);
        RectTransform boxRect = boxObject.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        boxRect.anchoredPosition = Vector2.zero;
        boxObject.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 0.98f);

        Text title = CreateText(boxObject.transform, "背包", 30, TextAnchor.MiddleCenter, Color.white);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(0f, 54f);
        titleRect.anchoredPosition = new Vector2(0f, -16f);

        GameObject viewportObject = new GameObject("InventoryViewport", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
        viewportObject.transform.SetParent(boxObject.transform, false);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(34f, 86f);
        viewportRect.offsetMax = new Vector2(-34f, -82f);
        viewportObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.08f);
        viewportObject.GetComponent<Mask>().showMaskGraphic = false;

        GameObject contentObject = new GameObject("InventoryContent", typeof(RectTransform));
        contentObject.transform.SetParent(viewportObject.transform, false);
        contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 360f);

        emptyText = CreateText(contentObject.transform, "背包空空如也。", 22, TextAnchor.MiddleCenter, new Color(0.86f, 0.88f, 0.90f, 1f));
        RectTransform emptyRect = emptyText.rectTransform;
        emptyRect.anchorMin = new Vector2(0f, 1f);
        emptyRect.anchorMax = new Vector2(1f, 1f);
        emptyRect.pivot = new Vector2(0.5f, 1f);
        emptyRect.sizeDelta = new Vector2(0f, 120f);
        emptyRect.anchoredPosition = new Vector2(0f, -120f);

        scrollRect = viewportObject.GetComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 28f;

        Button closeButton = CreateButton(boxObject.transform, "关闭", new Vector2(0f, 26f), new Vector2(130f, 42f), false);
        closeButton.onClick.AddListener(Hide);
    }

    private Button CreateButton(Transform parent, string text, Vector2 position, Vector2 size, bool topRight)
    {
        GameObject buttonObject = new GameObject(text + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        if (topRight)
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
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        buttonObject.GetComponent<Image>().color = new Color(0.18f, 0.24f, 0.30f, 1f);
        Button button = buttonObject.GetComponent<Button>();
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

    private Font GetDefaultFont()
    {
        if (cachedFont != null) return cachedFont;
        cachedFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 16);
        if (cachedFont != null) return cachedFont;
        cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return cachedFont;
    }

    private void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private bool IsUiBlocked()
    {
        if (RestManager.IsRestingTransition) return true;
        if (BattleManager.IsBattleOpen) return true;
        if (ShopManager.IsShopOpen) return true;
        if (OpeningStoryManager.IsOpeningActive) return true;
        if (ChapterTitleManager.IsChapterTitleActive) return true;
        if (ChapterOneLocationMechanicsManager.IsChapterOneEventOpen) return true;
        if (ChapterOneLateStoryFixManager.IsEndingPlaying) return true;
        return false;
    }

    private bool IsOtherWindowOpen()
    {
        GameObject statusPanel = GameObject.Find("CharacterStatusPanel");
        if (statusPanel != null && statusPanel.activeInHierarchy) return true;
        return ShopManager.IsShopOpen;
    }

    private void ShowMessage(string message)
    {
        if (locationUIManager == null) locationUIManager = GetComponent<LocationUIManager>();
        if (locationUIManager != null) locationUIManager.ShowMessage(message);
        else Debug.Log(message);
    }
}
