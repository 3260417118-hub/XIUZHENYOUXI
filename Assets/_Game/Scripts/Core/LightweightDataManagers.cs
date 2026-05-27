using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class ItemData
{
    public string id;
    public string name;
    public string description;
    public string type;
}

[Serializable]
public class ItemDataList
{
    public List<ItemData> items = new List<ItemData>();
}

[Serializable]
public class SkillData
{
    public string id;
    public string name;
    public string description;
    public string type;
}

[Serializable]
public class SkillDataList
{
    public List<SkillData> skills = new List<SkillData>();
}

[Serializable]
public class NightEventRewardData
{
    public string addItem;
    public string setFlag;
    public string learnSkill;
    public int cultivationGain;
    public int spiritStoneGain;
}

[Serializable]
public class NightEventData
{
    public string id;
    public string title;
    public string text;
    public NightEventRewardData reward;
    public string message;
}

[Serializable]
public class NightEventDataList
{
    public List<NightEventData> nightEvents = new List<NightEventData>();
}

/// <summary>
/// 条件判断工具。用于事件选项、对话选项、地点行为、地图解锁。
/// </summary>
public static class ConditionUtility
{
    public static bool IsMet(PlayerState playerState, ConditionData condition)
    {
        if (condition == null) return true;
        if (playerState == null) return false;
        playerState.EnsureLists();

        if (!AllStringsPresent(condition.requireFlags, playerState.flags)) return false;
        if (AnyStringPresent(condition.excludeFlags, playerState.flags)) return false;
        if (!AllStringsPresent(condition.requireItems, playerState.items)) return false;
        if (AnyStringPresent(condition.excludeItems, playerState.items)) return false;
        if (!AllStringsPresent(condition.requireSkills, playerState.learnedSkills)) return false;
        if (AnyStringPresent(condition.excludeSkills, playerState.learnedSkills)) return false;
        if (condition.minCultivation > 0 && playerState.cultivation < condition.minCultivation) return false;
        if (condition.minDay > 0 && playerState.day < condition.minDay) return false;
        if (condition.maxDay > 0 && playerState.day > condition.maxDay) return false;
        return true;
    }

    public static bool IsMet(
        PlayerState playerState,
        string[] requireFlags,
        string[] excludeFlags,
        string[] requireItems,
        string[] excludeItems,
        string[] requireSkills,
        string[] excludeSkills,
        int minCultivation,
        int minDay,
        int maxDay)
    {
        ConditionData condition = new ConditionData
        {
            requireFlags = requireFlags,
            excludeFlags = excludeFlags,
            requireItems = requireItems,
            excludeItems = excludeItems,
            requireSkills = requireSkills,
            excludeSkills = excludeSkills,
            minCultivation = minCultivation,
            minDay = minDay,
            maxDay = maxDay
        };
        return IsMet(playerState, condition);
    }

    private static bool AllStringsPresent(string[] required, List<string> owned)
    {
        if (required == null || required.Length == 0) return true;
        foreach (string value in required)
        {
            if (string.IsNullOrEmpty(value)) continue;
            if (owned == null || !owned.Contains(value)) return false;
        }
        return true;
    }

    private static bool AnyStringPresent(string[] excluded, List<string> owned)
    {
        if (excluded == null || excluded.Length == 0) return false;
        foreach (string value in excluded)
        {
            if (string.IsNullOrEmpty(value)) continue;
            if (owned != null && owned.Contains(value)) return true;
        }
        return false;
    }
}

public class InventoryLiteManager : MonoBehaviour
{
    private readonly Dictionary<string, ItemData> itemById = new Dictionary<string, ItemData>();
    private GameManager gameManager;
    private LocationUIManager locationUIManager;

    private void Start()
    {
        gameManager = GetComponent<GameManager>();
        locationUIManager = GetComponent<LocationUIManager>();
        LoadItems();
    }

    private void LoadItems()
    {
        itemById.Clear();
        TextAsset jsonAsset = Resources.Load<TextAsset>("Data/items");
        if (jsonAsset == null) return;
        ItemDataList dataList = JsonUtility.FromJson<ItemDataList>(jsonAsset.text);
        if (dataList == null || dataList.items == null) return;
        foreach (ItemData item in dataList.items)
        {
            if (item != null && !string.IsNullOrEmpty(item.id)) itemById[item.id] = item;
        }
    }

    public void AddItem(string itemId)
    {
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState == null || string.IsNullOrEmpty(itemId)) return;
        playerState.AddItem(itemId);
        if (locationUIManager != null) locationUIManager.ShowMessage("获得物品：" + GetItemName(itemId));
    }

    public bool HasItem(string itemId)
    {
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        return playerState != null && playerState.HasItem(itemId);
    }

    public void RemoveItem(string itemId)
    {
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState != null) playerState.RemoveItem(itemId);
    }

    public string GetItemName(string itemId)
    {
        if (itemById.Count == 0) LoadItems();
        ItemData item;
        if (!string.IsNullOrEmpty(itemId) && itemById.TryGetValue(itemId, out item)) return item.name;
        return itemId;
    }
}

public class SkillManager : MonoBehaviour
{
    private readonly Dictionary<string, SkillData> skillById = new Dictionary<string, SkillData>();
    private GameManager gameManager;
    private LocationUIManager locationUIManager;

    private void Start()
    {
        gameManager = GetComponent<GameManager>();
        locationUIManager = GetComponent<LocationUIManager>();
        LoadSkills();
    }

    private void LoadSkills()
    {
        skillById.Clear();
        TextAsset jsonAsset = Resources.Load<TextAsset>("Data/skills");
        if (jsonAsset == null) return;
        SkillDataList dataList = JsonUtility.FromJson<SkillDataList>(jsonAsset.text);
        if (dataList == null || dataList.skills == null) return;
        foreach (SkillData skill in dataList.skills)
        {
            if (skill != null && !string.IsNullOrEmpty(skill.id)) skillById[skill.id] = skill;
        }
    }

    public void LearnSkill(string skillId)
    {
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState == null || string.IsNullOrEmpty(skillId)) return;
        playerState.LearnSkill(skillId);
        if (locationUIManager != null) locationUIManager.ShowMessage("学会功法：" + GetSkillName(skillId));
    }

    public bool HasSkill(string skillId)
    {
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        return playerState != null && playerState.HasSkill(skillId);
    }

    public string GetSkillName(string skillId)
    {
        if (skillById.Count == 0) LoadSkills();
        SkillData skill;
        if (!string.IsNullOrEmpty(skillId) && skillById.TryGetValue(skillId, out skill)) return skill.name;
        return skillId;
    }
}

public class DailyLimitManager : MonoBehaviour
{
    private GameManager gameManager;

    private void Start()
    {
        gameManager = GetComponent<GameManager>();
    }

    public bool HasDoneToday(string key)
    {
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        return playerState != null && playerState.HasDoneToday(key);
    }

    public void MarkDoneToday(string key)
    {
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState != null) playerState.MarkDoneToday(key);
    }

    public void ClearDailyRecords()
    {
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState != null)
        {
            playerState.EnsureLists();
            playerState.dailyActionRecords.Clear();
        }
    }
}

public class CounterManager : MonoBehaviour
{
    private GameManager gameManager;

    private void Start()
    {
        gameManager = GetComponent<GameManager>();
    }

    public int GetCounter(string id)
    {
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        return playerState != null ? playerState.GetCounter(id) : 0;
    }

    public void AddCounter(string id, int amount)
    {
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState != null) playerState.AddCounter(id, amount);
    }

    public void SetCounter(string id, int value)
    {
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState != null) playerState.SetCounter(id, value);
    }
}

public class MapUnlockManager : MonoBehaviour
{
    private GameManager gameManager;
    private LocationUIManager locationUIManager;
    private MapGridManager mapGridManager;

    private void Start()
    {
        gameManager = GetComponent<GameManager>();
        locationUIManager = GetComponent<LocationUIManager>();
        mapGridManager = GetComponent<MapGridManager>();
    }

    public bool IsUnlocked(string cellId)
    {
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        return playerState != null && playerState.IsCellUnlocked(cellId);
    }

    public void UnlockCell(string cellId)
    {
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState == null || string.IsNullOrEmpty(cellId)) return;
        playerState.UnlockCell(cellId);
        MapCellData cell = mapGridManager != null ? mapGridManager.GetCellById(cellId) : null;
        if (locationUIManager != null) locationUIManager.ShowMessage("发现新的道路：" + (cell != null ? cell.name : cellId));
        if (mapGridManager != null) mapGridManager.RefreshMap();
    }
}

public class NightEventManager : MonoBehaviour
{
    private readonly Dictionary<string, NightEventData> nightEventById = new Dictionary<string, NightEventData>();
    private GameManager gameManager;
    private InventoryLiteManager inventoryLiteManager;
    private SkillManager skillManager;

    private void Start()
    {
        gameManager = GetComponent<GameManager>();
        inventoryLiteManager = GetComponent<InventoryLiteManager>();
        skillManager = GetComponent<SkillManager>();
        LoadNightEvents();
    }

    private void LoadNightEvents()
    {
        nightEventById.Clear();
        TextAsset jsonAsset = Resources.Load<TextAsset>("Data/night_events");
        if (jsonAsset == null) return;
        NightEventDataList dataList = JsonUtility.FromJson<NightEventDataList>(jsonAsset.text);
        if (dataList == null || dataList.nightEvents == null) return;
        foreach (NightEventData nightEvent in dataList.nightEvents)
        {
            if (nightEvent != null && !string.IsNullOrEmpty(nightEvent.id)) nightEventById[nightEvent.id] = nightEvent;
        }
    }

    public void AddPendingNightEvent(string eventId)
    {
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState != null) playerState.AddPendingNightEvent(eventId);
    }

    public NightEventData GetNightEvent(string eventId)
    {
        if (nightEventById.Count == 0) LoadNightEvents();
        NightEventData data;
        if (!string.IsNullOrEmpty(eventId) && nightEventById.TryGetValue(eventId, out data)) return data;
        return null;
    }

    public string ApplyNightEvent(string eventId)
    {
        NightEventData data = GetNightEvent(eventId);
        if (data == null) return "";
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState == null) return data.message;

        if (data.reward != null)
        {
            if (!string.IsNullOrEmpty(data.reward.addItem))
            {
                playerState.AddItem(data.reward.addItem);
            }

            if (!string.IsNullOrEmpty(data.reward.setFlag))
            {
                playerState.AddFlag(data.reward.setFlag);
            }

            if (!string.IsNullOrEmpty(data.reward.learnSkill))
            {
                playerState.LearnSkill(data.reward.learnSkill);
            }

            if (data.reward.cultivationGain != 0)
            {
                playerState.cultivation += data.reward.cultivationGain;
            }

            if (data.reward.spiritStoneGain != 0)
            {
                playerState.spiritStones += data.reward.spiritStoneGain;
                if (playerState.spiritStones < 0) playerState.spiritStones = 0;
            }
        }

        return string.IsNullOrEmpty(data.message) ? data.text : data.message;
    }
}

public class RestManager : MonoBehaviour
{
    public static bool IsRestingTransition { get; private set; }

    [SerializeField] private float fadeToBlackDuration = 1.0f;
    [SerializeField] private float textFadeInDuration = 0.8f;
    [SerializeField] private float textHoldDuration = 1.2f;
    [SerializeField] private float textFadeOutDuration = 0.6f;
    [SerializeField] private float fadeFromBlackDuration = 1.0f;

    private GameManager gameManager;
    private MapGridManager mapGridManager;
    private LocationUIManager locationUIManager;
    private LocationActionManager locationActionManager;
    private NightEventManager nightEventManager;
    private BlockingEncounterManager blockingEncounterManager;
    private GameObject panelObject;
    private CanvasGroup canvasGroup;
    private Text transitionText;
    private Font cachedFont;

    private void Start()
    {
        gameManager = GetComponent<GameManager>();
        mapGridManager = GetComponent<MapGridManager>();
        locationUIManager = GetComponent<LocationUIManager>();
        locationActionManager = GetComponent<LocationActionManager>();
        nightEventManager = GetComponent<NightEventManager>();
        blockingEncounterManager = GetComponent<BlockingEncounterManager>();
        StartCoroutine(BindEndDayButtonAsHintNextFrame());
    }

    private IEnumerator BindEndDayButtonAsHintNextFrame()
    {
        yield return null;
        yield return null;
        GameObject endDayObject = GameObject.Find("EndDayButton");
        if (endDayObject == null) yield break;
        Button button = endDayObject.GetComponent<Button>();
        if (button == null) yield break;
        Text label = endDayObject.GetComponentInChildren<Text>();
        if (label != null) label.text = "休息过夜";
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(delegate
        {
            if (locationUIManager != null)
            {
                locationUIManager.ShowMessage("你需要回到破败小屋休息，才能结束今日。");
            }
        });
    }

    public void SleepUntilNextDay()
    {
        if (IsRestingTransition) return;
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        if (mapGridManager == null) mapGridManager = GetComponent<MapGridManager>();
        if (locationUIManager == null) locationUIManager = GetComponent<LocationUIManager>();
        if (locationActionManager == null) locationActionManager = GetComponent<LocationActionManager>();
        if (nightEventManager == null) nightEventManager = GetComponent<NightEventManager>();
        if (blockingEncounterManager == null) blockingEncounterManager = GetComponent<BlockingEncounterManager>();

        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        MapCellData currentCell = mapGridManager != null ? mapGridManager.GetCurrentCell() : null;
        if (playerState == null || currentCell == null) return;

        if (currentCell.id != playerState.currentRestLocationId)
        {
            if (locationUIManager != null) locationUIManager.ShowMessage("你需要回到破败小屋休息，才能结束今日。");
            return;
        }

        if (blockingEncounterManager != null && blockingEncounterManager.HasActiveBlockingEncounter())
        {
            if (locationUIManager != null) locationUIManager.ShowMessage("眼前的麻烦尚未解决，你无法安心休息。");
            return;
        }

        StartCoroutine(SleepRoutine(playerState));
    }

    private IEnumerator SleepRoutine(PlayerState playerState)
    {
        IsRestingTransition = true;
        EnsurePanel();
        if (locationUIManager != null) locationUIManager.ShowMessage("夜色渐深，你在破败小屋中沉沉睡去。");
        if (panelObject != null)
        {
            panelObject.SetActive(true);
            panelObject.transform.SetAsLastSibling();
        }

        yield return FadePanel(0f, 1f, fadeToBlackDuration);
        yield return ShowCenterText("夜色渐深，寒风穿过破败小屋的缝隙。");

        List<string> nightEvents = new List<string>(playerState.pendingNightEvents);
        foreach (string eventId in nightEvents)
        {
            NightEventData data = nightEventManager != null ? nightEventManager.GetNightEvent(eventId) : null;
            if (data != null)
            {
                yield return ShowCenterText(data.title + "\n\n" + data.text);
                string rewardMessage = nightEventManager.ApplyNightEvent(eventId);
                if (!string.IsNullOrEmpty(rewardMessage))
                {
                    yield return ShowCenterText(rewardMessage);
                }
            }
        }

        playerState.pendingNightEvents.Clear();
        playerState.dailyActionRecords.Clear();
        playerState.day += 1;
        playerState.actionPoints = Mathf.Max(1, playerState.maxActionPoints);

        if (locationUIManager != null)
        {
            locationUIManager.RefreshPlayerStatus(playerState);
        }

        yield return ShowCenterText("第 " + playerState.day + " 天");
        yield return FadePanel(1f, 0f, fadeFromBlackDuration);

        if (panelObject != null) panelObject.SetActive(false);
        IsRestingTransition = false;

        MapCellData currentCell = mapGridManager != null ? mapGridManager.GetCurrentCell() : null;
        if (locationUIManager != null)
        {
            locationUIManager.RefreshLocation(currentCell, playerState);
            locationUIManager.ShowMessage("新的一天开始了。");
        }
        if (locationActionManager != null) locationActionManager.RefreshCurrentLocation();
        if (blockingEncounterManager != null) blockingEncounterManager.CheckTodayEncounter();
    }

    private IEnumerator ShowCenterText(string content)
    {
        if (transitionText == null) yield break;
        transitionText.text = content;
        yield return FadeText(0f, 1f, textFadeInDuration);
        yield return new WaitForSeconds(textHoldDuration);
        yield return FadeText(1f, 0f, textFadeOutDuration);
    }

    private IEnumerator FadePanel(float from, float to, float duration)
    {
        float timer = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (timer < safeDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / safeDuration);
            if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }
        if (canvasGroup != null) canvasGroup.alpha = to;
    }

    private IEnumerator FadeText(float from, float to, float duration)
    {
        float timer = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (timer < safeDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / safeDuration);
            if (transitionText != null) transitionText.color = new Color(1f, 1f, 1f, Mathf.Lerp(from, to, t));
            yield return null;
        }
        if (transitionText != null) transitionText.color = new Color(1f, 1f, 1f, to);
    }

    private void EnsurePanel()
    {
        if (panelObject != null) return;
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;
        panelObject = new GameObject("RestTransitionPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panelObject.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        Image image = panelObject.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;
        canvasGroup = panelObject.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        GameObject textObject = new GameObject("RestTransitionText", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(panelObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.15f, 0.35f);
        textRect.anchorMax = new Vector2(0.85f, 0.65f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        transitionText = textObject.GetComponent<Text>();
        transitionText.font = GetDefaultFont();
        transitionText.fontSize = 28;
        transitionText.alignment = TextAnchor.MiddleCenter;
        transitionText.color = new Color(1f, 1f, 1f, 0f);
        transitionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        transitionText.verticalOverflow = VerticalWrapMode.Overflow;
        panelObject.SetActive(false);
    }

    private Font GetDefaultFont()
    {
        if (cachedFont != null) return cachedFont;
        cachedFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 24);
        if (cachedFont != null) return cachedFont;
        cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return cachedFont;
    }
}
