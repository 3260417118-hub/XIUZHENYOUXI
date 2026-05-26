using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 第一版剧情事件管理器。
/// 用来显示首次进入地点事件，并处理事件选项效果。
/// </summary>
public class EventManager : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private LocationUIManager locationUIManager;
    [SerializeField] private LocationActionManager locationActionManager;
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private RectTransform optionButtonContainer;
    [SerializeField] private string eventDataResourcePath = "Data/events";

    private readonly Dictionary<string, EventData> eventById = new Dictionary<string, EventData>();
    private readonly List<GameObject> createdEventObjects = new List<GameObject>();
    private Font cachedFont;
    private bool isEventOpen;

    public bool IsEventOpen
    {
        get { return isEventOpen; }
    }

    public void SetReferences(
        GameManager game,
        LocationUIManager locationUI,
        LocationActionManager actionManager,
        DialogueManager dialogue,
        RectTransform optionContainer)
    {
        gameManager = game;
        locationUIManager = locationUI;
        locationActionManager = actionManager;
        dialogueManager = dialogue;
        optionButtonContainer = optionContainer;
    }

    private void Start()
    {
        FindMissingReferences();
        LoadEventData();
    }

    private void FindMissingReferences()
    {
        if (gameManager == null)
        {
            gameManager = GetComponent<GameManager>();
        }

        if (locationUIManager == null)
        {
            locationUIManager = GetComponent<LocationUIManager>();
        }

        if (locationActionManager == null)
        {
            locationActionManager = GetComponent<LocationActionManager>();
        }

        if (dialogueManager == null)
        {
            dialogueManager = GetComponent<DialogueManager>();
        }

        if (optionButtonContainer == null)
        {
            GameObject target = GameObject.Find("ActionButtonContainer");
            if (target != null)
            {
                optionButtonContainer = target.GetComponent<RectTransform>();
            }
        }
    }

    public void LoadEventData()
    {
        eventById.Clear();

        TextAsset jsonAsset = Resources.Load<TextAsset>(eventDataResourcePath);
        if (jsonAsset == null)
        {
            Debug.LogError("找不到事件数据：Resources/" + eventDataResourcePath + ".json");
            return;
        }

        EventDataList dataList = JsonUtility.FromJson<EventDataList>(jsonAsset.text);
        if (dataList == null || dataList.events == null)
        {
            Debug.LogError("事件数据格式不正确：" + eventDataResourcePath);
            return;
        }

        foreach (EventData eventData in dataList.events)
        {
            if (eventData == null || string.IsNullOrEmpty(eventData.id))
            {
                continue;
            }

            eventById[eventData.id] = eventData;
        }
    }

    public void TryShowFirstEnterEvent(MapCellData cell)
    {
        if (cell == null || string.IsNullOrEmpty(cell.firstEnterEventId))
        {
            return;
        }

        FindMissingReferences();

        if (gameManager == null)
        {
            return;
        }

        PlayerState playerState = gameManager.GetPlayerState();
        if (playerState == null)
        {
            return;
        }

        playerState.EnsureLists();
        if (playerState.HasVisitedCell(cell.id))
        {
            return;
        }

        // 先记录已访问，避免玩家关闭事件后反复触发。
        playerState.AddVisitedCell(cell.id);
        ShowEvent(cell.firstEnterEventId);
    }

    public void ShowEvent(string eventId)
    {
        if (eventById.Count == 0)
        {
            LoadEventData();
        }

        EventData eventData;
        if (string.IsNullOrEmpty(eventId) || !eventById.TryGetValue(eventId, out eventData))
        {
            if (locationUIManager != null)
            {
                locationUIManager.ShowMessage("找不到事件数据：" + eventId);
            }

            return;
        }

        isEventOpen = true;

        if (dialogueManager != null)
        {
            dialogueManager.CloseDialogueSilently();
        }

        if (locationActionManager != null)
        {
            locationActionManager.ClearCurrentButtons();
        }

        ClearEventOnly();

        if (locationUIManager != null)
        {
            locationUIManager.ShowEvent(eventData.title, eventData.text);
        }

        CreateEventOptionButtons(eventData);
    }

    private void CreateEventOptionButtons(EventData eventData)
    {
        if (optionButtonContainer == null)
        {
            return;
        }

        CreateLabel(optionButtonContainer, "事件：");

        if (eventData.options == null || eventData.options.Count == 0)
        {
            Button closeButton = CreateButton(optionButtonContainer, "离开");
            closeButton.onClick.AddListener(CloseEvent);
            return;
        }

        foreach (EventOptionData option in eventData.options)
        {
            if (option == null)
            {
                continue;
            }

            Button button = CreateButton(optionButtonContainer, option.text);
            EventOptionData capturedOption = option;
            button.onClick.AddListener(delegate { ExecuteOption(capturedOption); });
        }
    }

    private void ExecuteOption(EventOptionData option)
    {
        if (option == null)
        {
            return;
        }

        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState != null)
        {
            playerState.EnsureLists();

            if (option.setFlags != null)
            {
                foreach (string flag in option.setFlags)
                {
                    playerState.AddFlag(flag);
                }
            }

            if (option.cultivationGain != 0)
            {
                playerState.cultivation += option.cultivationGain;
            }

            if (option.spiritStoneGain != 0)
            {
                playerState.spiritStones += option.spiritStoneGain;
                if (playerState.spiritStones < 0)
                {
                    playerState.spiritStones = 0;
                }
            }

            if (locationUIManager != null)
            {
                locationUIManager.RefreshPlayerStatus(playerState);
            }
        }

        if (!string.IsNullOrEmpty(option.message) && locationUIManager != null)
        {
            locationUIManager.ShowMessage(option.message);
        }

        if (!string.IsNullOrEmpty(option.nextEventId))
        {
            ShowEvent(option.nextEventId);
            return;
        }

        if (option.closeEvent)
        {
            CloseEventAfterOptionMessage();
        }
    }

    private void CloseEventAfterOptionMessage()
    {
        isEventOpen = false;
        ClearEventOnly();

        if (locationActionManager != null)
        {
            locationActionManager.RefreshCurrentLocation();
        }
    }

    private void CloseEvent()
    {
        isEventOpen = false;
        ClearEventOnly();

        if (locationUIManager != null)
        {
            locationUIManager.ShowMessage("事件已结束。");
        }

        if (locationActionManager != null)
        {
            locationActionManager.RefreshCurrentLocation();
        }
    }

    /// <summary>
    /// 存档读取或新游戏时使用：只关闭事件 UI，不额外显示提示。
    /// </summary>
    public void CloseEventSilently()
    {
        isEventOpen = false;
        ClearEventOnly();
    }

    public void ClearEventOnly()
    {
        foreach (GameObject eventObject in createdEventObjects)
        {
            if (eventObject == null)
            {
                continue;
            }

            eventObject.SetActive(false);
            Destroy(eventObject);
        }

        createdEventObjects.Clear();
    }

    private Button CreateButton(RectTransform parent, string text)
    {
        GameObject buttonObject = new GameObject(text + "EventButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        createdEventObjects.Add(buttonObject);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(150f, 30f);

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredWidth = 150f;
        layout.preferredHeight = 30f;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.30f, 0.34f, 0.38f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.40f, 0.46f, 0.50f, 1f);
        colors.pressedColor = new Color(0.22f, 0.25f, 0.28f, 1f);
        button.colors = colors;

        Text label = CreateText(buttonObject.transform, "Text", text, 18, TextAnchor.MiddleCenter, Color.white);
        StretchToParent(label.rectTransform);

        return button;
    }

    private void CreateLabel(RectTransform parent, string text)
    {
        GameObject labelObject = new GameObject(text + "EventLabel", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        labelObject.transform.SetParent(parent, false);
        createdEventObjects.Add(labelObject);

        Text label = labelObject.GetComponent<Text>();
        label.text = text;
        label.font = GetDefaultFont();
        label.fontSize = 18;
        label.color = new Color(0.88f, 0.88f, 0.88f, 1f);
        label.alignment = TextAnchor.MiddleLeft;

        LayoutElement layout = labelObject.GetComponent<LayoutElement>();
        layout.preferredWidth = 78f;
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
        if (cachedFont != null)
        {
            return cachedFont;
        }

        cachedFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 16);
        if (cachedFont != null)
        {
            return cachedFont;
        }

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
