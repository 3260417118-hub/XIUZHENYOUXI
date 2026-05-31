using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 第一版本地存档管理器。
/// 保存 PlayerState；背包数量和装备武器也随 PlayerState 一起保存。
/// 存档文件放在 Application.persistentDataPath 下。
/// </summary>
public class SaveManager : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private MapGridManager mapGridManager;
    [SerializeField] private LocationUIManager locationUIManager;
    [SerializeField] private LocationActionManager locationActionManager;
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private EventManager eventManager;

    [Header("存档按钮")]
    [SerializeField] private Button saveGameButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button newGameButton;

    private const float SaveButtonWidth = 100f;
    private const float SaveButtonHeight = 44f;
    private const float SaveButtonGap = 12f;
    private const float SaveButtonsRightMargin = 24f;
    private const float SaveButtonsBottomMargin = 210f;

    private Font cachedFont;

    private string SaveFilePath
    {
        get { return Path.Combine(Application.persistentDataPath, "xianxia_save.json"); }
    }

    public void SetReferences(GameManager game, MapGridManager mapGrid, LocationUIManager locationUI, LocationActionManager actionManager, DialogueManager dialogue)
    {
        gameManager = game;
        mapGridManager = mapGrid;
        locationUIManager = locationUI;
        locationActionManager = actionManager;
        dialogueManager = dialogue;
        eventManager = GetComponent<EventManager>();
        EnsureSaveButtons();
        BindButtons();
    }

    private void Start()
    {
        FindMissingReferences();
        EnsureSaveButtons();
        BindButtons();
    }

    private void FindMissingReferences()
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        if (mapGridManager == null) mapGridManager = GetComponent<MapGridManager>();
        if (locationUIManager == null) locationUIManager = GetComponent<LocationUIManager>();
        if (locationActionManager == null) locationActionManager = GetComponent<LocationActionManager>();
        if (dialogueManager == null) dialogueManager = GetComponent<DialogueManager>();
        if (eventManager == null) eventManager = GetComponent<EventManager>();
    }

    private void BindButtons()
    {
        if (saveGameButton != null)
        {
            saveGameButton.onClick.RemoveListener(SaveGame);
            saveGameButton.onClick.AddListener(SaveGame);
        }
        if (loadGameButton != null)
        {
            loadGameButton.onClick.RemoveListener(LoadGame);
            loadGameButton.onClick.AddListener(LoadGame);
        }
        if (newGameButton != null)
        {
            newGameButton.onClick.RemoveListener(NewGame);
            newGameButton.onClick.AddListener(NewGame);
        }
    }

    public void SaveGame()
    {
        if (RestManager.IsRestingTransition)
        {
            ShowMessage("正在休息过夜，暂时不能保存。");
            return;
        }
        if (gameManager == null)
        {
            ShowMessage("保存失败：缺少 GameManager。");
            return;
        }
        try
        {
            PlayerState playerState = gameManager.GetPlayerState();
            if (playerState != null) playerState.EnsureLists();
            string json = JsonUtility.ToJson(playerState, true);
            File.WriteAllText(SaveFilePath, json);
            ShowMessage("保存成功：" + SaveFilePath);
        }
        catch (Exception exception)
        {
            Debug.LogError("保存游戏失败：" + exception.Message);
            ShowMessage("保存失败，请查看 Console。 ");
        }
    }

    public bool HasSave()
    {
        return File.Exists(SaveFilePath);
    }

    public void LoadGame()
    {
        if (RestManager.IsRestingTransition)
        {
            ShowMessage("正在休息过夜，暂时不能读取。");
            return;
        }
        if (!HasSave())
        {
            ShowMessage("暂无存档");
            return;
        }
        if (gameManager == null)
        {
            ShowMessage("读取失败：缺少 GameManager。");
            return;
        }
        try
        {
            string json = File.ReadAllText(SaveFilePath);
            PlayerState loadedState = JsonUtility.FromJson<PlayerState>(json);
            if (loadedState == null || string.IsNullOrEmpty(loadedState.currentCellId))
            {
                ShowMessage("存档数据无效。");
                return;
            }
            loadedState.EnsureLists();
            CopyPlayerState(loadedState, gameManager.GetPlayerState());
            NormalizeProgressionAndStats();
            RefreshAfterStateChanged("读取存档成功。");
            OpeningStoryManager openingStory = GetComponent<OpeningStoryManager>();
            if (openingStory != null) openingStory.CheckOpeningAfterLoad();
            BlockingEncounterManager blockingEncounterManager = GetComponent<BlockingEncounterManager>();
            if (blockingEncounterManager != null) blockingEncounterManager.RestoreActiveEncounterUI();
        }
        catch (Exception exception)
        {
            Debug.LogError("读取游戏失败：" + exception.Message);
            ShowMessage("读取失败，请查看 Console。 ");
        }
    }

    public void NewGame()
    {
        if (RestManager.IsRestingTransition)
        {
            ShowMessage("正在休息过夜，暂时不能新游戏。");
            return;
        }
        if (gameManager == null)
        {
            ShowMessage("新游戏失败：缺少 GameManager。");
            return;
        }
        gameManager.InitNewGame();
        NormalizeProgressionAndStats();
        RefreshAfterStateChanged("新游戏开始。旧存档不会自动删除。 ");
        OpeningStoryManager openingStory = GetComponent<OpeningStoryManager>();
        if (openingStory != null) openingStory.PlayOpeningIfNeeded();
    }

    private void CopyPlayerState(PlayerState source, PlayerState target)
    {
        if (source == null || target == null) return;
        source.EnsureLists();
        target.EnsureLists();

        target.playerName = string.IsNullOrEmpty(source.playerName) ? "林昊" : source.playerName;
        target.currentCellId = source.currentCellId;
        target.currentX = source.currentX;
        target.currentY = source.currentY;
        target.currentMapId = string.IsNullOrEmpty(source.currentMapId) ? "main" : source.currentMapId;
        target.returnMainCellId = string.IsNullOrEmpty(source.returnMainCellId) ? "" : source.returnMainCellId;
        target.day = source.day;
        target.actionPoints = source.actionPoints;
        target.maxActionPoints = source.maxActionPoints <= 0 ? 3 : source.maxActionPoints;

        target.cultivation = source.cultivation;
        target.realm = string.IsNullOrEmpty(source.realm) ? "凡人" : source.realm;
        target.realmLevel = source.realmLevel < 0 ? 0 : source.realmLevel;
        target.maxCultivation = source.maxCultivation <= 0 ? 150 : source.maxCultivation;

        target.bodyCultivation = source.bodyCultivation < 0 ? 0 : source.bodyCultivation;
        target.bodyRealm = string.IsNullOrEmpty(source.bodyRealm) ? "凡体" : source.bodyRealm;
        target.bodyRealmLevel = source.bodyRealmLevel < 0 ? 0 : source.bodyRealmLevel;
        target.maxBodyCultivation = source.maxBodyCultivation <= 0 ? 150 : source.maxBodyCultivation;

        target.equippedCultivationSkillId = source.equippedCultivationSkillId == null ? "" : source.equippedCultivationSkillId;
        target.equippedBodyMethodId = source.equippedBodyMethodId == null ? "" : source.equippedBodyMethodId;
        target.equippedSpellSkillId = source.equippedSpellSkillId == null ? "" : source.equippedSpellSkillId;
        target.equippedWeaponId = source.equippedWeaponId == null ? "" : source.equippedWeaponId;

        target.spiritStones = source.spiritStones;
        target.hasSeenOpening = source.hasSeenOpening;
        target.activeBlockingEncounterId = source.activeBlockingEncounterId;
        target.currentRestLocationId = string.IsNullOrEmpty(source.currentRestLocationId) ? "ruined_hut" : source.currentRestLocationId;

        target.baseMaxHp = source.baseMaxHp <= 0 ? 50 : source.baseMaxHp;
        target.baseAttack = source.baseAttack <= 0 ? 10 : source.baseAttack;
        target.baseDefense = source.baseDefense < 0 ? 0 : source.baseDefense;
        target.hp = source.hp <= 0 ? target.baseMaxHp : source.hp;
        target.maxHp = source.maxHp <= 0 ? target.baseMaxHp : source.maxHp;
        target.attack = source.attack <= 0 ? target.baseAttack : source.attack;
        target.defense = source.defense < 0 ? target.baseDefense : source.defense;

        target.flags = new List<string>(source.flags);
        target.visitedCellIds = new List<string>(source.visitedCellIds);
        target.dayEventsTriggered = new List<string>(source.dayEventsTriggered);
        target.items = new List<string>(source.items);
        target.inventoryItems = new List<InventoryItemRecord>();
        if (source.inventoryItems != null)
        {
            foreach (InventoryItemRecord record in source.inventoryItems)
            {
                if (record != null && !string.IsNullOrEmpty(record.id) && record.count > 0)
                {
                    target.inventoryItems.Add(new InventoryItemRecord { id = record.id, count = record.count });
                }
            }
        }
        target.shopStocks = new List<ShopStockRecord>();
        if (source.shopStocks != null)
        {
            foreach (ShopStockRecord record in source.shopStocks)
            {
                if (record != null && !string.IsNullOrEmpty(record.shopId) && !string.IsNullOrEmpty(record.itemId) && record.remainingStock >= 0)
                {
                    target.shopStocks.Add(new ShopStockRecord { shopId = record.shopId, itemId = record.itemId, remainingStock = record.remainingStock });
                }
            }
        }
        target.learnedSkills = new List<string>(source.learnedSkills);
        target.unlockedCellIds = new List<string>(source.unlockedCellIds);
        target.dailyActionRecords = new List<string>(source.dailyActionRecords);
        target.pendingNightEvents = new List<string>(source.pendingNightEvents);
        target.counters = new List<CounterRecord>();
        foreach (CounterRecord record in source.counters)
        {
            if (record != null) target.counters.Add(new CounterRecord { id = record.id, value = record.value });
        }
        target.EnsureLists();
    }

    private void NormalizeProgressionAndStats()
    {
        RealmManager realmManager = GetComponent<RealmManager>();
        if (realmManager != null) realmManager.NormalizePlayerRealm();
        BodyRealmManager bodyRealmManager = GetComponent<BodyRealmManager>();
        if (bodyRealmManager != null) bodyRealmManager.NormalizeBodyRealm();
        InventoryManager inventoryManager = GetComponent<InventoryManager>();
        if (inventoryManager != null) inventoryManager.RecalculateStats(false);
    }

    private void RefreshAfterStateChanged(string message)
    {
        FindMissingReferences();
        if (dialogueManager != null) dialogueManager.CloseDialogueSilently();
        if (eventManager != null) eventManager.CloseEventSilently();
        DayEventManager dayEventManager = GetComponent<DayEventManager>();
        if (dayEventManager != null) dayEventManager.CloseDayEventSilently();
        BattleManager battleManager = GetComponent<BattleManager>();
        if (battleManager != null) battleManager.CloseBattleSilently();
        ShopManager shopManager = GetComponent<ShopManager>();
        if (shopManager != null) shopManager.HideImmediately();
        ChapterTitleManager chapterTitle = GetComponent<ChapterTitleManager>();
        if (chapterTitle != null) chapterTitle.HideImmediately();
        ChapterOneLocationMechanicsManager chapterOne = GetComponent<ChapterOneLocationMechanicsManager>();
        if (chapterOne != null) chapterOne.CloseChapterOneEventSilently();
        NormalizeProgressionAndStats();
        if (mapGridManager != null)
        {
            mapGridManager.SyncPlayerPositionToCurrentCell();
            mapGridManager.RefreshMap();
        }
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        MapCellData currentCell = mapGridManager != null ? mapGridManager.GetCurrentCell() : null;
        if (locationUIManager != null)
        {
            locationUIManager.RefreshLocation(currentCell, playerState);
            locationUIManager.ShowMessage(message);
        }
        if (locationActionManager != null) locationActionManager.RefreshCurrentLocation();
        CharacterStatusUIManager statusUI = GetComponent<CharacterStatusUIManager>();
        if (statusUI != null) statusUI.RefreshIfOpen();
        InventoryUIManager inventoryUI = GetComponent<InventoryUIManager>();
        if (inventoryUI != null) inventoryUI.RefreshIfOpen();
    }

    private void ShowMessage(string message)
    {
        if (locationUIManager != null) locationUIManager.ShowMessage(message);
        else Debug.Log(message);
    }

    private void EnsureSaveButtons()
    {
        if (saveGameButton == null) saveGameButton = FindButton("SaveGameButton");
        if (loadGameButton == null) loadGameButton = FindButton("LoadGameButton");
        if (newGameButton == null) newGameButton = FindButton("NewGameButton");
        Transform parent = null;
        Button endDayButton = FindButton("EndDayButton");
        if (endDayButton != null) parent = endDayButton.transform.parent;
        else
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null) parent = canvas.transform;
        }
        if (parent == null) return;
        if (newGameButton == null) newGameButton = CreateSaveButton(parent, "NewGameButton", "新游戏");
        if (loadGameButton == null) loadGameButton = CreateSaveButton(parent, "LoadGameButton", "读取游戏");
        if (saveGameButton == null) saveGameButton = CreateSaveButton(parent, "SaveGameButton", "保存游戏");
        LayoutSaveButtonsBottomRight();
    }

    private void LayoutSaveButtonsBottomRight()
    {
        MoveButtonToBottomRight(saveGameButton, 0);
        MoveButtonToBottomRight(loadGameButton, 1);
        MoveButtonToBottomRight(newGameButton, 2);
    }

    private void MoveButtonToBottomRight(Button button, int indexFromRight)
    {
        if (button == null) return;
        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect == null) return;
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = new Vector2(SaveButtonWidth, SaveButtonHeight);
        float x = -(SaveButtonsRightMargin + indexFromRight * (SaveButtonWidth + SaveButtonGap));
        rect.anchoredPosition = new Vector2(x, SaveButtonsBottomMargin);
    }

    private Button FindButton(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        return target == null ? null : target.GetComponent<Button>();
    }

    private Button CreateSaveButton(Transform parent, string objectName, string text)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = new Vector2(SaveButtonWidth, SaveButtonHeight);
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.30f, 0.34f, 0.38f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        Text label = CreateButtonText(buttonObject.transform, text);
        StretchToParent(label.rectTransform);
        return button;
    }

    private Text CreateButtonText(Transform parent, string text)
    {
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text label = textObject.GetComponent<Text>();
        label.text = text;
        label.font = GetDefaultFont();
        label.fontSize = 18;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
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
}
