using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 第一章第一批地点机制。
/// 只做轻量剧情/奖励/地图切换，不做完整背包 UI、装备 UI、商店系统。
/// </summary>
public class ChapterOneLocationMechanicsManager : MonoBehaviour
{
    public static bool IsChapterOneEventOpen { get; private set; }

    private GameManager gameManager;
    private MapGridManager mapGridManager;
    private LocationUIManager locationUIManager;
    private LocationActionManager locationActionManager;
    private ActionPointManager actionPointManager;
    private BattleManager battleManager;
    private NightEventManager nightEventManager;

    private GameObject storyPanelObject;
    private CanvasGroup storyCanvasGroup;
    private Text storyText;
    private Font cachedFont;

    private const string CaveStoryText = "【山洞中的哭声】\n\n你拨开藤蔓，洞中传来微弱的哭声。一个小女孩蜷缩在石壁旁，衣衫凌乱，手腕上还留着绳痕。她抬头看见你，眼中先是恐惧，随后像是认出了什么，颤声喊道：“哥哥……”";

    private void Start()
    {
        BindReferences();
    }

    private void BindReferences()
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        if (mapGridManager == null) mapGridManager = GetComponent<MapGridManager>();
        if (locationUIManager == null) locationUIManager = GetComponent<LocationUIManager>();
        if (locationActionManager == null) locationActionManager = GetComponent<LocationActionManager>();
        if (actionPointManager == null) actionPointManager = GetComponent<ActionPointManager>();
        if (battleManager == null) battleManager = GetComponent<BattleManager>();
        if (nightEventManager == null) nightEventManager = GetComponent<NightEventManager>();
    }

    public bool TryExecuteSpecialAction(LocationActionData actionData, MapCellData currentCell)
    {
        BindReferences();
        PlayerState playerState = GetState();
        if (playerState == null || actionData == null || currentCell == null) return false;

        if (currentCell.id == "black_wind_forest_entrance" && actionData.id == "observe" && !playerState.HasFlag("found_shovel"))
        {
            if (!TrySpend(actionData.costActionPoint)) return true;
            ShowBlackForestShovelEvent();
            return true;
        }

        if (currentCell.id == "black_wind_forest_entrance" && actionData.id == "enter_black_forest")
        {
            EnterBlackForestMap();
            return true;
        }

        if (currentCell.id == "forest_woods" && actionData.id == "return_from_black_forest")
        {
            ReturnFromBlackForestMap();
            return true;
        }

        if (currentCell.id == "mountain_path_east" && actionData.id == "observe" && !playerState.HasFlag("discovered_hidden_cave_path"))
        {
            if (!TrySpend(actionData.costActionPoint)) return true;
            ShowHiddenMistEvent();
            return true;
        }

        if (currentCell.id == "attic" && actionData.id == "read_books")
        {
            ReadBooksInAttic(actionData.costActionPoint);
            return true;
        }

        if (currentCell.id == "attic" && actionData.id == "look_around_attic" && !playerState.HasFlag("attic_look_hint_seen"))
        {
            if (!TrySpend(actionData.costActionPoint)) return true;
            playerState.AddFlag("attic_look_hint_seen");
            ShowMessage("多看几次书，说不定会有惊喜。");
            RefreshAll();
            return true;
        }

        if (currentCell.id == "ruined_temple" && actionData.id == "burn_incense")
        {
            BurnIncenseAtTemple(actionData.costActionPoint);
            return true;
        }

        if (currentCell.id == "ruined_temple" && actionData.id == "repair_temple")
        {
            RepairTemple();
            return true;
        }

        if (currentCell.id == "servant_yard" && actionData.id == "work_misc")
        {
            DoMiscLabor(actionData.costActionPoint);
            return true;
        }

        if (currentCell.id == "servant_yard" && actionData.id == "look_around_servant_yard")
        {
            LookAroundServantYard(actionData.costActionPoint);
            return true;
        }

        return false;
    }

    public void HandleEnterCell(MapCellData currentCell)
    {
        BindReferences();
        PlayerState playerState = GetState();
        if (playerState == null || currentCell == null) return;
        if (IsChapterOneEventOpen || BattleManager.IsBattleOpen || OpeningStoryManager.IsOpeningActive || ChapterTitleManager.IsChapterTitleActive) return;

        if (currentCell.id == "forest_cave" && !playerState.HasFlag("found_sister_in_cave") && !playerState.HasFlag("rescued_sister"))
        {
            StartCoroutine(ShowSisterCaveStoryThenOptions());
            return;
        }

        if (currentCell.id == "cave_dwelling" && !playerState.HasFlag("cave_dwelling_skeleton_seen") && !playerState.HasFlag("obtained_qiankun_fragment"))
        {
            ShowCaveSkeletonEvent();
            return;
        }

        if (currentCell.id == "back_mountain")
        {
            HandleBackMountainEnter(playerState);
            return;
        }

        if (currentCell.id == "sect_gate" && playerState.day >= 5 && !playerState.HasFlag("heard_sect_recruit_notice"))
        {
            playerState.AddFlag("heard_sect_recruit_notice");
            ShowMessage("青云宗将在第十日进行弟子选拔招募，凡有灵根者皆可一试。");
        }
    }

    private void EnterBlackForestMap()
    {
        PlayerState playerState = GetState();
        if (playerState == null) return;
        if (!playerState.HasSkill("skill_qi_training_basic") && !playerState.HasSkill("skill_body_tempering_basic"))
        {
            ShowMessage("黑风林中妖气森森，你现在毫无修炼根基，贸然进入恐怕有去无回。");
            return;
        }

        playerState.AddFlag("entered_black_forest");
        playerState.returnMainCellId = "black_wind_forest_entrance";
        SwitchToCell("forest_woods", "black_forest", "你踏入黑风林，四周雾气渐浓。");
    }

    private void ReturnFromBlackForestMap()
    {
        PlayerState playerState = GetState();
        if (playerState == null) return;
        string returnCellId = string.IsNullOrEmpty(playerState.returnMainCellId) ? "black_wind_forest_entrance" : playerState.returnMainCellId;
        SwitchToCell(returnCellId, "main", "你离开黑风林，回到入口处。");
    }

    private void SwitchToCell(string cellId, string mapId, string message)
    {
        PlayerState playerState = GetState();
        MapCellData cell = mapGridManager != null ? mapGridManager.GetCellById(cellId) : null;
        if (playerState == null || cell == null) return;
        playerState.currentMapId = mapId;
        playerState.currentCellId = cell.id;
        playerState.currentX = cell.x;
        playerState.currentY = cell.y;
        RefreshAll();
        ShowMessage(message);
    }

    private void ShowBlackForestShovelEvent()
    {
        OpenEvent(
            "旧铲子",
            "林昊在黑风林入口附近发现一把旧铲子。木柄已经磨得发亮，铁刃上还沾着泥土。",
            new List<BlockingEncounterOptionData>
            {
                new BlockingEncounterOptionData { text = "捡起铲子", closeOnly = true },
                new BlockingEncounterOptionData { text = "不管它", closeOnly = true }
            },
            delegate(BlockingEncounterOptionData option)
            {
                PlayerState playerState = GetState();
                if (option.text == "捡起铲子")
                {
                    playerState.AddItem("shovel");
                    playerState.AddFlag("found_shovel");
                    CloseEvent("获得物品：铲子");
                }
                else
                {
                    CloseEvent("你暂时没有理会那把旧铲子。");
                }
            });
    }

    private void ShowHiddenMistEvent()
    {
        OpenEvent(
            "远处雾气",
            "你站在山路东段，发现远处山壁间有一团不同寻常的雾气。雾中似乎隐约露出一条小路。",
            new List<BlockingEncounterOptionData>
            {
                new BlockingEncounterOptionData { text = "靠近查看", closeOnly = true },
                new BlockingEncounterOptionData { text = "暂时离开", closeOnly = true }
            },
            delegate(BlockingEncounterOptionData option)
            {
                PlayerState playerState = GetState();
                if (option.text == "靠近查看")
                {
                    playerState.AddFlag("discovered_hidden_cave_path");
                    UnlockCells("hidden_cave_path_01", "hidden_cave_path_02", "hidden_cave_path_03", "hidden_cave_path_04", "hidden_cave_path_05", "hidden_cave_path_06", "hidden_cave_path_07", "cave_dwelling");
                    CloseEvent("你发现了一条被雾气遮掩的小路。");
                }
                else
                {
                    CloseEvent("你记下了雾气所在的位置，决定稍后再来。");
                }
            });
    }

    private IEnumerator ShowSisterCaveStoryThenOptions()
    {
        IsChapterOneEventOpen = true;
        if (locationActionManager != null) locationActionManager.ClearCurrentButtons();
        EnsureStoryPanel();
        if (storyPanelObject != null)
        {
            storyPanelObject.SetActive(true);
            storyPanelObject.transform.SetAsLastSibling();
        }
        if (storyText != null)
        {
            storyText.text = CaveStoryText;
            storyText.color = new Color(1f, 1f, 1f, 0f);
        }
        yield return FadeStoryPanel(0f, 1f, 0.8f);
        yield return FadeStoryText(0f, 1f, 1.2f);
        yield return new WaitForSeconds(1.4f);
        yield return FadeStoryText(1f, 0f, 0.5f);
        yield return FadeStoryPanel(1f, 0f, 0.7f);
        if (storyPanelObject != null) storyPanelObject.SetActive(false);
        ShowSisterCaveEventBottomOptions();
    }

    private void ShowSisterCaveEventBottomOptions()
    {
        OpenEvent(
            "山洞中的哭声",
            "你拨开藤蔓，洞中传来微弱的哭声。那个小女孩仍蜷缩在石壁旁，颤声喊着你。",
            new List<BlockingEncounterOptionData>
            {
                new BlockingEncounterOptionData { text = "冲上前救她", startBattleId = "battle_forest_demon" },
                new BlockingEncounterOptionData { text = "先观察四周", closeOnly = true }
            },
            delegate(BlockingEncounterOptionData option)
            {
                PlayerState playerState = GetState();
                if (option.text == "先观察四周")
                {
                    playerState.AddFlag("observed_cave_before_rescue");
                    ShowMessage("洞中妖气浓重，你感觉黑暗深处有什么东西正在盯着你。");
                    ShowSisterCaveEventBottomOptions();
                    return;
                }
                playerState.AddFlag("found_sister_in_cave");
                StartBattleAndCloseEvent("battle_forest_demon", OnForestDemonBattleFinished);
            });
    }

    private void OnForestDemonBattleFinished()
    {
        PlayerState playerState = GetState();
        if (playerState == null) return;
        if (playerState.HasFlag("rescued_sister"))
        {
            playerState.AddItem("sister_ribbon");
            ShowMessage("你救下了妹妹。虽然她受了惊吓，却还活着。从这一刻起，你不再是孤身一人。");
        }
        else if (playerState.HasFlag("failed_rescue_sister"))
        {
            playerState.hp = 1;
            ShowMessage("你被妖怪击倒，意识模糊前，只听见妹妹惊恐地喊着你的名字。");
        }
        RefreshAll();
    }

    private void ShowCaveSkeletonEvent()
    {
        OpenEvent(
            "洞府骸骨",
            "洞府中盘坐着一具枯骨。你刚踏入其中，枯骨眼眶中忽然亮起幽光，竟摇摇晃晃站了起来。",
            new List<BlockingEncounterOptionData> { new BlockingEncounterOptionData { text = "迎战骸骨", startBattleId = "battle_cave_skeleton" } },
            delegate(BlockingEncounterOptionData option)
            {
                PlayerState playerState = GetState();
                playerState.AddFlag("cave_dwelling_skeleton_seen");
                StartBattleAndCloseEvent("battle_cave_skeleton", OnCaveSkeletonBattleFinished);
            });
    }

    private void OnCaveSkeletonBattleFinished()
    {
        PlayerState playerState = GetState();
        if (playerState == null) return;
        if (playerState.HasFlag("obtained_qiankun_fragment") && !playerState.HasFlag("cave_skeleton_reward_claimed"))
        {
            playerState.AddFlag("cave_skeleton_reward_claimed");
            playerState.spiritStones += 30;
            playerState.AddItem("qiankun_body_scroll_fragment");
            ShowMessage("你在洞府石匣中找到一些灵石，以及一页残破的锻体功法。");
            RefreshAll();
        }
    }

    private void HandleBackMountainEnter(PlayerState playerState)
    {
        if (!playerState.HasFlag("heard_cliff_call_day"))
        {
            playerState.AddFlag("heard_cliff_call_day");
            playerState.SetCounter("cliff_call_first_day", playerState.day);
            OpenEvent("悬崖下的呼唤", "你来到后山，忽然感觉悬崖下方有什么东西在呼唤自己。那种感觉一闪而逝，像是幻觉。", new List<BlockingEncounterOptionData> { new BlockingEncounterOptionData { text = "离开", closeOnly = true } }, delegate(BlockingEncounterOptionData option) { CloseEvent("你将那一瞬间的异样压在心底。"); });
            return;
        }

        int firstDay = playerState.GetCounter("cliff_call_first_day");
        if (firstDay > 0 && playerState.day > firstDay && !playerState.HasFlag("resolved_cliff_choice") && !playerState.HasFlag("jumped_down_cliff"))
        {
            OpenEvent(
                "金光与低语",
                "悬崖下亮起隐隐金光。一个蛊惑般的声音在耳边响起：“想要守护自己最重要的人吗？那就跳下来吧。”",
                new List<BlockingEncounterOptionData> { new BlockingEncounterOptionData { text = "跳下去", closeOnly = true }, new BlockingEncounterOptionData { text = "不跳下去", closeOnly = true } },
                delegate(BlockingEncounterOptionData option)
                {
                    if (option.text == "跳下去")
                    {
                        playerState.AddFlag("jumped_down_cliff");
                        CloseEvent("你纵身跃下悬崖，新的地图内容暂未开放。");
                    }
                    else
                    {
                        playerState.AddFlag("refused_cliff_call");
                        playerState.AddFlag("resolved_cliff_choice");
                        CloseEvent("你后退一步，将那道声音压在心底。");
                    }
                });
        }
    }

    private void ReadBooksInAttic(int cost)
    {
        PlayerState playerState = GetState();
        if (playerState.HasDoneToday("attic_read_books"))
        {
            ShowMessage("今日已经查看过书籍了。");
            return;
        }
        if (!TrySpend(cost)) return;
        playerState.MarkDoneToday("attic_read_books");
        int roll = UnityEngine.Random.Range(1, 101);
        int gain = roll <= 70 ? 1 : (roll <= 95 ? 3 : 5);
        playerState.cultivation += gain;
        playerState.AddCounter("attic_read_count", 1);
        string message = "你翻看阁楼旧书，心有所悟，修为 +" + gain + "。";
        if (playerState.GetCounter("attic_read_count") >= 3 && !playerState.HasFlag("attic_books_reward_claimed"))
        {
            playerState.AddFlag("attic_books_reward_claimed");
            playerState.LearnSkill("skill_qi_training_basic");
            playerState.LearnSkill("spell_guiyuan_qigong");
            message += "你终于读懂几卷入门书册，学会了练气入门与归元气功。";
        }
        ShowMessage(message);
        RefreshAll();
    }

    private void BurnIncenseAtTemple(int cost)
    {
        PlayerState playerState = GetState();
        if (playerState.HasDoneToday("temple_burn_incense"))
        {
            ShowMessage("今日已经上过香了。");
            return;
        }
        if (!TrySpend(cost)) return;
        playerState.MarkDoneToday("temple_burn_incense");
        playerState.AddCounter("temple_incense_count", 1);
        int roll = UnityEngine.Random.Range(0, 3);
        string message;
        if (roll == 0)
        {
            playerState.cultivation += 2;
            message = "今日心神安宁，修为 +2。";
        }
        else if (roll == 1)
        {
            playerState.hp = Mathf.Min(playerState.maxHp, playerState.hp + 5);
            message = "今日气血顺畅，hp +5。";
        }
        else
        {
            playerState.AddFlag("temple_blessing_today");
            message = "今日似有神意庇护。";
        }
        if (playerState.GetCounter("temple_incense_count") >= 5) playerState.AddFlag("temple_incense_5");
        if (playerState.HasFlag("temple_repaired") && !playerState.HasFlag("received_star_sword"))
        {
            playerState.AddPendingNightEvent("night_temple_god_reward");
            message += " 夜色降临时，或许会有异象。";
        }
        ShowMessage(message);
        RefreshAll();
    }

    private void RepairTemple()
    {
        PlayerState playerState = GetState();
        if (playerState.HasFlag("temple_repaired"))
        {
            ShowMessage("寺庙已经修缮过了。");
            return;
        }
        if (playerState.GetCounter("temple_incense_count") < 5)
        {
            ShowMessage("你对这里还不够熟悉，暂时不知道该如何修缮。需上香满 5 次。 ");
            return;
        }
        if (playerState.spiritStones < 50)
        {
            ShowMessage("修缮破庙需要 50 灵石。你现在灵石不足。 ");
            return;
        }
        playerState.spiritStones -= 50;
        playerState.AddFlag("temple_repaired");
        if (!playerState.HasFlag("received_star_sword")) playerState.AddPendingNightEvent("night_temple_god_reward");
        ShowMessage("你请人简单修缮了破庙。今夜若回小屋休息，也许会梦见什么。 ");
        RefreshAll();
    }

    private void DoMiscLabor(int cost)
    {
        PlayerState playerState = GetState();
        if (!TrySpend(cost)) return;
        int level = Mathf.Clamp(playerState.GetCounter("labor_level"), 1, 3);
        if (level <= 0) level = 1;
        int reward = level == 1 ? 5 : (level == 2 ? 8 : 12);
        playerState.spiritStones += reward;
        playerState.AddCounter("labor_exp", 1);
        string message = "你完成杂役劳作，获得灵石 " + reward + "。";
        int exp = playerState.GetCounter("labor_exp");
        if (exp >= 3 && level < 3)
        {
            level += 1;
            playerState.SetCounter("labor_level", level);
            playerState.SetCounter("labor_exp", 0);
            message += "你对杂役事务更加熟练，劳作等级提升到 " + level + "。";
        }
        else if (playerState.GetCounter("labor_level") <= 0) playerState.SetCounter("labor_level", 1);
        ShowMessage(message);
        RefreshAll();
    }

    private void LookAroundServantYard(int cost)
    {
        PlayerState playerState = GetState();
        if (!TrySpend(cost)) return;
        if (!playerState.HasFlag("got_broom"))
        {
            playerState.AddFlag("got_broom");
            playerState.AddItem("broom");
            ShowMessage("你在角落找到一把旧扫帚，虽然寒酸，却勉强能当作武器。");
        }
        else
        {
            ShowMessage("你在杂役院四处看了看，只看见柴米、水桶和来往忙碌的杂役弟子。");
        }
        RefreshAll();
    }

    private void StartBattleAndCloseEvent(string battleId, Action onFinished)
    {
        IsChapterOneEventOpen = false;
        if (locationActionManager != null) locationActionManager.ClearCurrentButtons();
        if (battleManager == null) battleManager = GetComponent<BattleManager>();
        if (battleManager != null) battleManager.StartBattle(battleId, onFinished);
    }

    private void OpenEvent(string title, string text, List<BlockingEncounterOptionData> options, Action<BlockingEncounterOptionData> onOptionClicked)
    {
        IsChapterOneEventOpen = true;
        if (locationActionManager != null) locationActionManager.ClearCurrentButtons();
        if (locationUIManager != null) locationUIManager.ShowEvent(title, "\n" + text);
        if (locationActionManager != null) locationActionManager.CreateEncounterOptionButtons(options, onOptionClicked);
    }

    private void CloseEvent(string message)
    {
        IsChapterOneEventOpen = false;
        RefreshAll();
        if (!string.IsNullOrEmpty(message)) ShowMessage(message);
    }

    public void CloseChapterOneEventSilently()
    {
        IsChapterOneEventOpen = false;
        if (storyPanelObject != null) storyPanelObject.SetActive(false);
    }

    private bool TrySpend(int cost)
    {
        if (actionPointManager == null) actionPointManager = GetComponent<ActionPointManager>();
        return actionPointManager != null && actionPointManager.TrySpendActionPoints(cost);
    }

    private void UnlockCells(params string[] cellIds)
    {
        PlayerState playerState = GetState();
        if (playerState == null || cellIds == null) return;
        foreach (string cellId in cellIds) playerState.UnlockCell(cellId);
    }

    private PlayerState GetState()
    {
        BindReferences();
        return gameManager != null ? gameManager.GetPlayerState() : null;
    }

    private void RefreshAll()
    {
        PlayerState playerState = GetState();
        MapCellData currentCell = mapGridManager != null ? mapGridManager.GetCurrentCell() : null;
        if (mapGridManager != null) mapGridManager.RefreshMap();
        if (locationUIManager != null)
        {
            locationUIManager.RefreshLocation(currentCell, playerState);
            locationUIManager.RefreshPlayerStatus(playerState);
        }
        if (locationActionManager != null) locationActionManager.RefreshCurrentLocation();
    }

    private void ShowMessage(string message)
    {
        if (locationUIManager != null) locationUIManager.ShowMessage(message);
    }

    private void EnsureStoryPanel()
    {
        if (storyPanelObject != null) return;
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;
        storyPanelObject = new GameObject("ChapterOneStoryPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        storyPanelObject.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = storyPanelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        Image image = storyPanelObject.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;
        storyCanvasGroup = storyPanelObject.GetComponent<CanvasGroup>();
        storyCanvasGroup.alpha = 0f;
        storyCanvasGroup.blocksRaycasts = true;
        storyCanvasGroup.interactable = true;
        GameObject textObject = new GameObject("ChapterOneStoryText", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(storyPanelObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.14f, 0.28f);
        textRect.anchorMax = new Vector2(0.86f, 0.72f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        storyText = textObject.GetComponent<Text>();
        storyText.font = GetDefaultFont();
        storyText.fontSize = 28;
        storyText.alignment = TextAnchor.MiddleCenter;
        storyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        storyText.verticalOverflow = VerticalWrapMode.Overflow;
        storyText.color = new Color(1f, 1f, 1f, 0f);
        storyPanelObject.SetActive(false);
    }

    private IEnumerator FadeStoryPanel(float from, float to, float duration)
    {
        float timer = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (timer < safeDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / safeDuration);
            if (storyCanvasGroup != null) storyCanvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }
        if (storyCanvasGroup != null) storyCanvasGroup.alpha = to;
    }

    private IEnumerator FadeStoryText(float from, float to, float duration)
    {
        float timer = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (timer < safeDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / safeDuration);
            if (storyText != null) storyText.color = new Color(1f, 1f, 1f, Mathf.Lerp(from, to, t));
            yield return null;
        }
        if (storyText != null) storyText.color = new Color(1f, 1f, 1f, to);
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
