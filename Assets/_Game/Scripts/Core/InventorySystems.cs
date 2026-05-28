using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class InventoryItemData
{
    public string id;
    public string name;
    public string description;
    public string type;
    public bool usable;
    public bool consumable;
    public int cultivationGain;
    public int hpGain;
    public int attackBonus;
    public int defenseBonus;
    public int maxHpBonus;
}

[Serializable]
public class InventoryItemDataList
{
    public List<InventoryItemData> items = new List<InventoryItemData>();
}

/// <summary>
/// 静态物品数据库。读取 Resources/Data/items.json。
/// </summary>
public static class InventoryItemDatabase
{
    private static readonly Dictionary<string, InventoryItemData> itemById = new Dictionary<string, InventoryItemData>();
    private static bool loaded;

    public static void Load()
    {
        if (loaded) return;
        loaded = true;
        itemById.Clear();
        TextAsset jsonAsset = Resources.Load<TextAsset>("Data/items");
        if (jsonAsset == null)
        {
            Debug.LogError("找不到物品数据：Resources/Data/items.json");
            return;
        }

        InventoryItemDataList dataList = JsonUtility.FromJson<InventoryItemDataList>(jsonAsset.text);
        if (dataList == null || dataList.items == null)
        {
            Debug.LogError("物品数据格式不正确：items.json");
            return;
        }

        foreach (InventoryItemData item in dataList.items)
        {
            if (item != null && !string.IsNullOrEmpty(item.id)) itemById[item.id] = item;
        }
    }

    public static InventoryItemData GetItem(string itemId)
    {
        if (!loaded) Load();
        InventoryItemData item;
        return !string.IsNullOrEmpty(itemId) && itemById.TryGetValue(itemId, out item) ? item : null;
    }

    public static string GetItemName(string itemId)
    {
        InventoryItemData item = GetItem(itemId);
        return item != null ? item.name : itemId;
    }
}

/// <summary>
/// 背包 / 道具使用 / 简单武器装备管理器。
/// </summary>
public class InventoryManager : MonoBehaviour
{
    private GameManager gameManager;
    private LocationUIManager locationUIManager;
    private CharacterStatusUIManager characterStatusUIManager;
    private InventoryUIManager inventoryUIManager;

    private void Start()
    {
        BindReferences();
        InventoryItemDatabase.Load();
        MigrateOldItems();
        RecalculateStatsWithEquipment(false);
    }

    private void LateUpdate()
    {
        // 兜底：其他系统突破/读档后可能重算基础属性，这里把武器加成重新并入最终属性，且不会重复叠加。
        RecalculateStatsWithEquipment(false);
    }

    private void BindReferences()
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        if (locationUIManager == null) locationUIManager = GetComponent<LocationUIManager>();
        if (characterStatusUIManager == null) characterStatusUIManager = GetComponent<CharacterStatusUIManager>();
        if (inventoryUIManager == null) inventoryUIManager = GetComponent<InventoryUIManager>();
    }

    private PlayerState GetState()
    {
        BindReferences();
        return gameManager != null ? gameManager.GetPlayerState() : null;
    }

    public void MigrateOldItems()
    {
        PlayerState state = GetState();
        if (state == null) return;
        state.EnsureLists();
    }

    public void AddItem(string itemId, int count = 1)
    {
        PlayerState state = GetState();
        if (state == null || string.IsNullOrEmpty(itemId) || count <= 0) return;
        state.AddItem(itemId, count);
        RefreshUi();
        ShowMessage("获得物品：" + InventoryItemDatabase.GetItemName(itemId) + (count > 1 ? " x" + count : ""));
    }

    public bool RemoveItem(string itemId, int count = 1)
    {
        PlayerState state = GetState();
        if (state == null || string.IsNullOrEmpty(itemId) || count <= 0) return false;
        if (!state.HasItem(itemId, count)) return false;
        state.RemoveItem(itemId, count);
        if (state.equippedWeaponId == itemId && !state.HasItem(itemId)) state.equippedWeaponId = "";
        RecalculateStatsWithEquipment(false);
        RefreshUi();
        return true;
    }

    public bool HasItem(string itemId, int count = 1)
    {
        PlayerState state = GetState();
        return state != null && state.HasItem(itemId, count);
    }

    public int GetItemCount(string itemId)
    {
        PlayerState state = GetState();
        return state != null ? state.GetItemCount(itemId) : 0;
    }

    public bool UseItem(string itemId)
    {
        PlayerState state = GetState();
        if (state == null || string.IsNullOrEmpty(itemId)) return false;
        InventoryItemData item = InventoryItemDatabase.GetItem(itemId);
        if (item == null)
        {
            ShowMessage("找不到物品数据：" + itemId);
            return false;
        }
        if (!state.HasItem(itemId))
        {
            ShowMessage("背包中没有该物品。");
            return false;
        }
        if (!item.usable)
        {
            ShowMessage("这个物品暂时不能主动使用。");
            return false;
        }

        if (item.cultivationGain != 0) state.cultivation += item.cultivationGain;
        if (item.hpGain != 0) state.hp = Mathf.Min(state.maxHp, state.hp + item.hpGain);
        if (item.consumable) state.RemoveItem(itemId, 1);

        RecalculateStatsWithEquipment(false);
        RefreshUi();
        string message = "使用了：" + item.name;
        if (item.cultivationGain != 0) message += "，修为 +" + item.cultivationGain;
        if (item.hpGain != 0) message += "，生命恢复 " + item.hpGain;
        ShowMessage(message + "。");
        return true;
    }

    public bool EquipWeapon(string itemId)
    {
        PlayerState state = GetState();
        if (state == null || string.IsNullOrEmpty(itemId)) return false;
        InventoryItemData item = InventoryItemDatabase.GetItem(itemId);
        if (item == null)
        {
            ShowMessage("找不到物品数据：" + itemId);
            return false;
        }
        if (item.type != "weapon")
        {
            ShowMessage("这个物品不是武器，无法装备。");
            return false;
        }
        if (!state.HasItem(itemId))
        {
            ShowMessage("背包中没有该武器。");
            return false;
        }

        state.equippedWeaponId = itemId;
        RecalculateStatsWithEquipment(false);
        RefreshUi();
        ShowMessage("已装备：" + item.name);
        return true;
    }

    public void UnequipWeapon()
    {
        PlayerState state = GetState();
        if (state == null) return;
        if (string.IsNullOrEmpty(state.equippedWeaponId))
        {
            ShowMessage("当前没有装备武器。");
            return;
        }

        string oldName = InventoryItemDatabase.GetItemName(state.equippedWeaponId);
        state.equippedWeaponId = "";
        RecalculateStatsWithEquipment(false);
        RefreshUi();
        ShowMessage("已卸下：" + oldName);
    }

    public InventoryItemData GetEquippedWeapon()
    {
        PlayerState state = GetState();
        return state != null ? InventoryItemDatabase.GetItem(state.equippedWeaponId) : null;
    }

    public int GetWeaponAttackBonus()
    {
        InventoryItemData weapon = GetEquippedWeapon();
        return weapon != null ? weapon.attackBonus : 0;
    }

    public void RecalculateStatsWithEquipment(bool healToFull)
    {
        PlayerState state = GetState();
        if (state == null) return;
        RealmManager realmManager = GetComponent<RealmManager>();
        BodyRealmManager bodyRealmManager = GetComponent<BodyRealmManager>();
        PlayerStatCalculator.RecalculateStats(state, realmManager, bodyRealmManager, healToFull);

        InventoryItemData weapon = InventoryItemDatabase.GetItem(state.equippedWeaponId);
        if (weapon != null && state.HasItem(state.equippedWeaponId) && weapon.type == "weapon")
        {
            state.maxHp += weapon.maxHpBonus;
            state.attack += weapon.attackBonus;
            state.defense += weapon.defenseBonus;
            state.hp = healToFull ? state.maxHp : Mathf.Clamp(state.hp, 1, state.maxHp);
        }
        else if (!string.IsNullOrEmpty(state.equippedWeaponId))
        {
            state.equippedWeaponId = "";
        }
    }

    private void RefreshUi()
    {
        PlayerState state = GetState();
        if (locationUIManager != null) locationUIManager.RefreshPlayerStatus(state);
        if (characterStatusUIManager != null) characterStatusUIManager.RefreshIfOpen();
        if (inventoryUIManager != null) inventoryUIManager.RefreshIfOpen();
    }

    private void ShowMessage(string message)
    {
        if (locationUIManager != null) locationUIManager.ShowMessage(message);
        else Debug.Log(message);
    }
}

/// <summary>
/// 背包面板：运行时自动创建，不重建 DemoScene。
/// </summary>
public class InventoryUIManager : MonoBehaviour
{
    private GameManager gameManager;
    private InventoryManager inventoryManager;
    private LocationUIManager locationUIManager;

    private GameObject bagButtonObject;
    private GameObject panelObject;
    private Text emptyText;
    private RectTransform contentRect;
    private ScrollRect scrollRect;
    private readonly List<GameObject> rowObjects = new List<GameObject>();
    private Font cachedFont;

    private const float PanelWidth = 820f;
    private const float PanelHeight = 640f;
    private const float RowHeight = 86f;

    private void Start()
    {
        BindReferences();
        EnsureBagButton();
        EnsurePanel();
        Hide();
    }

    private void Update()
    {
        if (panelObject != null && panelObject.activeSelf && Input.GetKeyDown(KeyCode.Escape)) Hide();
        if (bagButtonObject != null) bagButtonObject.SetActive(!IsUiBlocked());
    }

    private void BindReferences()
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        if (inventoryManager == null) inventoryManager = GetComponent<InventoryManager>();
        if (locationUIManager == null) locationUIManager = GetComponent<LocationUIManager>();
    }

    public void Open()
    {
        BindReferences();
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
        if (contentRect == null) return;
        ClearRows();
        PlayerState state = gameManager != null ? gameManager.GetPlayerState() : null;
        if (state == null) return;
        state.EnsureLists();

        bool hasAny = false;
        float y = -8f;
        foreach (InventoryItemRecord record in state.inventoryItems)
        {
            if (record == null || string.IsNullOrEmpty(record.id) || record.count <= 0) continue;
            CreateItemRow(record, y, state.equippedWeaponId == record.id);
            y -= RowHeight + 8f;
            hasAny = true;
        }

        if (emptyText != null) emptyText.gameObject.SetActive(!hasAny);
        float contentHeight = Mathf.Max(420f, Mathf.Abs(y) + 20f);
        contentRect.sizeDelta = new Vector2(0f, contentHeight);
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
    }

    private void CreateItemRow(InventoryItemRecord record, float y, bool equipped)
    {
        InventoryItemData item = InventoryItemDatabase.GetItem(record.id);
        string itemName = item != null ? item.name : record.id;
        string type = item != null ? item.type : "unknown";
        string description = item != null ? item.description : "缺少物品说明。";

        GameObject row = new GameObject("InventoryRow_" + record.id, typeof(RectTransform), typeof(Image));
        row.transform.SetParent(contentRect, false);
        rowObjects.Add(row);
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.sizeDelta = new Vector2(0f, RowHeight);
        rowRect.anchoredPosition = new Vector2(0f, y);
        Image rowImage = row.GetComponent<Image>();
        rowImage.color = new Color(0.12f, 0.14f, 0.17f, 0.94f);

        string line = itemName + " x" + record.count + "    类型：" + TranslateType(type) + "\n" + description;
        Text label = CreateText(row.transform, line, 17, TextAnchor.MiddleLeft, Color.white);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(16f, 8f);
        labelRect.offsetMax = new Vector2(-230f, -8f);

        if (item != null && item.usable)
        {
            Button useButton = CreateButton(row.transform, "使用", new Vector2(-158f, 22f), new Vector2(84f, 34f));
            string capturedId = record.id;
            useButton.onClick.AddListener(delegate
            {
                if (inventoryManager != null) inventoryManager.UseItem(capturedId);
                Refresh();
            });
        }

        if (item != null && item.type == "weapon")
        {
            string buttonText = equipped ? "卸下" : "装备";
            Button equipButton = CreateButton(row.transform, buttonText, new Vector2(-58f, 22f), new Vector2(84f, 34f));
            string capturedId = record.id;
            equipButton.onClick.AddListener(delegate
            {
                if (inventoryManager == null) return;
                if (equipped) inventoryManager.UnequipWeapon();
                else inventoryManager.EquipWeapon(capturedId);
                Refresh();
            });
        }

        if (equipped)
        {
            Text equippedLabel = CreateText(row.transform, "已装备", 16, TextAnchor.MiddleCenter, new Color(0.85f, 1f, 0.72f, 1f));
            RectTransform eqRect = equippedLabel.rectTransform;
            eqRect.anchorMin = new Vector2(1f, 0.5f);
            eqRect.anchorMax = new Vector2(1f, 0.5f);
            eqRect.pivot = new Vector2(1f, 0.5f);
            eqRect.sizeDelta = new Vector2(80f, 30f);
            eqRect.anchoredPosition = new Vector2(-10f, -22f);
        }
    }

    private string TranslateType(string type)
    {
        if (type == "tool") return "工具";
        if (type == "clue") return "线索";
        if (type == "scroll") return "残卷";
        if (type == "consumable") return "消耗品";
        if (type == "weapon") return "武器";
        return type;
    }

    private void EnsureBagButton()
    {
        if (bagButtonObject != null) return;
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        bagButtonObject = new GameObject("InventoryButton", typeof(RectTransform), typeof(Image), typeof(Button));
        bagButtonObject.transform.SetParent(canvas.transform, false);
        RectTransform rect = bagButtonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(90f, 38f);
        rect.anchoredPosition = new Vector2(-260f, -18f);

        Image image = bagButtonObject.GetComponent<Image>();
        image.color = new Color(0.20f, 0.26f, 0.32f, 1f);
        Button button = bagButtonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(Open);
        Text label = CreateText(bagButtonObject.transform, "背包", 18, TextAnchor.MiddleCenter, Color.white);
        StretchToParent(label.rectTransform);
    }

    private void EnsurePanel()
    {
        if (panelObject != null) return;
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        panelObject = new GameObject("InventoryPanel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        panelRect.anchoredPosition = Vector2.zero;
        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.08f, 0.09f, 0.11f, 0.98f);

        Text title = CreateText(panelObject.transform, "背包", 30, TextAnchor.MiddleCenter, Color.white);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(0f, 54f);
        titleRect.anchoredPosition = new Vector2(0f, -14f);

        GameObject viewportObject = new GameObject("InventoryViewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportObject.transform.SetParent(panelObject.transform, false);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(38f, 88f);
        viewportRect.offsetMax = new Vector2(-38f, -76f);
        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.08f);
        Mask mask = viewportObject.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject contentObject = new GameObject("InventoryContent", typeof(RectTransform));
        contentObject.transform.SetParent(viewportObject.transform, false);
        contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 420f);

        scrollRect = panelObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;

        emptyText = CreateText(panelObject.transform, "背包空空如也。", 22, TextAnchor.MiddleCenter, Color.white);
        RectTransform emptyRect = emptyText.rectTransform;
        emptyRect.anchorMin = Vector2.zero;
        emptyRect.anchorMax = Vector2.one;
        emptyRect.offsetMin = new Vector2(40f, 88f);
        emptyRect.offsetMax = new Vector2(-40f, -76f);

        Button closeButton = CreateButton(panelObject.transform, "关闭", new Vector2(0f, 24f), new Vector2(120f, 40f));
        closeButton.onClick.AddListener(Hide);
    }

    private Button CreateButton(Transform parent, string text, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject buttonObject = new GameObject(text + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.18f, 0.24f, 0.30f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
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

    private void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void ClearRows()
    {
        foreach (GameObject row in rowObjects)
        {
            if (row != null) Destroy(row);
        }
        rowObjects.Clear();
    }

    private Font GetDefaultFont()
    {
        if (cachedFont != null) return cachedFont;
        cachedFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 16);
        if (cachedFont != null) return cachedFont;
        cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return cachedFont;
    }

    private bool IsUiBlocked()
    {
        if (RestManager.IsRestingTransition) return true;
        if (BattleManager.IsBattleOpen) return true;
        if (OpeningStoryManager.IsOpeningActive) return true;
        if (ChapterTitleManager.IsChapterTitleActive) return true;
        if (ChapterOneLocationMechanicsManager.IsChapterOneEventOpen) return true;
        if (ChapterOneLateStoryFixManager.IsEndingPlaying) return true;
        return false;
    }
}
