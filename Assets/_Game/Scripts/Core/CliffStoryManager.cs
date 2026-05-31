using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 后山悬崖黑屏剧情、崖底地图入口、崖底石台调查。
/// 触发时只播放黑屏剧情；剧情结束后，选择项显示在底部“可执行”区域。
/// </summary>
public class CliffStoryManager : MonoBehaviour
{
    public static bool IsCliffStoryOpen { get; private set; }

    private const string DailyRecordKey = "cliff_event_triggered_today";
    private const string FirstDayCounterId = "cliff_call_first_day";
    private const string FirstStoryReadyFlag = "cliff_call_story_ready";
    private const string TrialStoryReadyFlag = "cliff_trial_story_ready";

    private GameManager gameManager;
    private MapGridManager mapGridManager;
    private LocationUIManager locationUIManager;
    private LocationActionManager locationActionManager;
    private InventoryManager inventoryManager;

    private GameObject panelObject;
    private Text titleText;
    private Text bodyText;
    private Font cachedFont;
    private Coroutine playingCoroutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttach()
    {
        GameManager manager = FindObjectOfType<GameManager>();
        if (manager != null && manager.GetComponent<CliffStoryManager>() == null)
        {
            manager.gameObject.AddComponent<CliffStoryManager>();
        }
    }

    private void Start()
    {
        BindReferences();
        EnsurePanel();
        HidePanel();
    }

    private void Update()
    {
        BindReferences();
        if (IsCliffStoryOpen) return;
        if (RestManager.IsRestingTransition || BattleManager.IsBattleOpen || OpeningStoryManager.IsOpeningActive || ChapterTitleManager.IsChapterTitleActive || ChapterOneLocationMechanicsManager.IsChapterOneEventOpen) return;

        PlayerState state = GetState();
        MapCellData cell = mapGridManager != null ? mapGridManager.GetCurrentCell() : null;
        if (state == null || cell == null || cell.id != "back_mountain") return;

        if (ShouldStartFirstCliffCall(state))
        {
            StartFirstCliffCall();
            return;
        }

        if (ShouldStartCliffTrial(state))
        {
            StartCliffTrial();
        }
    }

    private void BindReferences()
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        if (mapGridManager == null) mapGridManager = GetComponent<MapGridManager>();
        if (locationUIManager == null) locationUIManager = GetComponent<LocationUIManager>();
        if (locationActionManager == null) locationActionManager = GetComponent<LocationActionManager>();
        if (inventoryManager == null) inventoryManager = GetComponent<InventoryManager>();
    }

    private PlayerState GetState()
    {
        return gameManager != null ? gameManager.GetPlayerState() : null;
    }

    private bool ShouldStartFirstCliffCall(PlayerState state)
    {
        if (state.HasFlag(FirstStoryReadyFlag)) return false;
        if (state.HasFlag("heard_cliff_call")) return false;
        if (state.HasFlag("ignored_cliff_call")) return false;
        if (state.HasFlag("cliff_trial_started")) return false;
        if (state.HasFlag("jumped_down_cliff")) return false;
        if (state.HasDoneToday(DailyRecordKey)) return false;
        return true;
    }

    private bool ShouldStartCliffTrial(PlayerState state)
    {
        if (state.HasFlag(TrialStoryReadyFlag)) return false;
        if (!state.HasFlag("heard_cliff_call")) return false;
        if (state.HasFlag("resolved_cliff_choice")) return false;
        if (state.HasFlag("jumped_down_cliff")) return false;
        if (state.HasDoneToday(DailyRecordKey)) return false;
        return state.day > state.GetCounter(FirstDayCounterId);
    }

    public bool TryHandleCliffAction(LocationActionData actionData, MapCellData currentCell)
    {
        BindReferences();
        if (actionData == null || currentCell == null) return false;
        PlayerState state = GetState();
        if (state == null) return false;

        switch (actionData.id)
        {
            case "listen_cliff_call":
                state.AddFlag("heard_cliff_call");
                state.SetCounter(FirstDayCounterId, state.day);
                state.MarkDoneToday(DailyRecordKey);
                state.RemoveFlag(FirstStoryReadyFlag);
                RefreshAll();
                ShowMessage("你屏息凝神，想要听清那道声音。可风声忽然变得尖锐，那股呼唤也随之沉入崖底。你知道，这件事还没有结束。");
                return true;

            case "ignore_cliff_call":
                state.AddFlag("ignored_cliff_call");
                state.AddFlag("resolved_cliff_choice");
                state.RemoveFlag(FirstStoryReadyFlag);
                RefreshAll();
                ShowMessage("你强迫自己移开视线，装作什么都没有听见。崖下的呼唤渐渐远去，只剩冷风掠过山壁。");
                return true;

            case "jump_down_cliff":
                state.AddFlag("jumped_down_cliff");
                state.AddFlag("resolved_cliff_choice");
                state.AddFlag("cliff_trial_started");
                state.RemoveFlag(TrialStoryReadyFlag);
                state.UnlockCell("cliff_bottom");
                state.UnlockCell("cliff_stone_road");
                state.UnlockCell("cliff_stone_platform");
                MoveToCell("cliff_bottom", "cliff_bottom", "你咬紧牙关，纵身跃入云雾。风声在耳边炸开，身体急速下坠。就在你以为自己要摔得粉身碎骨时，一缕金光托住了你。");
                return true;

            case "delay_cliff_trial":
                state.AddFlag("delayed_cliff_trial");
                state.MarkDoneToday(DailyRecordKey);
                state.RemoveFlag(TrialStoryReadyFlag);
                RefreshAll();
                ShowMessage("你后退半步，压下心中的冲动。崖底金光仍在雾中闪烁，像是在等待你的下一次选择。");
                return true;

            case "refuse_cliff_trial":
                state.AddFlag("refused_cliff_trial");
                state.AddFlag("resolved_cliff_choice");
                state.RemoveFlag(TrialStoryReadyFlag);
                RefreshAll();
                ShowMessage("你冷冷望着崖下金光，强行压下心中悸动。无论那下面有什么，都不该用这种方式诱你跳下去。");
                return true;

            case "return_to_back_mountain_from_cliff_bottom":
                MoveToCell("back_mountain", "main", "你抓住藤蔓，费了些力气，终于重新爬回后山。");
                return true;

            case "investigate_cliff_stone_platform":
                if (currentCell.id != "cliff_stone_platform")
                {
                    ShowMessage("这里没有可以调查的石台。");
                    return true;
                }
                if (state.HasFlag("obtained_jinluan_blood_stone"))
                {
                    ShowMessage("石台上的金光已经消散，只剩冰冷的裂痕。");
                    return true;
                }
                StartStonePlatformStory();
                return true;
        }

        return false;
    }

    private void StartFirstCliffCall()
    {
        PlayerState state = GetState();
        if (state != null) state.AddFlag(FirstStoryReadyFlag);
        PlayStoryAndReturn(
            "悬崖下的呼唤",
            new[]
            {
                "后山风声极冷，崖边雾气翻涌。",
                "林昊站在悬崖边，忽然觉得胸口微微发烫，仿佛崖下有什么东西正在呼唤自己。",
                "那声音很轻，却像是直接落在心底。"
            },
            "崖下的呼唤仍在耳边回荡，你必须做出选择。"
        );
    }

    private void StartCliffTrial()
    {
        PlayerState state = GetState();
        if (state != null) state.AddFlag(TrialStoryReadyFlag);
        PlayStoryAndReturn(
            "悬崖下的试炼",
            new[]
            {
                "你再次来到后山悬崖边。",
                "云雾深处，一缕金光忽明忽暗，像是某种古老存在睁开了眼。",
                "那道声音比上次更加清晰，低低落入你的心底。",
                "“想要守护自己最重要的人吗？”",
                "“那就跳下来。”"
            },
            "崖底金光仍在雾中闪烁，等待你的选择。"
        );
    }

    private void StartStonePlatformStory()
    {
        EnsurePanel();
        IsCliffStoryOpen = true;
        if (locationActionManager != null) locationActionManager.ClearCurrentButtons();
        panelObject.SetActive(true);
        panelObject.transform.SetAsLastSibling();
        titleText.text = "崖底石台";
        bodyText.text = "";
        if (playingCoroutine != null) StopCoroutine(playingCoroutine);
        playingCoroutine = StartCoroutine(PlayStonePlatformLines());
    }

    private IEnumerator PlayStonePlatformLines()
    {
        string[] lines =
        {
            "你走近石台，掌心忽然传来一阵刺痛。",
            "石缝中的金光像是活物般游动，最终凝成一枚暗金色血石。",
            "它并不耀眼，却让你的气血微微震动。"
        };
        for (int i = 0; i < lines.Length; i++)
        {
            yield return TypeLine(lines[i]);
            yield return new WaitForSeconds(0.35f);
        }
        PlayerState state = GetState();
        if (state != null && !state.HasFlag("obtained_jinluan_blood_stone"))
        {
            if (inventoryManager != null) inventoryManager.AddItem("jinluan_blood_stone", 1);
            else state.AddItem("jinluan_blood_stone", 1);
            state.AddFlag("obtained_jinluan_blood_stone");
        }
        yield return new WaitForSeconds(0.6f);
        CloseStory("你伸手取走血石。血石入手温热，像是有一股古老气血沉睡其中。获得：金銮血石。");
    }

    private void PlayStoryAndReturn(string title, string[] lines, string messageAfterClose)
    {
        EnsurePanel();
        IsCliffStoryOpen = true;
        if (locationActionManager != null) locationActionManager.ClearCurrentButtons();
        panelObject.SetActive(true);
        panelObject.transform.SetAsLastSibling();
        titleText.text = title;
        bodyText.text = "";
        if (playingCoroutine != null) StopCoroutine(playingCoroutine);
        playingCoroutine = StartCoroutine(PlayLinesThenClose(lines, messageAfterClose));
    }

    private IEnumerator PlayLinesThenClose(string[] lines, string messageAfterClose)
    {
        yield return new WaitForSeconds(0.25f);
        if (lines != null)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                yield return TypeLine(lines[i]);
                yield return new WaitForSeconds(0.35f);
            }
        }
        yield return new WaitForSeconds(0.8f);
        CloseStory(messageAfterClose);
    }

    private IEnumerator TypeLine(string line)
    {
        if (bodyText == null || string.IsNullOrEmpty(line)) yield break;
        if (!string.IsNullOrEmpty(bodyText.text)) bodyText.text += "\n\n";
        for (int i = 0; i < line.Length; i++)
        {
            bodyText.text += line[i];
            yield return new WaitForSeconds(0.045f);
        }
    }

    private void CloseStory(string message)
    {
        if (playingCoroutine != null)
        {
            StopCoroutine(playingCoroutine);
            playingCoroutine = null;
        }
        HidePanel();
        IsCliffStoryOpen = false;
        RefreshAll();
        if (!string.IsNullOrEmpty(message)) ShowMessage(message);
    }

    private void MoveToCell(string cellId, string mapId, string message)
    {
        PlayerState state = GetState();
        MapCellData cell = mapGridManager != null ? mapGridManager.GetCellById(cellId) : null;
        if (state == null || cell == null) return;
        state.currentMapId = mapId;
        state.currentCellId = cell.id;
        state.currentX = cell.x;
        state.currentY = cell.y;
        RefreshAll();
        ShowMessage(message);
    }

    private void RefreshAll()
    {
        PlayerState state = GetState();
        if (mapGridManager != null)
        {
            mapGridManager.SyncPlayerPositionToCurrentCell();
            mapGridManager.RefreshMap();
        }
        MapCellData cell = mapGridManager != null ? mapGridManager.GetCurrentCell() : null;
        if (locationUIManager != null) locationUIManager.RefreshLocation(cell, state);
        if (locationActionManager != null) locationActionManager.RefreshCurrentLocation();
        InventoryUIManager inventoryUI = GetComponent<InventoryUIManager>();
        if (inventoryUI != null) inventoryUI.RefreshIfOpen();
        CharacterStatusUIManager statusUI = GetComponent<CharacterStatusUIManager>();
        if (statusUI != null) statusUI.RefreshIfOpen();
    }

    private void ShowMessage(string message)
    {
        if (locationUIManager != null) locationUIManager.ShowMessage(message);
        else Debug.Log(message);
    }

    private void EnsurePanel()
    {
        if (panelObject != null) return;
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;
        panelObject = new GameObject("CliffStoryPanel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvas.transform, false);
        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = panelObject.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;
        titleText = CreateText(panelObject.transform, "Title", 30, TextAnchor.MiddleCenter, new Vector2(0.12f, 0.72f), new Vector2(0.88f, 0.86f));
        bodyText = CreateText(panelObject.transform, "Body", 24, TextAnchor.MiddleCenter, new Vector2(0.16f, 0.28f), new Vector2(0.84f, 0.72f));
    }

    private Text CreateText(Transform parent, string name, int fontSize, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Text text = obj.GetComponent<Text>();
        text.font = GetDefaultFont();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private Font GetDefaultFont()
    {
        if (cachedFont != null) return cachedFont;
        cachedFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 20);
        if (cachedFont != null) return cachedFont;
        cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return cachedFont;
    }

    private void HidePanel()
    {
        if (panelObject != null) panelObject.SetActive(false);
    }
}
