using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 最简单的 NPC 对话管理器。
/// 负责读取 NPC / 对话 JSON，点击 NPC 后显示对话和选项按钮。
/// 对话不消耗行动点。
/// </summary>
public class DialogueManager : MonoBehaviour
{
    [SerializeField] private LocationUIManager locationUIManager;
    [SerializeField] private LocationActionManager locationActionManager;
    [SerializeField] private RectTransform optionButtonContainer;
    [SerializeField] private RectTransform npcButtonContainer;
    [SerializeField] private string npcDataResourcePath = "Data/npcs";
    [SerializeField] private string dialogueDataResourcePath = "Data/dialogues";

    private readonly Dictionary<string, NPCData> npcById = new Dictionary<string, NPCData>();
    private readonly Dictionary<string, DialogueData> dialogueById = new Dictionary<string, DialogueData>();
    private readonly List<GameObject> createdDialogueObjects = new List<GameObject>();
    private Font cachedFont;
    private bool isDialogueOpen;

    public bool IsDialogueOpen
    {
        get { return isDialogueOpen; }
    }

    public void SetReferences(LocationUIManager locationUI, LocationActionManager actionManager, RectTransform optionContainer, RectTransform npcContainer)
    {
        locationUIManager = locationUI;
        locationActionManager = actionManager;
        optionButtonContainer = optionContainer;
        npcButtonContainer = npcContainer;
    }

    private void Start()
    {
        LoadNpcData();
        LoadDialogueData();
    }

    public void LoadNpcData()
    {
        npcById.Clear();
        TextAsset jsonAsset = Resources.Load<TextAsset>(npcDataResourcePath);
        if (jsonAsset == null)
        {
            Debug.LogError("找不到 NPC 数据：Resources/" + npcDataResourcePath + ".json");
            return;
        }

        NPCDataList dataList = JsonUtility.FromJson<NPCDataList>(jsonAsset.text);
        if (dataList == null || dataList.npcs == null)
        {
            Debug.LogError("NPC 数据格式不正确：" + npcDataResourcePath);
            return;
        }

        foreach (NPCData npc in dataList.npcs)
        {
            if (npc == null || string.IsNullOrEmpty(npc.id)) continue;
            npcById[npc.id] = npc;
        }
    }

    public void LoadDialogueData()
    {
        dialogueById.Clear();
        TextAsset jsonAsset = Resources.Load<TextAsset>(dialogueDataResourcePath);
        if (jsonAsset == null)
        {
            Debug.LogError("找不到对话数据：Resources/" + dialogueDataResourcePath + ".json");
            return;
        }

        DialogueDataList dataList = JsonUtility.FromJson<DialogueDataList>(jsonAsset.text);
        if (dataList == null || dataList.dialogues == null)
        {
            Debug.LogError("对话数据格式不正确：" + dialogueDataResourcePath);
            return;
        }

        foreach (DialogueData dialogue in dataList.dialogues)
        {
            if (dialogue == null || string.IsNullOrEmpty(dialogue.id)) continue;
            dialogueById[dialogue.id] = dialogue;
        }
    }

    public string GetNpcName(string npcId)
    {
        NPCData npc;
        if (!string.IsNullOrEmpty(npcId) && npcById.TryGetValue(npcId, out npc)) return npc.name;
        return npcId;
    }

    public void StartDialogueByNpcId(string npcId)
    {
        if (RestManager.IsRestingTransition) return;
        if (npcById.Count == 0) LoadNpcData();
        if (dialogueById.Count == 0) LoadDialogueData();

        NPCData npc;
        if (!npcById.TryGetValue(npcId, out npc))
        {
            if (locationUIManager != null) locationUIManager.ShowMessage("找不到 NPC 数据：" + npcId);
            return;
        }

        ShowDialogue(npc.dialogueId);
    }

    private void ShowDialogue(string dialogueId)
    {
        DialogueData dialogue;
        if (string.IsNullOrEmpty(dialogueId) || !dialogueById.TryGetValue(dialogueId, out dialogue))
        {
            if (locationUIManager != null) locationUIManager.ShowMessage("找不到对话数据：" + dialogueId);
            return;
        }

        isDialogueOpen = true;
        if (locationActionManager != null) locationActionManager.ClearCurrentButtons();
        ClearDialogueOnly();
        if (locationUIManager != null) locationUIManager.ShowDialogue(dialogue.speaker, dialogue.text);
        CreateDialogueOptionButtons(dialogue);
    }

    private void CreateDialogueOptionButtons(DialogueData dialogue)
    {
        if (optionButtonContainer == null) return;
        CreateLabel(optionButtonContainer, "选项：");

        if (dialogue.options == null || dialogue.options.Count == 0)
        {
            Button closeButton = CreateButton(optionButtonContainer, "离开");
            closeButton.onClick.AddListener(CloseDialogue);
            return;
        }

        PlayerState playerState = GameManager.Instance != null ? GameManager.Instance.GetPlayerState() : null;
        bool createdAny = false;
        foreach (DialogueOptionData option in dialogue.options)
        {
            if (option == null) continue;
            if (!IsOptionVisible(option, playerState)) continue;
            Button button = CreateButton(optionButtonContainer, option.text);
            DialogueOptionData capturedOption = option;
            button.onClick.AddListener(delegate { ExecuteOption(capturedOption); });
            createdAny = true;
        }

        if (!createdAny)
        {
            Button closeButton = CreateButton(optionButtonContainer, "离开");
            closeButton.onClick.AddListener(CloseDialogue);
        }
    }

    private bool IsOptionVisible(DialogueOptionData option, PlayerState playerState)
    {
        if (option == null) return false;
        if (!ConditionUtility.IsMet(playerState, option.condition)) return false;
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

    private void ExecuteOption(DialogueOptionData option)
    {
        if (option == null) return;
        if (RestManager.IsRestingTransition) return;

        ApplyDialogueOptionEffects(option);

        if (option.action == "close")
        {
            if (!string.IsNullOrEmpty(option.message) && locationUIManager != null) locationUIManager.ShowMessage(option.message);
            CloseDialogueWithoutDefaultMessage();
            return;
        }

        if (option.action == "message")
        {
            if (locationUIManager != null) locationUIManager.ShowMessage(option.message);
            if (locationActionManager != null) locationActionManager.RefreshCurrentLocation();
            return;
        }

        if (option.action == "dialogue")
        {
            ShowDialogue(option.target);
            return;
        }

        if (locationUIManager != null) locationUIManager.ShowMessage("未知对话选项动作：" + option.action);
    }

    private void ApplyDialogueOptionEffects(DialogueOptionData option)
    {
        PlayerState playerState = GameManager.Instance != null ? GameManager.Instance.GetPlayerState() : null;
        if (playerState == null || option == null) return;

        if (option.setFlags != null)
        {
            foreach (string flag in option.setFlags) playerState.AddFlag(flag);
        }
        if (option.addItems != null)
        {
            foreach (string item in option.addItems) playerState.AddItem(item);
        }
        if (option.removeItems != null)
        {
            foreach (string item in option.removeItems) playerState.RemoveItem(item);
        }
        if (option.learnSkills != null)
        {
            foreach (string skill in option.learnSkills) playerState.LearnSkill(skill);
        }
        if (option.cultivationGain != 0) playerState.cultivation += option.cultivationGain;
        if (option.spiritStoneGain != 0) playerState.spiritStones += option.spiritStoneGain;
        if (locationUIManager != null) locationUIManager.RefreshPlayerStatus(playerState);
    }

    private void CloseDialogue()
    {
        isDialogueOpen = false;
        ClearDialogueOnly();
        if (locationUIManager != null) locationUIManager.ShowMessage("对话已结束。");
        if (locationActionManager != null) locationActionManager.RefreshCurrentLocation();
    }

    private void CloseDialogueWithoutDefaultMessage()
    {
        isDialogueOpen = false;
        ClearDialogueOnly();
        if (locationActionManager != null) locationActionManager.RefreshCurrentLocation();
    }

    public void CloseDialogueSilently()
    {
        isDialogueOpen = false;
        ClearDialogueOnly();
    }

    public void ClearDialogueOnly()
    {
        foreach (GameObject dialogueObject in createdDialogueObjects)
        {
            if (dialogueObject == null) continue;
            dialogueObject.SetActive(false);
            Destroy(dialogueObject);
        }
        createdDialogueObjects.Clear();
    }

    private Button CreateButton(RectTransform parent, string text)
    {
        GameObject buttonObject = new GameObject(text + "DialogueButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        createdDialogueObjects.Add(buttonObject);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(150f, 30f);
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
        GameObject labelObject = new GameObject(text + "DialogueLabel", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        labelObject.transform.SetParent(parent, false);
        createdDialogueObjects.Add(labelObject);
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
