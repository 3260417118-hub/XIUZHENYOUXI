using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 负责根据当前地点生成行为按钮和 NPC 按钮。
/// 地点行为会消耗行动点；NPC 对话和阻塞事件 NPC 不消耗行动点。
/// </summary>
public class LocationActionManager : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private MapGridManager mapGridManager;
    [SerializeField] private ActionPointManager actionPointManager;
    [SerializeField] private LocationUIManager locationUIManager;
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private RectTransform actionButtonContainer;
    [SerializeField] private RectTransform npcButtonContainer;
    [SerializeField] private string actionDataResourcePath = "Data/location_actions";

    private readonly Dictionary<string, LocationActionData> actionById = new Dictionary<string, LocationActionData>();
    private readonly List<GameObject> createdButtons = new List<GameObject>();
    private Font cachedFont;

    public void SetReferences(
        GameManager game,
        MapGridManager mapGrid,
        ActionPointManager actionPoint,
        LocationUIManager locationUI,
        RectTransform actionContainer,
        RectTransform npcContainer)
    {
        gameManager = game;
        mapGridManager = mapGrid;
        actionPointManager = actionPoint;
        locationUIManager = locationUI;
        actionButtonContainer = actionContainer;
        npcButtonContainer = npcContainer;
        EnsureDialogueManager();
    }

    private void Start()
    {
        LoadActions();
        EnsureDialogueManager();
        RefreshCurrentLocation();
    }

    private void EnsureDialogueManager()
    {
        if (dialogueManager == null) dialogueManager = GetComponent<DialogueManager>();
        if (dialogueManager == null) dialogueManager = gameObject.AddComponent<DialogueManager>();
        dialogueManager.SetReferences(locationUIManager, this, actionButtonContainer, npcButtonContainer);
    }

    public void LoadActions()
    {
        actionById.Clear();
        TextAsset jsonAsset = Resources.Load<TextAsset>(actionDataResourcePath);
        if (jsonAsset == null)
        {
            Debug.LogError("找不到地点行为数据：Resources/" + actionDataResourcePath + ".json");
            return;
        }

        LocationActionDataList dataList = JsonUtility.FromJson<LocationActionDataList>(jsonAsset.text);
        if (dataList == null || dataList.actions == null)
        {
            Debug.LogError("地点行为数据格式不正确：" + actionDataResourcePath);
            return;
        }

        foreach (LocationActionData action in dataList.actions)
        {
            if (action == null || string.IsNullOrEmpty(action.id)) continue;
            actionById[action.id] = action;
        }
    }

    public void RefreshCurrentLocation()
    {
        ClearCurrentButtons();
        EnsureDialogueManager();

        if (RestManager.IsRestingTransition || BattleManager.IsBattleOpen || OpeningStoryManager.IsOpeningActive || ChapterTitleManager.IsChapterTitleActive)
        {
            return;
        }

        if (dialogueManager != null && dialogueManager.IsDialogueOpen) return;
        if (mapGridManager == null) return;

        MapCellData currentCell = mapGridManager.GetCurrentCell();
        if (currentCell == null) return;

        CreateActionButtons(currentCell);
        CreateNpcButtons(currentCell);
    }

    public void ClearCurrentButtons()
    {
        ClearButtons();
    }

    public void CreateEncounterOptionButtons(List<BlockingEncounterOptionData> options, System.Action<BlockingEncounterOptionData> onOptionClicked)
    {
        ClearCurrentButtons();
        if (actionButtonContainer == null) return;
        CreateLabel(actionButtonContainer, "选项：");

        if (options == null || options.Count == 0)
        {
            CreateLabel(actionButtonContainer, "无");
            return;
        }

        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        bool createdAny = false;
        foreach (BlockingEncounterOptionData option in options)
        {
            if (option == null) continue;
            if (!IsBlockingEncounterOptionVisible(option, playerState)) continue;
            BlockingEncounterOptionData captured = option;
            Button button = CreateButton(actionButtonContainer, option.text, 140f);
            button.onClick.AddListener(delegate
            {
                if (RestManager.IsRestingTransition) return;
                if (onOptionClicked != null) onOptionClicked(captured);
            });
            createdAny = true;
        }

        if (!createdAny) CreateLabel(actionButtonContainer, "无");
    }

    private bool IsBlockingEncounterOptionVisible(BlockingEncounterOptionData option, PlayerState playerState)
    {
        if (option == null) return false;
        return ConditionUtility.IsMet(
            playerState,
            option.requireFlags,
            option.excludeFlags,
            option.requireItems,
            option.excludeItems,
            option.requireSkills,
            option.excludeSkills,
            option.minCultivation,
            option.minDay,
            option.maxDay);
    }

    private void CreateActionButtons(MapCellData currentCell)
    {
        if (actionButtonContainer == null) return;
        CreateLabel(actionButtonContainer, "可执行：");

        if (currentCell.actionIds == null || currentCell.actionIds.Length == 0)
        {
            CreateLabel(actionButtonContainer, "无");
            return;
        }

        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        bool createdAny = false;
        foreach (string actionId in currentCell.actionIds)
        {
            LocationActionData actionData;
            if (!actionById.TryGetValue(actionId, out actionData)) continue;
            if (!ConditionUtility.IsMet(playerState, actionData.condition)) continue;

            Button button = CreateButton(actionButtonContainer, actionData.name, 120f);
            LocationActionData capturedAction = actionData;
            button.onClick.AddListener(delegate { ExecuteAction(capturedAction); });
            RefreshActionButtonInteractable(button, actionData);
            createdAny = true;
        }

        if (!createdAny) CreateLabel(actionButtonContainer, "无");
    }

    private void CreateNpcButtons(MapCellData currentCell)
    {
        if (npcButtonContainer == null) return;
        CreateLabel(npcButtonContainer, "人物：");
        bool hasAnyNpc = false;

        if (currentCell.npcIds != null)
        {
            foreach (string npcId in currentCell.npcIds)
            {
                string npcName = GetNpcName(npcId);
                Button button = CreateButton(npcButtonContainer, npcName, 120f);
                string capturedNpcId = npcId;
                button.onClick.AddListener(delegate
                {
                    if (RestManager.IsRestingTransition) return;
                    StartNpcDialogue(capturedNpcId);
                });
                hasAnyNpc = true;
            }
        }

        BlockingEncounterManager blockingEncounterManager = GetComponent<BlockingEncounterManager>();
        if (blockingEncounterManager != null && blockingEncounterManager.HasActiveBlockingEncounter())
        {
            string encounterNpcName = blockingEncounterManager.GetActiveEncounterNpcName();
            if (!string.IsNullOrEmpty(encounterNpcName))
            {
                Button button = CreateButton(npcButtonContainer, encounterNpcName, 120f);
                button.onClick.AddListener(delegate
                {
                    if (RestManager.IsRestingTransition) return;
                    blockingEncounterManager.StartActiveEncounterDialogue();
                });
                hasAnyNpc = true;
            }
        }

        if (!hasAnyNpc) CreateLabel(npcButtonContainer, "无");
    }

    private void StartNpcDialogue(string npcId)
    {
        EnsureDialogueManager();
        if (dialogueManager == null)
        {
            if (locationUIManager != null) locationUIManager.ShowMessage("对话系统未初始化。");
            return;
        }
        dialogueManager.StartDialogueByNpcId(npcId);
    }

    private void ExecuteAction(LocationActionData actionData)
    {
        if (RestManager.IsRestingTransition || OpeningStoryManager.IsOpeningActive || ChapterTitleManager.IsChapterTitleActive || BattleManager.IsBattleOpen)
        {
            if (locationUIManager != null) locationUIManager.ShowMessage("请先处理当前事件。");
            return;
        }

        if (actionData == null || gameManager == null) return;

        if (actionData.id == "rest_at_ruined_hut")
        {
            RestManager restManager = GetComponent<RestManager>();
            if (restManager != null) restManager.SleepUntilNextDay();
            else if (locationUIManager != null) locationUIManager.ShowMessage("休息系统未初始化。");
            return;
        }

        if (actionPointManager == null) return;
        if (!actionPointManager.TrySpendActionPoints(actionData.costActionPoint))
        {
            RefreshCurrentLocation();
            return;
        }

        PlayerState playerState = gameManager.GetPlayerState();
        if (actionData.cultivationGain > 0) playerState.cultivation += actionData.cultivationGain;

        if (locationUIManager != null)
        {
            locationUIManager.RefreshPlayerStatus(playerState);
            locationUIManager.ShowMessage(actionData.message);
        }

        RefreshCurrentLocation();
    }

    private void RefreshActionButtonInteractable(Button button, LocationActionData actionData)
    {
        if (button == null || actionData == null || actionPointManager == null) return;
        bool hasEnough = actionData.id == "rest_at_ruined_hut" || actionPointManager.HasEnoughActionPoints(actionData.costActionPoint);
        button.interactable = true;

        Color normalColor = hasEnough ? new Color(0.30f, 0.34f, 0.38f, 1f) : new Color(0.18f, 0.19f, 0.21f, 1f);
        ColorBlock colors = button.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor = hasEnough ? new Color(0.40f, 0.46f, 0.50f, 1f) : new Color(0.22f, 0.23f, 0.25f, 1f);
        colors.pressedColor = hasEnough ? new Color(0.22f, 0.25f, 0.28f, 1f) : new Color(0.16f, 0.16f, 0.18f, 1f);
        colors.disabledColor = new Color(0.18f, 0.19f, 0.21f, 1f);
        button.colors = colors;

        Image image = button.targetGraphic as Image;
        if (image != null) image.color = normalColor;
    }

    private string GetNpcName(string npcId)
    {
        EnsureDialogueManager();
        if (dialogueManager != null)
        {
            string npcName = dialogueManager.GetNpcName(npcId);
            if (!string.IsNullOrEmpty(npcName)) return npcName;
        }
        return npcId;
    }

    private Button CreateButton(RectTransform parent, string text, float preferredWidth)
    {
        GameObject buttonObject = new GameObject(text + "Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        createdButtons.Add(buttonObject);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(preferredWidth, 30f);
        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredWidth = preferredWidth;
        layout.preferredHeight = 30f;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.30f, 0.34f, 0.38f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.40f, 0.46f, 0.50f, 1f);
        colors.pressedColor = new Color(0.22f, 0.25f, 0.28f, 1f);
        colors.disabledColor = new Color(0.18f, 0.19f, 0.21f, 1f);
        button.colors = colors;

        Text label = CreateText(buttonObject.transform, "Text", text, 18, TextAnchor.MiddleCenter, Color.white);
        StretchToParent(label.rectTransform);
        return button;
    }

    private void CreateLabel(RectTransform parent, string text)
    {
        GameObject labelObject = new GameObject(text + "Label", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        labelObject.transform.SetParent(parent, false);
        createdButtons.Add(labelObject);

        Text label = labelObject.GetComponent<Text>();
        label.text = text;
        label.font = GetDefaultFont();
        label.fontSize = 18;
        label.color = new Color(0.88f, 0.88f, 0.88f, 1f);
        label.alignment = TextAnchor.MiddleLeft;

        LayoutElement layout = labelObject.GetComponent<LayoutElement>();
        layout.preferredWidth = text == "无" ? 36f : 78f;
        layout.preferredHeight = 30f;
    }

    private Text CreateText(Transform parent, string name, string text, int fontSize, TextAnchor alignment, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text label = textObject.GetComponent<Text>();
        label.text = text;
        label.font = GetDefaultFont();
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = color;
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

    private void ClearButtons()
    {
        foreach (GameObject buttonObject in createdButtons)
        {
            if (buttonObject == null) continue;
            buttonObject.SetActive(false);
            Destroy(buttonObject);
        }
        createdButtons.Clear();
    }
}
