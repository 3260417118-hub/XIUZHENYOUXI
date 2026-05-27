using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 第一章后期剧情修正：
/// 1. 第 13 天村口争斗只在玩家进入村口时触发。
/// 2. 第 18 天白发老者只在第 18 天药田显示，婉拒/相助后消失。
/// 3. 第 21 天赵霸天根据是否救出妹妹使用不同文本。
/// 4. 赵霸天战斗结束后播放黑屏结局文字和章节结束标题。
/// </summary>
public class ChapterOneLateStoryFixManager : MonoBehaviour
{
    public static bool IsEndingPlaying { get; private set; }

    private GameManager gameManager;
    private MapGridManager mapGridManager;
    private LocationUIManager locationUIManager;
    private LocationActionManager locationActionManager;
    private BlockingEncounterManager blockingEncounterManager;

    private GameObject endingPanel;
    private CanvasGroup endingCanvasGroup;
    private Text endingText;
    private Font cachedFont;
    private bool playedEndingThisRun;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttach()
    {
        GameObject managerObject = GameObject.Find("GameManager");
        if (managerObject == null) return;
        if (managerObject.GetComponent<ChapterOneLateStoryFixManager>() == null)
        {
            managerObject.AddComponent<ChapterOneLateStoryFixManager>();
        }
    }

    private void Start()
    {
        BindReferences();
    }

    private void Update()
    {
        BindReferences();
        PlayerState state = GetState();
        if (state == null) return;
        if (OpeningStoryManager.IsOpeningActive || ChapterTitleManager.IsChapterTitleActive || BattleManager.IsBattleOpen || IsEndingPlaying) return;

        CheckLateLocationTriggeredEncounters(state);
        CheckChapterOneEnding(state);
    }

    private void BindReferences()
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        if (mapGridManager == null) mapGridManager = GetComponent<MapGridManager>();
        if (locationUIManager == null) locationUIManager = GetComponent<LocationUIManager>();
        if (locationActionManager == null) locationActionManager = GetComponent<LocationActionManager>();
        if (blockingEncounterManager == null) blockingEncounterManager = GetComponent<BlockingEncounterManager>();
    }

    private PlayerState GetState()
    {
        return gameManager != null ? gameManager.GetPlayerState() : null;
    }

    private void CheckLateLocationTriggeredEncounters(PlayerState state)
    {
        if (state == null || mapGridManager == null) return;
        if (!string.IsNullOrEmpty(state.activeBlockingEncounterId)) return;

        MapCellData currentCell = mapGridManager.GetCurrentCell();
        if (currentCell == null) return;

        // 第 13 天：必须玩家真正来到村口，才会触发村口争斗。
        if (state.day == 13 && currentCell.id == "village_gate" && !state.HasTriggeredDayEvent("encounter_day13_faction_fight"))
        {
            state.activeBlockingEncounterId = "encounter_day13_faction_fight";
            RefreshEncounterUI("村口两伙人马已经成一团，你暂时无法离开这片混乱。");
            return;
        }

        // 第 21 天：赵霸天可以在当前地点堵住玩家，但文本根据是否救出妹妹区分。
        if (state.day == 21 && !state.HasTriggeredDayEvent("encounter_day21_zhao_batian") && !state.HasTriggeredDayEvent("encounter_day21_zhao_batian_no_sister"))
        {
            bool hasSister = state.HasFlag("rescued_sister") || state.HasFlag("sister_at_ruined_hut");
            state.activeBlockingEncounterId = hasSister ? "encounter_day21_zhao_batian" : "encounter_day21_zhao_batian_no_sister";
            RefreshEncounterUI("赵霸天带人堵住了去路。");
        }
    }

    private void RefreshEncounterUI(string message)
    {
        if (blockingEncounterManager == null) blockingEncounterManager = GetComponent<BlockingEncounterManager>();
        if (blockingEncounterManager != null) blockingEncounterManager.RestoreActiveEncounterUI();
        if (locationUIManager != null) locationUIManager.ShowMessage(message);
    }

    public bool ShouldShowWhiteHairedElder(MapCellData currentCell)
    {
        PlayerState state = GetState();
        if (state == null || currentCell == null) return false;
        if (currentCell.id != "herb_field") return false;
        if (state.day != 18) return false;
        if (state.HasFlag("helped_white_haired_elder")) return false;
        if (state.HasFlag("refused_white_haired_elder")) return false;
        return true;
    }

    private void CheckChapterOneEnding(PlayerState state)
    {
        if (playedEndingThisRun || state == null) return;
        if (state.HasFlag("chapter_one_ending_played")) return;

        if (state.HasFlag("chapter_one_good_end"))
        {
            playedEndingThisRun = true;
            state.AddFlag("chapter_one_ending_played");
            StartCoroutine(PlayEndingRoutine(GetGoodEndingText()));
        }
        else if (state.HasFlag("chapter_one_bad_end"))
        {
            playedEndingThisRun = true;
            state.AddFlag("chapter_one_ending_played");
            StartCoroutine(PlayEndingRoutine(GetBadEndingText(state)));
        }
    }

    private string GetGoodEndingText()
    {
        return "赵霸天败退后，青石村短暂恢复平静。\n\n林昊望向远处的青云宗山门，心中明白，真正的敌人并不在这小小村落。\n\n父亲的残卷、妹妹的遭遇、林家的血夜，都指向更深的修真世界。";
    }

    private string GetBadEndingText(PlayerState state)
    {
        bool hasSister = state != null && (state.HasFlag("rescued_sister") || state.HasFlag("sister_at_ruined_hut"));
        if (hasSister)
        {
            return "林昊护住了妹妹，却也付出了惨痛代价。\n\n青石村的夜色压得人喘不过气来。\n\n他第一次如此清楚地明白，弱小本身就是罪。";
        }

        return "赵霸天的笑声渐渐远去，青石村重新归于压抑的沉默。\n\n林昊倒在尘土中，心中却仍回响着那句未完成的执念。\n\n他还没有找到妹妹，也还没有弄清林家血夜背后的真相。";
    }

    private IEnumerator PlayEndingRoutine(string endingBody)
    {
        IsEndingPlaying = true;
        if (locationActionManager != null) locationActionManager.ClearCurrentButtons();
        EnsureEndingPanel();
        if (endingPanel == null)
        {
            IsEndingPlaying = false;
            yield break;
        }

        endingPanel.SetActive(true);
        endingPanel.transform.SetAsLastSibling();

        if (endingText != null)
        {
            endingText.fontSize = 28;
            endingText.text = endingBody;
            endingText.color = new Color(1f, 1f, 1f, 0f);
        }

        yield return FadePanel(0f, 1f, 0.8f);
        yield return FadeText(0f, 1f, 1.2f);
        yield return new WaitForSeconds(2.6f);
        yield return FadeText(1f, 0f, 0.7f);

        if (endingText != null)
        {
            endingText.fontSize = 42;
            endingText.text = "第一章：青石村外\n结束……";
            endingText.color = new Color(1f, 1f, 1f, 0f);
        }

        yield return FadeText(0f, 1f, 1.2f);
        yield return new WaitForSeconds(1.6f);
        yield return FadeText(1f, 0f, 0.8f);
        yield return FadePanel(1f, 0f, 0.8f);

        endingPanel.SetActive(false);
        IsEndingPlaying = false;
        if (locationActionManager != null) locationActionManager.RefreshCurrentLocation();
        if (locationUIManager != null) locationUIManager.ShowMessage("第一章内容暂到此处。");
    }

    private void EnsureEndingPanel()
    {
        if (endingPanel != null) return;
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        endingPanel = new GameObject("ChapterOneEndingPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        endingPanel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = endingPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image image = endingPanel.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;

        endingCanvasGroup = endingPanel.GetComponent<CanvasGroup>();
        endingCanvasGroup.alpha = 0f;
        endingCanvasGroup.blocksRaycasts = true;
        endingCanvasGroup.interactable = true;

        GameObject textObject = new GameObject("ChapterOneEndingText", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(endingPanel.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.14f, 0.26f);
        textRect.anchorMax = new Vector2(0.86f, 0.74f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        endingText = textObject.GetComponent<Text>();
        endingText.font = GetDefaultFont();
        endingText.alignment = TextAnchor.MiddleCenter;
        endingText.horizontalOverflow = HorizontalWrapMode.Wrap;
        endingText.verticalOverflow = VerticalWrapMode.Overflow;
        endingText.color = new Color(1f, 1f, 1f, 0f);

        endingPanel.SetActive(false);
    }

    private IEnumerator FadePanel(float from, float to, float duration)
    {
        float timer = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (timer < safeDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / safeDuration);
            if (endingCanvasGroup != null) endingCanvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }
        if (endingCanvasGroup != null) endingCanvasGroup.alpha = to;
    }

    private IEnumerator FadeText(float from, float to, float duration)
    {
        float timer = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (timer < safeDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / safeDuration);
            if (endingText != null) endingText.color = new Color(1f, 1f, 1f, Mathf.Lerp(from, to, t));
            yield return null;
        }
        if (endingText != null) endingText.color = new Color(1f, 1f, 1f, to);
    }

    private Font GetDefaultFont()
    {
        if (cachedFont != null) return cachedFont;
        cachedFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 28);
        if (cachedFont != null) return cachedFont;
        cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return cachedFont;
    }
}
