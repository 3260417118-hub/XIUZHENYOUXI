using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

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

[Serializable]
public class BlockingEncounterOptionData
{
    public string text;
    public string message;
    public string[] setFlags;
    public string startBattleId;
    public bool resolveEncounter;
    public bool closeOnly;

    // 轻量条件字段：用于关键剧情选项显示。
    public string[] requireFlags;
    public string[] excludeFlags;
    public string[] requireItems;
    public string[] excludeItems;
    public string[] requireSkills;
    public string[] excludeSkills;
    public int minCultivation;
    public int minDay;
    public int maxDay;
}

[Serializable]
public class BlockingEncounterData
{
    public string id;
    public int triggerDay;
    public string npcName;
    public string title;
    public string text;
    public string blockMoveMessage;
    public List<BlockingEncounterOptionData> options = new List<BlockingEncounterOptionData>();
    public string[] resolvedFlags;
    public bool spawnAtPlayerCurrentCell;
}

[Serializable]
public class BlockingEncounterDataList
{
    public List<BlockingEncounterData> encounters = new List<BlockingEncounterData>();
}

/// <summary>
/// 开场剧情管理器。
/// 新游戏第一次进入时，用全黑背景逐句淡入开场文字。
/// </summary>
public class OpeningStoryManager : MonoBehaviour
{
    public static bool IsOpeningActive { get; private set; }

    [SerializeField] private float lineFadeSeconds = 0.7f;
    [SerializeField] private float lineIntervalSeconds = 1.0f;

    private readonly string[] storyTexts = new string[]
    {
        "十年前，林家一夜血雨。",
        "林昊的父亲林远山，不知从何处得到一卷残破古卷。",
        "那卷轴上的文字无人能识，却引来了灭门之祸。",
        "那一夜，刀光、火光、血光，照亮了整个林家宅院。",
        "父亲拼死护着林昊、母亲和妹妹逃出重围。",
        "可逃亡途中，众人又遭遇伏击。",
        "混乱中，林昊与母亲失散，只记得自己死死拉着妹妹的手。",
        "再醒来时，他躺在村口草地上。",
        "头痛欲裂，记忆支离破碎。",
        "他只记得一件事——",
        "他还有一个妹妹。",
        "村口草木凌乱，远处山路雾气沉沉。",
        "林昊缓缓起身，开始寻找妹妹的下落。"
    };

    private GameManager gameManager;
    private LocationUIManager locationUIManager;
    private GameObject panelObject;
    private RectTransform lineContainer;
    private Button enterButton;
    private Button skipButton;
    private Coroutine playCoroutine;
    private Font cachedFont;

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
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState != null && playerState.hasSeenOpening)
        {
            HidePanelAndDeactivateOpening();
            ChapterTitleManager chapterTitle = GetOrCreateChapterTitleManager();
            if (chapterTitle != null) chapterTitle.HideImmediately();
            return;
        }
        PlayOpeningIfNeeded();
    }

    public void PlayOpeningIfNeeded()
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        if (locationUIManager == null) locationUIManager = GetComponent<LocationUIManager>();
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState == null || playerState.hasSeenOpening)
        {
            HidePanelAndDeactivateOpening();
            return;
        }

        EnsurePanel();
        IsOpeningActive = true;
        panelObject.SetActive(true);
        panelObject.transform.SetAsLastSibling();
        enterButton.gameObject.SetActive(false);
        skipButton.gameObject.SetActive(true);
        ClearLines();
        if (playCoroutine != null) StopCoroutine(playCoroutine);
        playCoroutine = StartCoroutine(PlayOpeningLines());
    }

    private IEnumerator PlayOpeningLines()
    {
        for (int i = 0; i < storyTexts.Length; i++)
        {
            Text lineText = CreateLineText(storyTexts[i], 0f);
            float timer = 0f;
            while (timer < lineFadeSeconds)
            {
                timer += Time.deltaTime;
                float alpha = Mathf.Clamp01(timer / Mathf.Max(0.01f, lineFadeSeconds));
                lineText.color = new Color(1f, 1f, 1f, alpha);
                yield return null;
            }
            lineText.color = Color.white;
            yield return new WaitForSeconds(lineIntervalSeconds);
        }
        ShowEnterGameButton();
    }

    private void OnSkipClicked()
    {
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }
        ShowAllOpeningTextImmediately();
        ShowEnterGameButton();
    }

    private void ShowAllOpeningTextImmediately()
    {
        ClearLines();
        foreach (string line in storyTexts) CreateLineText(line, 1f);
    }

    private void ShowEnterGameButton()
    {
        if (skipButton != null) skipButton.gameObject.SetActive(false);
        if (enterButton != null) enterButton.gameObject.SetActive(true);
    }

    private Text CreateLineText(string content, float alpha)
    {
        GameObject lineObject = new GameObject("OpeningLine", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        lineObject.transform.SetParent(lineContainer, false);
        Text text = lineObject.GetComponent<Text>();
        text.text = content;
        text.font = GetDefaultFont();
        text.fontSize = 22;
        text.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        LayoutElement layout = lineObject.GetComponent<LayoutElement>();
        layout.preferredHeight = 34f;
        return text;
    }

    private void OnEnterGameClicked()
    {
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }
        if (panelObject != null) panelObject.SetActive(false);
        IsOpeningActive = true;
        ChapterTitleManager chapterTitle = GetOrCreateChapterTitleManager();
        if (chapterTitle == null)
        {
            FinishOpeningAndEnterMap();
            return;
        }
        chapterTitle.PlayChapterTitle("第一章：青石村外", FinishOpeningAndEnterMap);
    }

    private void FinishOpeningAndEnterMap()
    {
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState != null)
        {
            playerState.hasSeenOpening = true;
            playerState.AddFlag("tutorial_move_shown");
        }
        IsOpeningActive = false;
        if (locationUIManager != null) locationUIManager.ShowMessage("你在村口醒来，记忆一片混乱。");
        LocationActionManager actionManager = GetComponent<LocationActionManager>();
        if (actionManager != null) actionManager.RefreshCurrentLocation();
    }

    private ChapterTitleManager GetOrCreateChapterTitleManager()
    {
        ChapterTitleManager chapterTitle = GetComponent<ChapterTitleManager>();
        if (chapterTitle == null) chapterTitle = gameObject.AddComponent<ChapterTitleManager>();
        return chapterTitle;
    }

    private void HidePanelAndDeactivateOpening()
    {
        IsOpeningActive = false;
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }
        if (panelObject != null) panelObject.SetActive(false);
    }

    private void EnsurePanel()
    {
        if (panelObject != null) return;
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;
        panelObject = new GameObject("OpeningPanel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvas.transform, false);
        Stretch(panelObject.GetComponent<RectTransform>());
        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = Color.black;
        panelImage.raycastTarget = true;

        GameObject containerObject = new GameObject("OpeningLineContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
        containerObject.transform.SetParent(panelObject.transform, false);
        lineContainer = containerObject.GetComponent<RectTransform>();
        lineContainer.anchorMin = new Vector2(0.14f, 0.20f);
        lineContainer.anchorMax = new Vector2(0.86f, 0.80f);
        lineContainer.offsetMin = Vector2.zero;
        lineContainer.offsetMax = Vector2.zero;
        VerticalLayoutGroup layout = containerObject.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 4f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        enterButton = CreateButton(panelObject.transform, "EnterGameButton", "进入游戏", new Vector2(180f, 54f), 22);
        RectTransform enterRect = enterButton.GetComponent<RectTransform>();
        enterRect.anchorMin = new Vector2(0.5f, 0.10f);
        enterRect.anchorMax = new Vector2(0.5f, 0.10f);
        enterRect.pivot = new Vector2(0.5f, 0.5f);
        enterRect.anchoredPosition = Vector2.zero;
        enterButton.onClick.AddListener(OnEnterGameClicked);
        enterButton.gameObject.SetActive(false);

        skipButton = CreateButton(panelObject.transform, "SkipOpeningButton", "跳过", new Vector2(110f, 42f), 18);
        RectTransform skipRect = skipButton.GetComponent<RectTransform>();
        skipRect.anchorMin = new Vector2(1f, 1f);
        skipRect.anchorMax = new Vector2(1f, 1f);
        skipRect.pivot = new Vector2(1f, 1f);
        skipRect.anchoredPosition = new Vector2(-32f, -32f);
        skipButton.onClick.AddListener(OnSkipClicked);
        skipButton.gameObject.SetActive(false);
    }

    private void ClearLines()
    {
        if (lineContainer == null) return;
        for (int i = lineContainer.childCount - 1; i >= 0; i--) Destroy(lineContainer.GetChild(i).gameObject);
    }

    private Button CreateButton(Transform parent, string objectName, string text, Vector2 size, int fontSize)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.15f, 0.15f, 0.18f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(buttonObject.transform, false);
        Stretch(textObject.GetComponent<RectTransform>());
        Text label = textObject.GetComponent<Text>();
        label.font = GetDefaultFont();
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.text = text;
        return button;
    }

    private Font GetDefaultFont()
    {
        if (cachedFont != null) return cachedFont;
        cachedFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 20);
        if (cachedFont != null) return cachedFont;
        cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return cachedFont;
    }

    private void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}

public class ChapterTitleManager : MonoBehaviour
{
    public static bool IsChapterTitleActive { get; private set; }
    [SerializeField] private float fadeInDuration = 1.2f;
    [SerializeField] private float holdDuration = 1.5f;
    [SerializeField] private float fadeOutDuration = 1.0f;
    private GameObject panelObject;
    private Text titleText;
    private Font cachedFont;
    private Coroutine playCoroutine;
    private Action finishCallback;

    public void PlayChapterTitle(string title, Action onFinished)
    {
        EnsurePanel();
        if (panelObject == null)
        {
            if (onFinished != null) onFinished.Invoke();
            return;
        }
        finishCallback = onFinished;
        if (playCoroutine != null) StopCoroutine(playCoroutine);
        panelObject.SetActive(true);
        panelObject.transform.SetAsLastSibling();
        titleText.text = title;
        titleText.color = new Color(1f, 1f, 1f, 0f);
        IsChapterTitleActive = true;
        playCoroutine = StartCoroutine(PlayRoutine());
    }

    public void HideImmediately()
    {
        IsChapterTitleActive = false;
        if (playCoroutine != null) StopCoroutine(playCoroutine);
        playCoroutine = null;
        if (panelObject != null) panelObject.SetActive(false);
    }

    private IEnumerator PlayRoutine()
    {
        yield return FadeTitle(0f, 1f, fadeInDuration);
        yield return new WaitForSeconds(holdDuration);
        yield return FadeTitle(1f, 0f, fadeOutDuration);
        IsChapterTitleActive = false;
        if (panelObject != null) panelObject.SetActive(false);
        Action callback = finishCallback;
        finishCallback = null;
        if (callback != null) callback.Invoke();
    }

    private IEnumerator FadeTitle(float from, float to, float duration)
    {
        float timer = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (timer < safeDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / safeDuration);
            titleText.color = new Color(1f, 1f, 1f, Mathf.Lerp(from, to, t));
            yield return null;
        }
        titleText.color = new Color(1f, 1f, 1f, to);
    }

    private void EnsurePanel()
    {
        if (panelObject != null) return;
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;
        panelObject = new GameObject("ChapterTitlePanel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        Image image = panelObject.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;
        GameObject textObject = new GameObject("ChapterTitleText", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(panelObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.1f, 0.42f);
        textRect.anchorMax = new Vector2(0.9f, 0.58f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        titleText = textObject.GetComponent<Text>();
        titleText.font = GetDefaultFont();
        titleText.fontSize = 42;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(1f, 1f, 1f, 0f);
        titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        titleText.verticalOverflow = VerticalWrapMode.Overflow;
        panelObject.SetActive(false);
    }

    private Font GetDefaultFont()
    {
        if (cachedFont != null) return cachedFont;
        cachedFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 32);
        if (cachedFont != null) return cachedFont;
        cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return cachedFont;
    }
}

/// <summary>
/// 阻塞式关键日期事件：到指定日期后，在玩家当前格生成临时 NPC。
/// </summary>
public class BlockingEncounterManager : MonoBehaviour
{
    public static BlockingEncounterManager Instance { get; private set; }
    private readonly Dictionary<string, BlockingEncounterData> encounterById = new Dictionary<string, BlockingEncounterData>();
    private GameManager gameManager;
    private LocationUIManager locationUIManager;
    private LocationActionManager locationActionManager;
    private EventManager eventManager;
    private DialogueManager dialogueManager;
    private bool boundEndDay;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        gameManager = GetComponent<GameManager>();
        locationUIManager = GetComponent<LocationUIManager>();
        locationActionManager = GetComponent<LocationActionManager>();
        eventManager = GetComponent<EventManager>();
        dialogueManager = GetComponent<DialogueManager>();
        LoadEncounters();
        StartCoroutine(BindAndCheckNextFrame());
    }

    private IEnumerator BindAndCheckNextFrame()
    {
        yield return null;
        BindEndDayButton();
        RestoreActiveEncounterUI();
        if (!OpeningStoryManager.IsOpeningActive && !ChapterTitleManager.IsChapterTitleActive) CheckTodayEncounter();
    }

    private void LoadEncounters()
    {
        encounterById.Clear();
        TextAsset jsonAsset = Resources.Load<TextAsset>("Data/blocking_encounters");
        if (jsonAsset == null) return;
        BlockingEncounterDataList dataList = JsonUtility.FromJson<BlockingEncounterDataList>(jsonAsset.text);
        if (dataList == null || dataList.encounters == null) return;
        foreach (BlockingEncounterData encounter in dataList.encounters)
        {
            if (encounter != null && !string.IsNullOrEmpty(encounter.id)) encounterById[encounter.id] = encounter;
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
        button.onClick.AddListener(EndDayAndCheckEncounter);
        boundEndDay = true;
    }

    public void EndDayAndCheckEncounter()
    {
        if (locationUIManager == null) locationUIManager = GetComponent<LocationUIManager>();
        if (locationUIManager != null) locationUIManager.ShowMessage("你需要回到破败小屋休息，才能结束今日。");
    }

    public void CheckTodayEncounter()
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        if (locationUIManager == null) locationUIManager = GetComponent<LocationUIManager>();
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState == null || OpeningStoryManager.IsOpeningActive || ChapterTitleManager.IsChapterTitleActive) return;
        playerState.EnsureLists();
        ChapterOneLocationMechanicsManager chapterOne = GetComponent<ChapterOneLocationMechanicsManager>();
        if (chapterOne != null) chapterOne.HandleDailyStoryTrigger();

        if (!string.IsNullOrEmpty(playerState.activeBlockingEncounterId))
        {
            RestoreActiveEncounterUI();
            return;
        }

        foreach (BlockingEncounterData encounter in encounterById.Values)
        {
            if (encounter.triggerDay == playerState.day && !playerState.HasTriggeredDayEvent(encounter.id))
            {
                playerState.activeBlockingEncounterId = encounter.id;
                RestoreActiveEncounterUI();
                if (locationUIManager != null) locationUIManager.ShowMessage(encounter.npcName + "出现在你面前。");
                return;
            }
        }
    }

    public bool HasActiveBlockingEncounter()
    {
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        return playerState != null && !string.IsNullOrEmpty(playerState.activeBlockingEncounterId);
    }

    public string GetBlockMoveMessageOrDefault()
    {
        BlockingEncounterData encounter = GetActiveEncounter();
        if (encounter != null && !string.IsNullOrEmpty(encounter.blockMoveMessage)) return encounter.blockMoveMessage;
        if (OpeningStoryManager.IsOpeningActive || ChapterTitleManager.IsChapterTitleActive) return "请先看完当前剧情。";
        return "有人拦住了你。";
    }

    public BlockingEncounterData GetActiveEncounter()
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState == null || string.IsNullOrEmpty(playerState.activeBlockingEncounterId)) return null;
        BlockingEncounterData encounter;
        encounterById.TryGetValue(playerState.activeBlockingEncounterId, out encounter);
        return encounter;
    }

    public string GetActiveEncounterNpcName()
    {
        BlockingEncounterData encounter = GetActiveEncounter();
        return encounter != null ? encounter.npcName : "";
    }

    public void StartActiveEncounterDialogue()
    {
        BlockingEncounterData encounter = GetActiveEncounter();
        if (encounter == null || locationUIManager == null) return;
        locationUIManager.ShowEvent(encounter.title, "\n" + GetEncounterText(encounter));
        RefreshLocationButtonsWithEncounterOptions(encounter);
    }

    private string GetEncounterText(BlockingEncounterData encounter)
    {
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState == null || encounter == null) return encounter != null ? encounter.text : "";
        if (encounter.id == "encounter_day4_thug_saw_sister")
        {
            if (playerState.HasFlag("rescued_sister") || playerState.HasFlag("sister_at_ruined_hut"))
            {
                return "第四日午后，一个吊儿郎当的小混混挡在林昊面前。他原本只是想敲诈几枚铜钱，可目光扫过破败小屋的方向时，眼神顿时变了。“听说你身边藏着个小丫头？嘿，跟着你这穷小子有什么前途？不如让她去赵爷府上享福。”";
            }
            return "第四日午后，一个吊儿郎当的小混混挡在林昊面前，斜着眼打量你：“外乡来的小子，最近在村里到处乱晃？识相点，把身上的东西交出来。”";
        }
        if (encounter.id == "encounter_day7_bully_helpers")
        {
            if (playerState.HasFlag("thug_reported_sister") || playerState.HasFlag("sister_at_ruined_hut"))
            {
                return "第七日清晨，两名赵霸天的帮手堵住去路。为首一人冷笑道：“就是你打了赵爷的人？听说你还藏着个漂亮丫头。赵爷近日有事脱不开身，便让我们先来请人。”";
            }
            return "第七日清晨，两名赵霸天的帮手堵住去路。为首一人冷笑道：“就是你这外乡小子，敢在青石村不给赵爷面子？”";
        }
        return encounter.text;
    }

    private void RefreshLocationButtonsWithEncounterOptions(BlockingEncounterData encounter)
    {
        LocationActionManager manager = locationActionManager != null ? locationActionManager : GetComponent<LocationActionManager>();
        if (manager == null) return;
        manager.ClearCurrentButtons();
        manager.CreateEncounterOptionButtons(encounter.options, ExecuteOption);
    }

    private void ExecuteOption(BlockingEncounterOptionData option)
    {
        if (option == null) return;
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState == null) return;
        ApplyFlags(option.setFlags);
        string optionMessage = option.message;
        if (!string.IsNullOrEmpty(optionMessage) && locationUIManager != null) locationUIManager.ShowMessage(optionMessage);
        if (!string.IsNullOrEmpty(option.startBattleId))
        {
            if (locationActionManager != null) locationActionManager.ClearCurrentButtons();
            BattleManager battleManager = GetComponent<BattleManager>();
            if (battleManager != null) battleManager.StartBattle(option.startBattleId, ResolveActiveEncounterAfterBattle);
            else if (option.resolveEncounter) ResolveActiveEncounter(optionMessage);
            return;
        }
        if (option.resolveEncounter) ResolveActiveEncounter(optionMessage);
        else if (option.closeOnly) RefreshLocationButtons();
    }

    private void ApplyFlags(string[] flags)
    {
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState == null || flags == null) return;
        foreach (string flag in flags) playerState.AddFlag(flag);
    }

    private void ResolveActiveEncounterAfterBattle()
    {
        ResolveActiveEncounter("");
    }

    public void ResolveActiveEncounter()
    {
        ResolveActiveEncounter("");
    }

    public void ResolveActiveEncounter(string finalMessage)
    {
        BlockingEncounterData encounter = GetActiveEncounter();
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState == null) return;
        if (encounter != null)
        {
            ApplyFlags(encounter.resolvedFlags);
            playerState.AddTriggeredDayEvent(encounter.id);
        }
        playerState.activeBlockingEncounterId = "";
        RefreshLocationButtons();
        if (!string.IsNullOrEmpty(finalMessage) && locationUIManager != null) locationUIManager.ShowMessage(finalMessage);
    }

    public void RestoreActiveEncounterUI()
    {
        RefreshLocationButtons();
    }

    private void RefreshLocationButtons()
    {
        if (locationActionManager == null) locationActionManager = GetComponent<LocationActionManager>();
        if (locationActionManager != null) locationActionManager.RefreshCurrentLocation();
    }
}

public class DayEventManager : MonoBehaviour
{
    public static bool IsDayEventOpen { get { return false; } }
    public void CheckTodayEvent() { }
    public void CloseDayEventSilently() { }
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
    private Action battleFinishedCallback;
    private string lastBattleResultMessage = "";

    private void Start()
    {
        gameManager = GetComponent<GameManager>();
        locationUIManager = GetComponent<LocationUIManager>();
        locationActionManager = GetComponent<LocationActionManager>();
        LoadBattles();
    }

    private void LoadBattles()
    {
        battleById.Clear();
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
        StartBattle(battleId, null);
    }

    public void StartBattle(string battleId, Action onFinished)
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        if (locationUIManager == null) locationUIManager = GetComponent<LocationUIManager>();
        if (battleById.Count == 0) LoadBattles();
        BattleData battle;
        if (!battleById.TryGetValue(battleId, out battle))
        {
            if (locationUIManager != null) locationUIManager.ShowMessage("找不到战斗数据：" + battleId);
            return;
        }
        currentBattle = battle;
        enemyHp = battle.enemyHp;
        battleFinishedCallback = onFinished;
        lastBattleResultMessage = "";
        EnsurePanel();
        IsBattleOpen = true;
        panelObject.SetActive(true);
        panelObject.transform.SetAsLastSibling();
        attackButton.onClick.RemoveAllListeners();
        attackButton.onClick.AddListener(Attack);
        attackButton.GetComponentInChildren<Text>().text = "攻击";
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
            lastBattleResultMessage = currentBattle.winMessage;
            log += "\n" + currentBattle.winMessage;
            RefreshBattleText(log);
            SetFinishButton();
            return;
        }
        int enemyDamage = Mathf.Max(1, currentBattle.enemyAttack - playerState.defense);
        playerState.hp -= enemyDamage;
        if (playerState.hp < 0) playerState.hp = 0;
        log += "\n" + currentBattle.enemyName + "反击，造成 " + enemyDamage + " 点伤害。";
        if (playerState.hp <= 0)
        {
            ApplyFlags(currentBattle.loseFlags);
            lastBattleResultMessage = currentBattle.loseMessage;
            log += "\n" + currentBattle.loseMessage;
            playerState.hp = Mathf.Max(1, playerState.maxHp / 2);
            SetFinishButton();
        }
        RefreshBattleText(log);
        if (locationUIManager != null) locationUIManager.RefreshPlayerStatus(playerState);
    }

    private void SetFinishButton()
    {
        Text label = attackButton.GetComponentInChildren<Text>();
        if (label != null) label.text = "结束战斗";
        attackButton.onClick.RemoveAllListeners();
        attackButton.onClick.AddListener(CloseBattle);
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
        Action callback = battleFinishedCallback;
        string resultMessage = lastBattleResultMessage;
        CloseBattleSilently();
        if (callback != null) callback.Invoke();
        if (locationActionManager != null) locationActionManager.RefreshCurrentLocation();
        if (!string.IsNullOrEmpty(resultMessage) && locationUIManager != null) locationUIManager.ShowMessage(resultMessage);
    }

    public void CloseBattleSilently()
    {
        IsBattleOpen = false;
        if (panelObject != null) panelObject.SetActive(false);
        currentBattle = null;
        battleFinishedCallback = null;
        lastBattleResultMessage = "";
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
        if (OpeningStoryManager.IsOpeningActive || ChapterTitleManager.IsChapterTitleActive) return;
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
            locationUIManager.ShowMessage("今日行动点已用尽。你仍然可以移动和对话，也可以点击休息过夜。");
        }
    }
}

public class SaveButtonOverrideManager : MonoBehaviour
{
    private GameManager gameManager;
    private MapGridManager mapGridManager;
    private LocationUIManager locationUIManager;
    private LocationActionManager locationActionManager;

    private string SaveFilePath { get { return Path.Combine(Application.persistentDataPath, "xianxia_save.json"); } }

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
        if (RestManager.IsRestingTransition) { if (locationUIManager != null) locationUIManager.ShowMessage("正在休息过夜，暂时不能保存。"); return; }
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState == null) return;
        playerState.EnsureLists();
        File.WriteAllText(SaveFilePath, JsonUtility.ToJson(playerState, true));
        if (locationUIManager != null) locationUIManager.ShowMessage("保存成功：" + SaveFilePath);
    }

    private void LoadGame()
    {
        if (RestManager.IsRestingTransition) { if (locationUIManager != null) locationUIManager.ShowMessage("正在休息过夜，暂时不能读取。"); return; }
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
        BlockingEncounterManager encounterManager = GetComponent<BlockingEncounterManager>();
        if (encounterManager != null) encounterManager.RestoreActiveEncounterUI();
    }

    private void NewGame()
    {
        if (RestManager.IsRestingTransition) { if (locationUIManager != null) locationUIManager.ShowMessage("正在休息过夜，暂时不能新游戏。"); return; }
        if (gameManager == null) return;
        gameManager.InitNewGame();
        RefreshAfterStateChanged("新游戏开始。");
        OpeningStoryManager opening = GetComponent<OpeningStoryManager>();
        if (opening != null) opening.PlayOpeningIfNeeded();
    }

    private void RefreshAfterStateChanged(string message)
    {
        BlockingEncounterManager encounterManager = GetComponent<BlockingEncounterManager>();
        if (encounterManager != null) encounterManager.RestoreActiveEncounterUI();
        DayEventManager dayEvent = GetComponent<DayEventManager>();
        if (dayEvent != null) dayEvent.CloseDayEventSilently();
        BattleManager battle = GetComponent<BattleManager>();
        if (battle != null) battle.CloseBattleSilently();
        ChapterOneLocationMechanicsManager chapterOne = GetComponent<ChapterOneLocationMechanicsManager>();
        if (chapterOne != null) chapterOne.CloseChapterOneEventSilently();
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
