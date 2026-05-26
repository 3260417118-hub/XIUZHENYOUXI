using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class DayEventOptionData
{
    public string text;
    public string message;
    public string[] setFlags;
    public string startBattleId;
    public bool closeEvent;
}

[Serializable]
public class DayEventData
{
    public string id;
    public int triggerDay;
    public string title;
    public string text;
    public List<DayEventOptionData> options = new List<DayEventOptionData>();
    public string battleId;
    public string[] setFlags;
    public string messageAfterClose;
}

[Serializable]
public class DayEventDataList
{
    public List<DayEventData> events = new List<DayEventData>();
}

[Serializable]
public class BattleData
{
    public string id;
    public string title;
    public string enemyName;
    public int enemyHp;
    public int enemyAttack;
    public int enemyDefense;
    public string winMessage;
    public string loseMessage;
    public string[] winFlags;
    public string[] loseFlags;
}

[Serializable]
public class BattleDataList
{
    public List<BattleData> battles = new List<BattleData>();
}

public class OpeningStoryManager : MonoBehaviour
{
    public static bool IsOpeningActive { get; private set; }

    private readonly string[] storyTexts = new string[]
    {
        "十年前，林家一夜血雨。",
        "林昊的父亲林远山，不知从何处得到一卷残破古卷。那卷轴上的文字无人能识，却引来了灭门之祸。",
        "那一夜，刀光、火光、血光，照亮了整个林家宅院。父亲拼死护着林昊、母亲和妹妹逃出重围。",
        "可逃亡途中，众人又遭遇伏击。混乱中，林昊与母亲失散，只记得自己死死拉着妹妹的手。",
        "再醒来时，他躺在村口草地上。头痛欲裂，记忆支离破碎。",
        "他只记得一件事——他还有一个妹妹。",
        "村口草木凌乱，远处山路雾气沉沉。林昊缓缓起身，开始寻找妹妹的下落。"
    };

    private GameManager gameManager;
    private LocationUIManager locationUIManager;
    private GameObject panelObject;
    private Text storyText;
    private Button continueButton;
    private int storyIndex;

    private void Start()
    {
        gameManager = GetComponent<GameManager>();
        locationUIManager = GetComponent<LocationUIManager>();
        StartCoroutine(PlayOpeningNextFrame());
    }

    private IEnumerator PlayOpeningNextFrame()
    {
        yield return null;
        PlayOpeningIfNeeded();
    }

    public void CheckOpeningAfterLoad()
    {
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState != null && playerState.hasSeenOpening)
        {
            HidePanel();
        }
        else
        {
            PlayOpeningIfNeeded();
        }
    }

    public void PlayOpeningIfNeeded()
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        if (locationUIManager == null) locationUIManager = GetComponent<LocationUIManager>();

        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState == null || playerState.hasSeenOpening)
        {
            HidePanel();
            return;
        }

        EnsurePanel();
        storyIndex = 0;
        IsOpeningActive = true;
        panelObject.SetActive(true);
        ShowCurrentText();
    }

    private void ShowCurrentText()
    {
        if (storyText == null || continueButton == null) return;
        storyText.text = storyTexts[storyIndex];
        Text buttonText = continueButton.GetComponentInChildren<Text>();
        if (buttonText != null) buttonText.text = storyIndex >= storyTexts.Length - 1 ? "进入游戏" : "继续";
    }

    private void OnContinueClicked()
    {
        if (storyIndex < storyTexts.Length - 1)
        {
            storyIndex++;
            ShowCurrentText();
            return;
        }

        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState != null)
        {
            playerState.hasSeenOpening = true;
            playerState.AddFlag("tutorial_move_shown");
        }

        HidePanel();

        if (locationUIManager != null)
        {
            locationUIManager.ShowMessage("你在村口醒来，记忆一片混乱。你可以点击周围相邻格子移动。移动不会消耗行动点。");
        }
    }

    private void HidePanel()
    {
        IsOpeningActive = false;
        if (panelObject != null) panelObject.SetActive(false);
    }

    private void EnsurePanel()
    {
        if (panelObject != null) return;
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        Font font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 22);
        panelObject = new GameObject("OpeningPanel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        Stretch(panelRect);
        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.02f, 0.02f, 0.03f, 0.96f);
        panelImage.raycastTarget = true;

        GameObject textObject = new GameObject("OpeningText", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(panelObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.18f, 0.38f);
        textRect.anchorMax = new Vector2(0.82f, 0.72f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        storyText = textObject.GetComponent<Text>();
        storyText.font = font;
        storyText.fontSize = 28;
        storyText.color = Color.white;
        storyText.alignment = TextAnchor.MiddleCenter;
        storyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        storyText.verticalOverflow = VerticalWrapMode.Overflow;

        continueButton = CreateButton(panelObject.transform, "ContinueOpeningButton", "继续", new Vector2(180f, 52f));
        RectTransform buttonRect = continueButton.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.25f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.25f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = Vector2.zero;
        continueButton.onClick.AddListener(OnContinueClicked);
    }

    private Button CreateButton(Transform parent, string objectName, string text, Vector2 size)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.18f, 0.22f, 0.28f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        Stretch(textRect);
        Text label = textObject.GetComponent<Text>();
        label.font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 20);
        label.fontSize = 22;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.text = text;
        return button;
    }

    private void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}

public class DayEventManager : MonoBehaviour
{
    public static bool IsDayEventOpen { get; private set; }

    private readonly Dictionary<string, DayEventData> eventById = new Dictionary<string, DayEventData>();
    private GameManager gameManager;
    private LocationUIManager locationUIManager;
    private LocationActionManager locationActionManager;
    private GameObject panelObject;
    private Text titleText;
    private Text bodyText;
    private RectTransform optionContainer;
    private DayEventData currentEvent;
    private bool boundEndDay;

    private void Start()
    {
        gameManager = GetComponent<GameManager>();
        locationUIManager = GetComponent<LocationUIManager>();
        locationActionManager = GetComponent<LocationActionManager>();
        LoadDayEvents();
        StartCoroutine(BindEndDayNextFrame());
        StartCoroutine(CheckDayAfterOpening());
    }

    private IEnumerator BindEndDayNextFrame()
    {
        yield return null;
        BindEndDayButton();
    }

    private IEnumerator CheckDayAfterOpening()
    {
        yield return null;
        yield return null;
        if (!OpeningStoryManager.IsOpeningActive)
        {
            CheckTodayEvent();
        }
    }

    private void BindEndDayButton()
    {
        if (boundEndDay) return;
        GameObject endDayObject = GameObject.Find("EndDayButton");
        if (endDayObject == null) return;
        Button button = endDayObject.GetComponent<Button>();
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(EndDayAndCheckEvent);
        boundEndDay = true;
    }

    private void LoadDayEvents()
    {
        eventById.Clear();
        TextAsset jsonAsset = Resources.Load<TextAsset>("Data/day_events");
        if (jsonAsset == null) return;
        DayEventDataList dataList = JsonUtility.FromJson<DayEventDataList>(jsonAsset.text);
        if (dataList == null || dataList.events == null) return;
        foreach (DayEventData eventData in dataList.events)
        {
            if (eventData != null && !string.IsNullOrEmpty(eventData.id)) eventById[eventData.id] = eventData;
        }
    }

    public void EndDayAndCheckEvent()
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        if (locationUIManager == null) locationUIManager = GetComponent<LocationUIManager>();
        if (locationActionManager == null) locationActionManager = GetComponent<LocationActionManager>();

        if (OpeningStoryManager.IsOpeningActive || BattleManager.IsBattleOpen || IsDayEventOpen)
        {
            if (locationUIManager != null) locationUIManager.ShowMessage("请先处理当前事件。");
            return;
        }

        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState == null) return;

        ActionPointRules.EndDay(playerState);
        if (locationUIManager != null)
        {
            locationUIManager.RefreshPlayerStatus(playerState);
            locationUIManager.ShowMessage("新的一天开始了，行动点已恢复。");
        }

        if (locationActionManager != null) locationActionManager.RefreshCurrentLocation();
        CheckTodayEvent();
    }

    public void CheckTodayEvent()
    {
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState == null || OpeningStoryManager.IsOpeningActive) return;
        playerState.EnsureLists();

        foreach (DayEventData eventData in eventById.Values)
        {
            if (eventData.triggerDay == playerState.day && !playerState.HasTriggeredDayEvent(eventData.id))
            {
                playerState.AddTriggeredDayEvent(eventData.id);
                ApplyFlags(eventData.setFlags);
                ShowDayEvent(eventData);
                return;
            }
        }
    }

    private void ShowDayEvent(DayEventData eventData)
    {
        currentEvent = eventData;
        EnsurePanel();
        IsDayEventOpen = true;
        panelObject.SetActive(true);
        titleText.text = eventData.title;
        bodyText.text = eventData.text;
        ClearOptions();
        if (eventData.options == null || eventData.options.Count == 0)
        {
            AddOptionButton("离开", CloseDayEvent);
            return;
        }

        foreach (DayEventOptionData option in eventData.options)
        {
            DayEventOptionData captured = option;
            AddOptionButton(option.text, delegate { ExecuteOption(captured); });
        }
    }

    private void ExecuteOption(DayEventOptionData option)
    {
        ApplyFlags(option.setFlags);
        if (!string.IsNullOrEmpty(option.message) && locationUIManager != null) locationUIManager.ShowMessage(option.message);

        if (!string.IsNullOrEmpty(option.startBattleId))
        {
            CloseDayEventSilently();
            BattleManager battleManager = GetComponent<BattleManager>();
            if (battleManager != null) battleManager.StartBattle(option.startBattleId);
            return;
        }

        if (option.closeEvent) CloseDayEvent();
    }

    private void ApplyFlags(string[] flags)
    {
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState == null || flags == null) return;
        foreach (string flag in flags) playerState.AddFlag(flag);
    }

    private void CloseDayEvent()
    {
        string message = currentEvent != null ? currentEvent.messageAfterClose : "";
        CloseDayEventSilently();
        if (!string.IsNullOrEmpty(message) && locationUIManager != null) locationUIManager.ShowMessage(message);
        if (locationActionManager != null) locationActionManager.RefreshCurrentLocation();
    }

    public void CloseDayEventSilently()
    {
        IsDayEventOpen = false;
        if (panelObject != null) panelObject.SetActive(false);
        currentEvent = null;
    }

    private void EnsurePanel()
    {
        if (panelObject != null) return;
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;
        Font font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 20);
        panelObject = new GameObject("DayEventPanel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.02f, 0.02f, 0.03f, 0.88f);
        panelImage.raycastTarget = true;

        titleText = CreateText(panelObject.transform, "Title", font, 28, TextAnchor.MiddleCenter, new Vector2(0.15f, 0.72f), new Vector2(0.85f, 0.82f));
        bodyText = CreateText(panelObject.transform, "Body", font, 22, TextAnchor.MiddleCenter, new Vector2(0.18f, 0.42f), new Vector2(0.82f, 0.70f));
        GameObject optionObject = new GameObject("Options", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        optionObject.transform.SetParent(panelObject.transform, false);
        optionContainer = optionObject.GetComponent<RectTransform>();
        optionContainer.anchorMin = new Vector2(0.20f, 0.25f);
        optionContainer.anchorMax = new Vector2(0.80f, 0.35f);
        optionContainer.offsetMin = Vector2.zero;
        optionContainer.offsetMax = Vector2.zero;
        HorizontalLayoutGroup layout = optionObject.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleCenter;
    }

    private Text CreateText(Transform parent, string name, Font font, int size, TextAnchor align, Vector2 min, Vector2 max)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Text text = obj.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = align;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private void AddOptionButton(string text, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(text + "Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(optionContainer, false);
        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredWidth = 160f;
        layout.preferredHeight = 42f;
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.18f, 0.22f, 0.28f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        Text label = CreateText(buttonObject.transform, "Text", titleText.font, 18, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
        label.text = text;
    }

    private void ClearOptions()
    {
        if (optionContainer == null) return;
        for (int i = optionContainer.childCount - 1; i >= 0; i--) Destroy(optionContainer.GetChild(i).gameObject);
    }
}

public class BattleManager : MonoBehaviour
{
    public static bool IsBattleOpen { get; private set; }

    private readonly Dictionary<string, BattleData> battleById = new Dictionary<string, BattleData>();
    private GameManager gameManager;
    private LocationUIManager locationUIManager;
    private LocationActionManager locationActionManager;
    private GameObject panelObject;
    private Text titleText;
    private Text bodyText;
    private Button attackButton;
    private BattleData currentBattle;
    private int enemyHp;

    private void Start()
    {
        gameManager = GetComponent<GameManager>();
        locationUIManager = GetComponent<LocationUIManager>();
        locationActionManager = GetComponent<LocationActionManager>();
        LoadBattles();
    }

    private void LoadBattles()
    {
        TextAsset jsonAsset = Resources.Load<TextAsset>("Data/battles");
        if (jsonAsset == null) return;
        BattleDataList dataList = JsonUtility.FromJson<BattleDataList>(jsonAsset.text);
        if (dataList == null || dataList.battles == null) return;
        foreach (BattleData battle in dataList.battles)
        {
            if (battle != null && !string.IsNullOrEmpty(battle.id)) battleById[battle.id] = battle;
        }
    }

    public void StartBattle(string battleId)
    {
        if (battleById.Count == 0) LoadBattles();
        BattleData battle;
        if (!battleById.TryGetValue(battleId, out battle))
        {
            if (locationUIManager != null) locationUIManager.ShowMessage("找不到战斗数据：" + battleId);
            return;
        }

        currentBattle = battle;
        enemyHp = battle.enemyHp;
        EnsurePanel();
        IsBattleOpen = true;
        panelObject.SetActive(true);
        RefreshBattleText("战斗开始！");
    }

    private void Attack()
    {
        if (currentBattle == null || gameManager == null) return;
        PlayerState playerState = gameManager.GetPlayerState();
        int playerDamage = Mathf.Max(1, playerState.attack - currentBattle.enemyDefense);
        enemyHp -= playerDamage;
        string log = "你攻击了" + currentBattle.enemyName + "，造成 " + playerDamage + " 点伤害。";

        if (enemyHp <= 0)
        {
            ApplyFlags(currentBattle.winFlags);
            log += "\n" + currentBattle.winMessage;
            RefreshBattleText(log);
            attackButton.GetComponentInChildren<Text>().text = "结束战斗";
            attackButton.onClick.RemoveAllListeners();
            attackButton.onClick.AddListener(CloseBattle);
            return;
        }

        int enemyDamage = Mathf.Max(1, currentBattle.enemyAttack - playerState.defense);
        playerState.hp -= enemyDamage;
        if (playerState.hp < 0) playerState.hp = 0;
        log += "\n" + currentBattle.enemyName + "反击，造成 " + enemyDamage + " 点伤害。";

        if (playerState.hp <= 0)
        {
            ApplyFlags(currentBattle.loseFlags);
            log += "\n" + currentBattle.loseMessage;
            playerState.hp = Mathf.Max(1, playerState.maxHp / 2);
            attackButton.GetComponentInChildren<Text>().text = "结束战斗";
            attackButton.onClick.RemoveAllListeners();
            attackButton.onClick.AddListener(CloseBattle);
        }

        RefreshBattleText(log);
        if (locationUIManager != null) locationUIManager.RefreshPlayerStatus(playerState);
    }

    private void RefreshBattleText(string log)
    {
        PlayerState playerState = gameManager.GetPlayerState();
        titleText.text = currentBattle.title;
        bodyText.text = "敌人：" + currentBattle.enemyName + "\n敌人血量：" + Mathf.Max(0, enemyHp) + "/" + currentBattle.enemyHp + "\n你的血量：" + playerState.hp + "/" + playerState.maxHp + "\n\n" + log;
    }

    private void ApplyFlags(string[] flags)
    {
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState == null || flags == null) return;
        foreach (string flag in flags) playerState.AddFlag(flag);
    }

    private void CloseBattle()
    {
        CloseBattleSilently();
        if (locationActionManager != null) locationActionManager.RefreshCurrentLocation();
        if (locationUIManager != null) locationUIManager.ShowMessage("战斗结束，你重新回到自由探索。 ");
    }

    public void CloseBattleSilently()
    {
        IsBattleOpen = false;
        if (panelObject != null) panelObject.SetActive(false);
        currentBattle = null;
    }

    private void EnsurePanel()
    {
        if (panelObject != null) return;
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;
        Font font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 20);
        panelObject = new GameObject("BattlePanel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        Image image = panelObject.GetComponent<Image>();
        image.color = new Color(0.03f, 0.02f, 0.02f, 0.90f);
        image.raycastTarget = true;
        titleText = CreateText(panelObject.transform, "BattleTitle", font, 30, TextAnchor.MiddleCenter, new Vector2(0.2f, 0.72f), new Vector2(0.8f, 0.82f));
        bodyText = CreateText(panelObject.transform, "BattleBody", font, 22, TextAnchor.MiddleCenter, new Vector2(0.2f, 0.38f), new Vector2(0.8f, 0.70f));
        attackButton = CreateButton(panelObject.transform, "AttackButton", "攻击");
        RectTransform buttonRect = attackButton.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.27f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.27f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        attackButton.onClick.AddListener(Attack);
    }

    private Text CreateText(Transform parent, string name, Font font, int size, TextAnchor align, Vector2 min, Vector2 max)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Text text = obj.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = align;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private Button CreateButton(Transform parent, string name, string text)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(160f, 48f);
        Image image = obj.GetComponent<Image>();
        image.color = new Color(0.28f, 0.15f, 0.15f, 1f);
        Button button = obj.GetComponent<Button>();
        button.targetGraphic = image;
        Text label = CreateText(obj.transform, "Text", titleText.font, 20, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
        label.text = text;
        return button;
    }
}

public class TutorialManager : MonoBehaviour
{
    private GameManager gameManager;
    private MapGridManager mapGridManager;
    private LocationUIManager locationUIManager;

    private void Start()
    {
        gameManager = GetComponent<GameManager>();
        mapGridManager = GetComponent<MapGridManager>();
        locationUIManager = GetComponent<LocationUIManager>();
    }

    private void Update()
    {
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState == null || locationUIManager == null) return;

        MapCellData currentCell = mapGridManager != null ? mapGridManager.GetCurrentCell() : null;
        if (currentCell != null && currentCell.actionIds != null && currentCell.actionIds.Length > 0 && !playerState.HasFlag("tutorial_action_shown"))
        {
            playerState.AddFlag("tutorial_action_shown");
            locationUIManager.ShowMessage("修炼、搜索、采集等行为会消耗行动点。每天只有 3 点行动点。");
        }

        if (playerState.actionPoints <= 0 && !playerState.HasFlag("tutorial_no_ap_shown"))
        {
            playerState.AddFlag("tutorial_no_ap_shown");
            locationUIManager.ShowMessage("今日行动点已用尽。你仍然可以移动和对话，也可以点击结束今日。");
        }
    }
}

public class SaveButtonOverrideManager : MonoBehaviour
{
    private GameManager gameManager;
    private MapGridManager mapGridManager;
    private LocationUIManager locationUIManager;
    private LocationActionManager locationActionManager;

    private string SaveFilePath
    {
        get { return Path.Combine(Application.persistentDataPath, "xianxia_save.json"); }
    }

    private IEnumerator Start()
    {
        yield return null;
        yield return null;
        gameManager = GetComponent<GameManager>();
        mapGridManager = GetComponent<MapGridManager>();
        locationUIManager = GetComponent<LocationUIManager>();
        locationActionManager = GetComponent<LocationActionManager>();
        Bind("SaveGameButton", SaveGame);
        Bind("LoadGameButton", LoadGame);
        Bind("NewGameButton", NewGame);
    }

    private void Bind(string objectName, UnityEngine.Events.UnityAction action)
    {
        GameObject obj = GameObject.Find(objectName);
        if (obj == null) return;
        Button button = obj.GetComponent<Button>();
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void SaveGame()
    {
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState == null) return;
        File.WriteAllText(SaveFilePath, JsonUtility.ToJson(playerState, true));
        if (locationUIManager != null) locationUIManager.ShowMessage("保存成功：" + SaveFilePath);
    }

    private void LoadGame()
    {
        if (!File.Exists(SaveFilePath))
        {
            if (locationUIManager != null) locationUIManager.ShowMessage("暂无存档");
            return;
        }

        PlayerState loaded = JsonUtility.FromJson<PlayerState>(File.ReadAllText(SaveFilePath));
        if (loaded == null)
        {
            if (locationUIManager != null) locationUIManager.ShowMessage("存档数据无效。");
            return;
        }

        loaded.EnsureLists();
        FieldInfo field = typeof(GameManager).GetField("playerState", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null) field.SetValue(gameManager, loaded);
        RefreshAfterStateChanged("读取存档成功。");
        OpeningStoryManager opening = GetComponent<OpeningStoryManager>();
        if (opening != null) opening.CheckOpeningAfterLoad();
    }

    private void NewGame()
    {
        if (gameManager == null) return;
        gameManager.InitNewGame();
        RefreshAfterStateChanged("新游戏开始。");
        OpeningStoryManager opening = GetComponent<OpeningStoryManager>();
        if (opening != null) opening.PlayOpeningIfNeeded();
    }

    private void RefreshAfterStateChanged(string message)
    {
        DayEventManager dayEvent = GetComponent<DayEventManager>();
        if (dayEvent != null) dayEvent.CloseDayEventSilently();
        BattleManager battle = GetComponent<BattleManager>();
        if (battle != null) battle.CloseBattleSilently();
        EventManager eventManager = GetComponent<EventManager>();
        if (eventManager != null) eventManager.CloseEventSilently();
        DialogueManager dialogueManager = GetComponent<DialogueManager>();
        if (dialogueManager != null) dialogueManager.CloseDialogueSilently();

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
    }
}
