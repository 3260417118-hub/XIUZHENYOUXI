using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class RealmData
{
    public string id;
    public string name;
    public int level;
    public int requiredCultivation;
    public int maxHpBonus;
    public int attackBonus;
    public int defenseBonus;
    public string breakthroughMessage;
}

[Serializable]
public class RealmDataList
{
    public List<RealmData> realms = new List<RealmData>();
}

/// <summary>
/// 修炼管理器：只负责增加修为，不自动突破。
/// </summary>
public class CultivationManager : MonoBehaviour
{
    private GameManager gameManager;
    private LocationUIManager locationUIManager;

    private void Start()
    {
        gameManager = GetComponent<GameManager>();
        locationUIManager = GetComponent<LocationUIManager>();
    }

    public void AddCultivation(int amount)
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        if (locationUIManager == null) locationUIManager = GetComponent<LocationUIManager>();
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState == null) return;
        playerState.cultivation += Mathf.Max(0, amount);
        if (locationUIManager != null)
        {
            locationUIManager.RefreshPlayerStatus(playerState);
            locationUIManager.ShowMessage("你闭关修炼片刻，体内灵气流转，修为提升了 " + amount + " 点。");
        }
    }
}

/// <summary>
/// 境界管理器：读取 realms.json，负责手动突破和属性成长。
/// </summary>
public class RealmManager : MonoBehaviour
{
    [SerializeField] private string realmDataResourcePath = "Data/realms";

    private readonly Dictionary<int, RealmData> realmByLevel = new Dictionary<int, RealmData>();
    private GameManager gameManager;
    private LocationUIManager locationUIManager;

    private void Start()
    {
        gameManager = GetComponent<GameManager>();
        locationUIManager = GetComponent<LocationUIManager>();
        LoadRealms();
        NormalizePlayerRealm();
    }

    public void LoadRealms()
    {
        realmByLevel.Clear();
        TextAsset jsonAsset = Resources.Load<TextAsset>(realmDataResourcePath);
        if (jsonAsset == null)
        {
            Debug.LogError("找不到境界数据：Resources/" + realmDataResourcePath + ".json");
            return;
        }

        RealmDataList dataList = JsonUtility.FromJson<RealmDataList>(jsonAsset.text);
        if (dataList == null || dataList.realms == null)
        {
            Debug.LogError("境界数据格式不正确：" + realmDataResourcePath);
            return;
        }

        foreach (RealmData realm in dataList.realms)
        {
            if (realm == null) continue;
            realmByLevel[realm.level] = realm;
        }
    }

    public RealmData GetCurrentRealm()
    {
        PlayerState state = GetState();
        if (state == null) return null;
        if (realmByLevel.Count == 0) LoadRealms();
        RealmData result;
        return realmByLevel.TryGetValue(state.realmLevel, out result) ? result : null;
    }

    public RealmData GetNextRealm()
    {
        PlayerState state = GetState();
        if (state == null) return null;
        if (realmByLevel.Count == 0) LoadRealms();
        RealmData result;
        return realmByLevel.TryGetValue(state.realmLevel + 1, out result) ? result : null;
    }

    public bool CanBreakthrough()
    {
        PlayerState state = GetState();
        RealmData nextRealm = GetNextRealm();
        return state != null && nextRealm != null && state.cultivation >= nextRealm.requiredCultivation;
    }

    public bool TryBreakthrough()
    {
        PlayerState state = GetState();
        if (state == null) return false;
        RealmData nextRealm = GetNextRealm();
        if (nextRealm == null)
        {
            ShowMessage("当前已是第一版最高境界。");
            return false;
        }

        if (state.cultivation < nextRealm.requiredCultivation)
        {
            ShowMessage("修为不足，尚无法突破。");
            return false;
        }

        state.realmLevel = nextRealm.level;
        state.realm = nextRealm.name;

        // 修为是累计总修为。突破只检查门槛，不扣除、不清零。
        state.maxHp += nextRealm.maxHpBonus;
        state.attack += nextRealm.attackBonus;
        state.defense += nextRealm.defenseBonus;
        state.hp = state.maxHp;

        RealmData followingRealm = GetNextRealm();
        state.maxCultivation = followingRealm != null ? followingRealm.requiredCultivation : nextRealm.requiredCultivation;

        if (locationUIManager != null) locationUIManager.RefreshPlayerStatus(state);
        string message = string.IsNullOrEmpty(nextRealm.breakthroughMessage) ? ("你突破到了" + nextRealm.name + "。") : nextRealm.breakthroughMessage;
        ShowMessage(message);
        return true;
    }

    public void NormalizePlayerRealm()
    {
        PlayerState state = GetState();
        if (state == null) return;
        if (realmByLevel.Count == 0) LoadRealms();
        state.EnsureLists();

        RealmData current = GetCurrentRealm();
        if (current != null) state.realm = current.name;
        else if (string.IsNullOrEmpty(state.realm)) state.realm = "凡人";

        RealmData next = GetNextRealm();
        if (next != null) state.maxCultivation = next.requiredCultivation;
        else if (state.maxCultivation <= 0) state.maxCultivation = 150;

        if (locationUIManager != null) locationUIManager.RefreshPlayerStatus(state);
    }

    private PlayerState GetState()
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        if (locationUIManager == null) locationUIManager = GetComponent<LocationUIManager>();
        return gameManager != null ? gameManager.GetPlayerState() : null;
    }

    private void ShowMessage(string message)
    {
        if (locationUIManager == null) locationUIManager = GetComponent<LocationUIManager>();
        if (locationUIManager != null) locationUIManager.ShowMessage(message);
        else Debug.Log(message);
    }
}

/// <summary>
/// 自动创建“尝试突破”按钮，避免手动改 DemoScene。
/// </summary>
public class BreakthroughButtonManager : MonoBehaviour
{
    private Button breakthroughButton;
    private RealmManager realmManager;
    private Font cachedFont;

    private IEnumerator Start()
    {
        yield return null;
        yield return null;
        realmManager = GetComponent<RealmManager>();
        EnsureButton();
        RefreshButtonStateAndLayout();
    }

    private void Update()
    {
        if (breakthroughButton == null) return;
        RefreshButtonStateAndLayout();
    }

    private void EnsureButton()
    {
        if (breakthroughButton != null) return;
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        GameObject buttonObject = new GameObject("BreakthroughButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(canvas.transform, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(120f, 38f);
        rect.anchoredPosition = new Vector2(-24f, -126f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.20f, 0.26f, 0.32f, 1f);
        breakthroughButton = buttonObject.GetComponent<Button>();
        breakthroughButton.targetGraphic = image;
        breakthroughButton.onClick.AddListener(OnBreakthroughClicked);

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        Text label = textObject.GetComponent<Text>();
        label.text = "尝试突破";
        label.font = GetDefaultFont();
        label.fontSize = 18;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
    }

    private void RefreshButtonStateAndLayout()
    {
        if (breakthroughButton == null) return;
        bool blocked = IsUiBlockedForBreakthrough();
        breakthroughButton.gameObject.SetActive(!blocked);
        if (blocked) return;
        LayoutBelowRestButton();
    }

    private bool IsUiBlockedForBreakthrough()
    {
        if (RestManager.IsRestingTransition) return true;
        if (BattleManager.IsBattleOpen) return true;
        if (OpeningStoryManager.IsOpeningActive) return true;
        if (ChapterTitleManager.IsChapterTitleActive) return true;
        if (ChapterOneLocationMechanicsManager.IsChapterOneEventOpen) return true;
        if (ChapterOneLateStoryFixManager.IsEndingPlaying) return true;
        return false;
    }

    private void LayoutBelowRestButton()
    {
        RectTransform rect = breakthroughButton.GetComponent<RectTransform>();
        if (rect == null) return;

        GameObject endDayObject = GameObject.Find("EndDayButton");
        RectTransform endRect = endDayObject != null ? endDayObject.GetComponent<RectTransform>() : null;
        if (endRect == null)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(120f, 38f);
            rect.anchoredPosition = new Vector2(-24f, -126f);
            return;
        }

        rect.anchorMin = endRect.anchorMin;
        rect.anchorMax = endRect.anchorMax;
        rect.pivot = endRect.pivot;
        rect.sizeDelta = new Vector2(Mathf.Max(120f, endRect.sizeDelta.x), 38f);

        float gap = 10f;
        float endHeight = endRect.rect.height > 0f ? endRect.rect.height : endRect.sizeDelta.y;
        if (endRect.pivot.y >= 0.5f)
        {
            rect.anchoredPosition = new Vector2(endRect.anchoredPosition.x, endRect.anchoredPosition.y - endHeight - gap);
        }
        else
        {
            rect.anchoredPosition = new Vector2(endRect.anchoredPosition.x, endRect.anchoredPosition.y - rect.sizeDelta.y - gap);
        }
    }

    private void OnBreakthroughClicked()
    {
        if (IsUiBlockedForBreakthrough())
        {
            LocationUIManager ui = GetComponent<LocationUIManager>();
            if (ui != null) ui.ShowMessage("请先处理当前事件。");
            return;
        }
        if (realmManager == null) realmManager = GetComponent<RealmManager>();
        if (realmManager != null) realmManager.TryBreakthrough();
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
