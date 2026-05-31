using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class BodyRealmData
{
    public string id;
    public string name;
    public int level;
    public int requiredBodyCultivation;
    public int maxHpBonus;
    public int attackBonus;
    public int defenseBonus;
    public string breakthroughMessage;
}

[Serializable]
public class BodyRealmDataList
{
    public List<BodyRealmData> bodyRealms = new List<BodyRealmData>();
}

/// <summary>
/// 统一重算属性，避免突破、读档、装备变化时重复叠加。
/// </summary>
public static class PlayerStatCalculator
{
    public static void RecalculateStats(PlayerState state, RealmManager realmManager, BodyRealmManager bodyRealmManager, bool healToFull)
    {
        if (state == null) return;
        state.EnsureLists();

        int oldMaxHp = state.maxHp > 0 ? state.maxHp : state.baseMaxHp;
        int oldHp = state.hp > 0 ? state.hp : oldMaxHp;

        int maxHp = state.baseMaxHp;
        int attack = state.baseAttack;
        int defense = state.baseDefense;

        RealmData currentRealm = realmManager != null ? realmManager.GetCurrentRealm() : null;
        if (currentRealm != null)
        {
            maxHp += currentRealm.maxHpBonus;
            attack += currentRealm.attackBonus;
            defense += currentRealm.defenseBonus;
        }

        BodyRealmData currentBodyRealm = bodyRealmManager != null ? bodyRealmManager.GetCurrentBodyRealm() : null;
        if (currentBodyRealm != null)
        {
            maxHp += currentBodyRealm.maxHpBonus;
            attack += currentBodyRealm.attackBonus;
            defense += currentBodyRealm.defenseBonus;
        }

        state.maxHp = Mathf.Max(1, maxHp);
        state.attack = Mathf.Max(1, attack);
        state.defense = Mathf.Max(0, defense);
        state.hp = healToFull ? state.maxHp : Mathf.Clamp(oldHp, 1, state.maxHp);
    }
}

/// <summary>
/// 肉身锻体境界管理器。
/// </summary>
public class BodyRealmManager : MonoBehaviour
{
    [SerializeField] private string bodyRealmDataResourcePath = "Data/body_realms";

    private readonly Dictionary<int, BodyRealmData> bodyRealmByLevel = new Dictionary<int, BodyRealmData>();
    private GameManager gameManager;
    private LocationUIManager locationUIManager;

    private void Start()
    {
        gameManager = GetComponent<GameManager>();
        locationUIManager = GetComponent<LocationUIManager>();
        LoadBodyRealms();
        NormalizeBodyRealm();
    }

    public void LoadBodyRealms()
    {
        bodyRealmByLevel.Clear();
        TextAsset jsonAsset = Resources.Load<TextAsset>(bodyRealmDataResourcePath);
        if (jsonAsset == null)
        {
            Debug.LogError("找不到肉身境界数据：Resources/" + bodyRealmDataResourcePath + ".json");
            return;
        }

        BodyRealmDataList dataList = JsonUtility.FromJson<BodyRealmDataList>(jsonAsset.text);
        if (dataList == null || dataList.bodyRealms == null)
        {
            Debug.LogError("肉身境界数据格式不正确：" + bodyRealmDataResourcePath);
            return;
        }

        foreach (BodyRealmData bodyRealm in dataList.bodyRealms)
        {
            if (bodyRealm == null) continue;
            bodyRealmByLevel[bodyRealm.level] = bodyRealm;
        }
    }

    public BodyRealmData GetCurrentBodyRealm()
    {
        PlayerState state = GetState();
        if (state == null) return null;
        if (bodyRealmByLevel.Count == 0) LoadBodyRealms();
        BodyRealmData result;
        return bodyRealmByLevel.TryGetValue(state.bodyRealmLevel, out result) ? result : null;
    }

    public BodyRealmData GetNextBodyRealm()
    {
        PlayerState state = GetState();
        if (state == null) return null;
        if (bodyRealmByLevel.Count == 0) LoadBodyRealms();
        BodyRealmData result;
        return bodyRealmByLevel.TryGetValue(state.bodyRealmLevel + 1, out result) ? result : null;
    }

    public bool CanBodyBreakthrough()
    {
        PlayerState state = GetState();
        BodyRealmData next = GetNextBodyRealm();
        return state != null && next != null && state.bodyCultivation >= next.requiredBodyCultivation;
    }

    public bool TryBodyBreakthrough()
    {
        PlayerState state = GetState();
        if (state == null) return false;
        BodyRealmData next = GetNextBodyRealm();
        if (next == null)
        {
            ShowMessage("当前已是第一版最高肉身境界。");
            return false;
        }

        if (state.bodyCultivation < next.requiredBodyCultivation)
        {
            ShowMessage("气血积累不足，尚无法突破肉身境界。");
            return false;
        }

        state.bodyRealmLevel = next.level;
        state.bodyRealm = next.name;
        state.bodyCultivation = 0;

        BodyRealmData following = GetNextBodyRealm();
        state.maxBodyCultivation = following != null ? following.requiredBodyCultivation : next.requiredBodyCultivation;

        RealmManager realmManager = GetComponent<RealmManager>();
        PlayerStatCalculator.RecalculateStats(state, realmManager, this, true);
        RefreshUi();
        ShowMessage(string.IsNullOrEmpty(next.breakthroughMessage) ? ("你突破到了" + next.name + "。") : next.breakthroughMessage);
        return true;
    }

    public void NormalizeBodyRealm()
    {
        PlayerState state = GetState();
        if (state == null) return;
        if (bodyRealmByLevel.Count == 0) LoadBodyRealms();
        state.EnsureLists();

        BodyRealmData current = GetCurrentBodyRealm();
        if (current != null) state.bodyRealm = current.name;
        else if (string.IsNullOrEmpty(state.bodyRealm)) state.bodyRealm = "凡体";

        BodyRealmData next = GetNextBodyRealm();
        if (next != null) state.maxBodyCultivation = next.requiredBodyCultivation;
        else if (state.maxBodyCultivation <= 0) state.maxBodyCultivation = 150;

        RealmManager realmManager = GetComponent<RealmManager>();
        PlayerStatCalculator.RecalculateStats(state, realmManager, this, false);
        RefreshUi();
    }

    private PlayerState GetState()
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        if (locationUIManager == null) locationUIManager = GetComponent<LocationUIManager>();
        return gameManager != null ? gameManager.GetPlayerState() : null;
    }

    private void RefreshUi()
    {
        PlayerState state = GetState();
        if (locationUIManager != null) locationUIManager.RefreshPlayerStatus(state);
        CharacterStatusUIManager characterStatus = GetComponent<CharacterStatusUIManager>();
        if (characterStatus != null) characterStatus.RefreshIfOpen();
    }

    private void ShowMessage(string message)
    {
        if (locationUIManager == null) locationUIManager = GetComponent<LocationUIManager>();
        if (locationUIManager != null) locationUIManager.ShowMessage(message);
        else Debug.Log(message);
    }
}

/// <summary>
/// 锻体修炼管理器。
/// </summary>
public class BodyCultivationManager : MonoBehaviour
{
    private GameManager gameManager;
    private LocationUIManager locationUIManager;

    private void Start()
    {
        gameManager = GetComponent<GameManager>();
        locationUIManager = GetComponent<LocationUIManager>();
    }

    public void AddBodyCultivation(int amount)
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        if (locationUIManager == null) locationUIManager = GetComponent<LocationUIManager>();
        PlayerState state = gameManager != null ? gameManager.GetPlayerState() : null;
        if (state == null) return;

        if (!state.HasSkill("skill_body_tempering_basic"))
        {
            if (locationUIManager != null) locationUIManager.ShowMessage("你尚未掌握锻体法门，只能胡乱打熬身体，收效甚微。");
            return;
        }

        if (string.IsNullOrEmpty(state.equippedBodyMethodId)) state.equippedBodyMethodId = "skill_body_tempering_basic";
        state.bodyCultivation += Mathf.Max(0, amount);
        if (locationUIManager != null)
        {
            locationUIManager.RefreshPlayerStatus(state);
            locationUIManager.ShowMessage("你按照《锻体入门》打熬筋骨，气血渐渐凝实。锻体进度 +" + amount + "。");
        }
        CharacterStatusUIManager characterStatus = GetComponent<CharacterStatusUIManager>();
        if (characterStatus != null) characterStatus.RefreshIfOpen();
    }
}

/// <summary>
/// 人物状态界面：运行时自动创建，避免手动重建 DemoScene。
/// 这版把文字放进滚动区域，按钮固定在底部，避免内容和按钮互相遮挡。
/// </summary>
public class CharacterStatusUIManager : MonoBehaviour
{
    private GameManager gameManager;
    private RealmManager realmManager;
    private BodyRealmManager bodyRealmManager;
    private SkillManager skillManager;
    private LocationUIManager locationUIManager;

    private GameObject statusButtonObject;
    private Button statusButton;
    private GameObject panelObject;
    private ScrollRect scrollRect;
    private RectTransform viewportRect;
    private RectTransform contentRect;
    private Text contentText;
    private Button closeButton;
    private Font cachedFont;

    private const float PanelWidth = 760f;
    private const float PanelHeight = 720f;
    private const float TopPadding = 78f;
    private const float BottomButtonArea = 116f;
    private const float SidePadding = 42f;

    private void Start()
    {
        BindReferences();
        EnsureStatusButton();
        EnsurePanel();
        Hide();
    }

    private void Update()
    {
        if (panelObject != null && panelObject.activeSelf && Input.GetKeyDown(KeyCode.Escape)) Hide();
        if (statusButtonObject != null) statusButtonObject.SetActive(!IsUiBlocked());
    }

    private void BindReferences()
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        if (realmManager == null) realmManager = GetComponent<RealmManager>();
        if (bodyRealmManager == null) bodyRealmManager = GetComponent<BodyRealmManager>();
        if (skillManager == null) skillManager = GetComponent<SkillManager>();
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
        if (contentText == null) return;
        PlayerState state = gameManager != null ? gameManager.GetPlayerState() : null;
        if (state == null) return;
        state.EnsureLists();

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("【基本信息】");
        builder.AppendLine("姓名：" + state.playerName);
        builder.AppendLine("生命：" + state.hp + " / " + state.maxHp);
        builder.AppendLine("攻击：" + state.attack);
        builder.AppendLine("防御：" + state.defense);
        builder.AppendLine("武器：" + GetWeaponNameOrNone(state.equippedWeaponId));
        builder.AppendLine("灵石：" + state.spiritStones);
        builder.AppendLine();

        builder.AppendLine("【修炼境界】");
        builder.AppendLine("修炼境界：" + state.realm);
        builder.AppendLine("修炼等级：" + state.realmLevel);
        builder.AppendLine("当前修为：" + state.cultivation);
        RealmData nextRealm = realmManager != null ? realmManager.GetNextRealm() : null;
        builder.AppendLine("下次突破需求：" + (nextRealm != null ? nextRealm.requiredCultivation.ToString() : "已达上限"));
        builder.AppendLine();

        builder.AppendLine("【肉身锻体】");
        builder.AppendLine("锻体境界：" + state.bodyRealm);
        builder.AppendLine("锻体等级：" + state.bodyRealmLevel);
        builder.AppendLine("锻体值：" + state.bodyCultivation);
        BodyRealmData nextBody = bodyRealmManager != null ? bodyRealmManager.GetNextBodyRealm() : null;
        builder.AppendLine("下次突破需求：" + (nextBody != null ? nextBody.requiredBodyCultivation.ToString() : "已达上限"));
        builder.AppendLine();

        builder.AppendLine("【功法信息】");
        builder.AppendLine("主修功法：" + GetSkillNameOrNone(state.equippedCultivationSkillId));
        builder.AppendLine("锻体法门：" + GetSkillNameOrNone(state.equippedBodyMethodId));
        builder.AppendLine("战斗法术：" + GetSkillNameOrNone(state.equippedSpellSkillId));
        builder.AppendLine();

        builder.AppendLine("【已学功法】");
        if (state.learnedSkills == null || state.learnedSkills.Count == 0)
        {
            builder.AppendLine("尚未学会功法。");
        }
        else
        {
            foreach (string skillId in state.learnedSkills)
            {
                builder.AppendLine("- " + GetSkillNameOrNone(skillId));
            }
        }

        contentText.text = builder.ToString();
        UpdateScrollContentSize();
    }

    private void UpdateScrollContentSize()
    {
        if (contentText == null || contentRect == null || viewportRect == null) return;
        Canvas.ForceUpdateCanvases();
        float viewportHeight = Mathf.Max(120f, viewportRect.rect.height);
        float preferredHeight = Mathf.Max(viewportHeight, contentText.preferredHeight + 28f);
        contentRect.sizeDelta = new Vector2(0f, preferredHeight);
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
    }

    private string GetSkillNameOrNone(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return "无";
        return skillManager != null ? skillManager.GetSkillName(skillId) : skillId;
    }

    private string GetWeaponNameOrNone(string weaponId)
    {
        if (string.IsNullOrEmpty(weaponId)) return "无";
        InventoryItemData item = InventoryItemDatabase.GetItem(weaponId);
        return item != null ? item.name : weaponId;
    }

    private void EnsureStatusButton()
    {
        if (statusButtonObject != null) return;
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        statusButtonObject = new GameObject("CharacterStatusButton", typeof(RectTransform), typeof(Image), typeof(Button));
        statusButtonObject.transform.SetParent(canvas.transform, false);
        RectTransform rect = statusButtonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(90f, 38f);
        rect.anchoredPosition = new Vector2(-160f, -18f);

        Image image = statusButtonObject.GetComponent<Image>();
        image.color = new Color(0.20f, 0.26f, 0.32f, 1f);
        statusButton = statusButtonObject.GetComponent<Button>();
        statusButton.targetGraphic = image;
        statusButton.onClick.AddListener(Open);
        Text label = CreateText(statusButtonObject.transform, "状态", 18, TextAnchor.MiddleCenter, Color.white);
        StretchToParent(label.rectTransform);
    }

    private void EnsurePanel()
    {
        if (panelObject != null) return;
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        panelObject = new GameObject("CharacterStatusPanel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        panelRect.anchoredPosition = Vector2.zero;
        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.08f, 0.09f, 0.11f, 0.98f);

        Text titleText = CreateText(panelObject.transform, "人物状态", 30, TextAnchor.MiddleCenter, Color.white);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(0f, 54f);
        titleRect.anchoredPosition = new Vector2(0f, -16f);

        GameObject viewportObject = new GameObject("StatusScrollViewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportObject.transform.SetParent(panelObject.transform, false);
        viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(SidePadding, BottomButtonArea);
        viewportRect.offsetMax = new Vector2(-SidePadding, -TopPadding);
        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.08f);
        Mask mask = viewportObject.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject contentObject = new GameObject("StatusScrollContent", typeof(RectTransform), typeof(Text));
        contentObject.transform.SetParent(viewportObject.transform, false);
        contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 500f);

        contentText = contentObject.GetComponent<Text>();
        contentText.font = GetDefaultFont();
        contentText.fontSize = 18;
        contentText.lineSpacing = 1.05f;
        contentText.alignment = TextAnchor.UpperLeft;
        contentText.color = Color.white;
        contentText.horizontalOverflow = HorizontalWrapMode.Wrap;
        contentText.verticalOverflow = VerticalWrapMode.Overflow;

        scrollRect = panelObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 28f;

        GameObject buttonBar = new GameObject("StatusButtonBar", typeof(RectTransform));
        buttonBar.transform.SetParent(panelObject.transform, false);
        RectTransform buttonBarRect = buttonBar.GetComponent<RectTransform>();
        buttonBarRect.anchorMin = new Vector2(0f, 0f);
        buttonBarRect.anchorMax = new Vector2(1f, 0f);
        buttonBarRect.pivot = new Vector2(0.5f, 0f);
        buttonBarRect.sizeDelta = new Vector2(0f, 96f);
        buttonBarRect.anchoredPosition = new Vector2(0f, 12f);

        Button qiButton = CreateButton(buttonBar.transform, "突破修炼境界", new Vector2(-220f, 40f), new Vector2(190f, 42f));
        qiButton.onClick.AddListener(delegate
        {
            RealmManager manager = GetComponent<RealmManager>();
            if (manager != null) manager.TryBreakthrough();
            Refresh();
        });

        Button bodyButton = CreateButton(buttonBar.transform, "突破锻体境界", new Vector2(0f, 40f), new Vector2(190f, 42f));
        bodyButton.onClick.AddListener(delegate
        {
            BodyRealmManager manager = GetComponent<BodyRealmManager>();
            if (manager != null) manager.TryBodyBreakthrough();
            Refresh();
        });

        closeButton = CreateButton(buttonBar.transform, "关闭", new Vector2(220f, 40f), new Vector2(120f, 42f));
        closeButton.onClick.AddListener(Hide);
    }

    private Button CreateButton(Transform parent, string text, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject buttonObject = new GameObject(text + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.18f, 0.24f, 0.30f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        Text label = CreateText(buttonObject.transform, text, 18, TextAnchor.MiddleCenter, Color.white);
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
}
