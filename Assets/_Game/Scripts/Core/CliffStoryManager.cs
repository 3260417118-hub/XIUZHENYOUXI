using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 后山悬崖黑屏剧情、崖底地图入口、崖底石台调查。
/// 运行时自动挂载，不重建 DemoScene。
/// </summary>
public class CliffStoryManager : MonoBehaviour
{
    public static bool IsCliffStoryOpen { get; private set; }

    private const string DailyRecordKey = "cliff_event_triggered_today";
    private const string FirstDayCounterId = "cliff_call_first_day";

    private GameManager gameManager;
    private MapGridManager mapGridManager;
    private LocationUIManager locationUIManager;
    private LocationActionManager locationActionManager;
    private InventoryManager inventoryManager;

    private GameObject panelObject;
    private Text titleText;
    private Text bodyText;
    private RectTransform optionContainer;
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
        if (state.HasFlag("heard_cliff_call")) return false;
        if (state.HasFlag("ignored_cliff_call")) return false;
        if (state.HasFlag("cliff_trial_started")) return false;
        if (state.HasFlag("jumped_down_cliff")) return false;
        if (state.HasDoneToday(DailyRecordKey)) return false;
        return true;
    }

    private bool ShouldStartCliffTrial(PlayerState state)
    {
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

        if (actionData.id == "return_to_back_mountain_from_cliff_bottom")
        {
            MoveToCell("back_mountain", "main", "你抓住藤蔓，费了些力气，终于重新爬回后山。");
            return true;
        }

        if (actionData.id == "investigate_cliff_stone_platform")
        {
            PlayerState state = GetState();
            if (state == null) return true;
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
        PlayStory(
            "悬崖下的呼唤",
            new[]
            {
                "后山风声极冷，崖边雾气翻涌。",
                "林昊站在悬崖边，忽然觉得胸口微微发烫，仿佛崖下有什么东西正在呼唤自己。",
                "那声音很轻，却像是直接落在心底。"
            },
            new List<CliffStoryOption>
            {
                new CliffStoryOption("凝神倾听", delegate
                {
                    PlayerState state = GetState();
                    if (state == null) return;
                    state.AddFlag("heard_cliff_call");
                    state.SetCounter(FirstDayCounterId, state.day);
                    state.MarkDoneToday(DailyRecordKey);
                    CloseStory("你屏息凝神，想要听清那道声音。可风声忽然变得尖锐，那股呼唤也随之沉入崖底。你知道，这件事还没有结束。", null);
                }),
                new CliffStoryOption("假装没听到", delegate
                {
                    PlayerState state = GetState();
                    if (state == null) return;
                    state.AddFlag("ignored_cliff_call");
                    state.AddFlag("resolved_cliff_choice");
                    CloseStory("你强迫自己移开视线，装作什么都没有听见。崖下的呼唤渐渐远去，只剩冷风掠过山壁。", null);
                })
            });
    }

    private void StartCliffTrial()
    {
        PlayStory(
            "悬崖下的试炼",
            new[]
            {
                "你再次来到后山悬崖边。",
                "云雾深处，一缕金光忽明忽暗，像是某种古老存在睁开了眼。",
                "那道声音比上次更加清晰，低低落入你的心底。",
                "“想要守护自己最重要的人吗？”",
                "“那就跳下来。”"
            },
            new List<CliffStoryOption>
            {
                new CliffStoryOption("跳下去", delegate
                {
                    PlayerState state = GetState();
                    if (state == null) return;
                    state.AddFlag("jumped_down_cliff");
                    state.AddFlag("resolved_cliff_choice");
                    state.AddFlag("cliff_trial_started");
                    state.UnlockCell("cliff_bottom");
                    state.UnlockCell("cliff_stone_road");
                    state.UnlockCell("cliff_stone_platform");
                    MoveToCellSilently("cliff_bottom", "main");
                    CloseStory("你咬紧牙关，纵身跃入云雾。风声在耳边炸开，身体急速下坠。就在你以为自己要摔得粉身碎骨时，一缕金光托住了你。", null);
                }),
                new CliffStoryOption("暂时离开", delegate
                {
                    PlayerState state = GetState();
                    if (state == null) return;
                    state.AddFlag("delayed_cliff_trial");
                    state.MarkDoneToday(DailyRecordKey);
                    CloseStory("你后退半步，压下心中的冲动。崖底金光仍在雾中闪烁，像是在等待你的下一次选择。", null);
                }),
                new CliffStoryOption("不信这鬼话", delegate
                {
                    PlayerState state = GetState();
                    if (state == null) return;
                    state.AddFlag("refused_cliff_trial");
                    state.AddFlag("resolved_cliff_choice");
                    CloseStory("你冷冷望着崖下金光，强行压下心中悸动。无论那下面有什么，都不该用这种方式诱你跳下去。", null);
                })
            });
    }

    private void StartStonePlatformStory()
    {
        PlayStory(
            "崖底石台",
            new[]
            {
                "你走近石台，掌心忽然传来一阵刺痛。",
                "石缝中的金光像是活物般游动，最终凝成一枚暗金色血石。",
                "它并不耀眼，却让你的气血微微震动。"
            },
            new List<CliffStoryOption>
            {
                new CliffStoryOption("取走血石", delegate
                {
                    PlayerState state = GetState();
                    if (state == null) return;
                    if (!state.HasFlag("obtained_jinluan_blood_stone"))
                    {
                        if (inventoryManager != null) inventoryManager.AddItem("jinluan_blood_stone", 1);
                        else state.AddItem("jinluan_blood_stone", 1);
                        state.AddFlag("obtained_jinluan_blood_stone");
                    }
                    CloseStory("你伸手取走血石。血石入手温热，像是有一股古老气血沉睡其中。获得：金銮血石。", null);
                }),
                new CliffStoryOption("暂不触碰", delegate
                {
                    CloseStory("你压下心中的好奇，没有贸然触碰石台。金光仍在石缝中缓缓流动。", null);
                })
            });
    }

    private void PlayStory(string title, string[] lines, List<CliffStoryOption> options)
    {
        EnsurePanel();
        IsCliffStoryOpen = true;
        if (locationActionManager != null) locationActionManager.ClearCurrentButtons();
        panelObject.SetActive(true);
        panelObject.transform.SetAsLastSibling();
        titleText.text = title;
        bodyText.text = "";
        ClearOptions();
        if (playingCoroutine != null) StopCoroutine(playingCoroutine);
        playingCoroutine = StartCoroutine(PlayLines(lines, options));
    }

    private IEnumerator PlayLines(string[] lines, List<CliffStoryOption> options)
    {
        yield return new WaitForSeconds(0.25f);
        if (lines != null)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrEmpty(bodyText.text)) bodyText.text += "\n\n";
                bodyText.text += lines[i];
                yield return new WaitForSeconds(0.75f);
            }
        }
        CreateOptionButtons(options);
    }

    private void CloseStory(string message, System.Action afterClose)
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
        if (afterClose != null) afterClose();
    }

    private void MoveToCell(string cellId, string mapId, string message)
    {
        MoveToCellSilently(cellId, mapId);
        RefreshAll();
        ShowMessage(message);
    }

    private void MoveToCellSilently(string cellId, string mapId)
    {
        PlayerState state = GetState();
        MapCellData cell = mapGridManager != null ? mapGridManager.GetCellById(cellId) : null;
        if (state == null || cell == null) return;
        state.currentMapId = mapId;
        state.currentCellId = cell.id;
        state.currentX = cell.x;
        state.currentY = cell.y;
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
        bodyText = CreateText(panelObject.transform, "Body", 24, TextAnchor.UpperCenter, new Vector2(0.16f, 0.34f), new Vector2(0.84f, 0.70f));

        GameObject optionObject = new GameObject("Options", typeof(RectTransform));
        optionObject.transform.SetParent(panelObject.transform, false);
        optionContainer = optionObject.GetComponent<RectTransform>();
        optionContainer.anchorMin = new Vector2(0.5f, 0.18f);
        optionContainer.anchorMax = new Vector2(0.5f, 0.18f);
        optionContainer.pivot = new Vector2(0.5f, 0.5f);
        optionContainer.sizeDelta = new Vector2(720f, 80f);
        optionContainer.anchoredPosition = Vector2.zero;
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

    private void CreateOptionButtons(List<CliffStoryOption> options)
    {
        ClearOptions();
        if (options == null) return;
        float spacing = 22f;
        float width = 180f;
        float totalWidth = options.Count * width + Mathf.Max(0, options.Count - 1) * spacing;
        float startX = -totalWidth * 0.5f + width * 0.5f;
        for (int i = 0; i < options.Count; i++)
        {
            CliffStoryOption option = options[i];
            GameObject obj = new GameObject(option.Text + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(optionContainer, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, 42f);
            rect.anchoredPosition = new Vector2(startX + i * (width + spacing), 0f);
            Image image = obj.GetComponent<Image>();
            image.color = new Color(0.18f, 0.18f, 0.20f, 1f);
            Button button = obj.GetComponent<Button>();
            button.targetGraphic = image;
            Text label = CreateText(obj.transform, "Text", 18, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            label.text = option.Text;
            CliffStoryOption captured = option;
            button.onClick.AddListener(delegate { if (captured.OnChoose != null) captured.OnChoose(); });
        }
    }

    private void ClearOptions()
    {
        if (optionContainer == null) return;
        for (int i = optionContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(optionContainer.GetChild(i).gameObject);
        }
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

    private class CliffStoryOption
    {
        public string Text;
        public System.Action OnChoose;

        public CliffStoryOption(string text, System.Action onChoose)
        {
            Text = text;
            OnChoose = onChoose;
        }
    }
}
