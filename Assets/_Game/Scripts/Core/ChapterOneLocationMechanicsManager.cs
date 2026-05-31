using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 第一章地点与早期剧情机制。
/// 只做轻量剧情、奖励、地图切换，不做完整背包 UI、队友系统、商店系统。
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

    private const int SisterDailyActionPointBonusLimit = 2;
    private const int SisterActionPointBonusTotalLimit = 10;
    private const string SisterBonusCounterId = "sister_action_point_bonus_total";
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

    public void HandleDailyStoryTrigger()
    {
        BindReferences();
        PlayerState playerState = GetState();
        if (playerState == null) return;

        if (playerState.day == 3 && !playerState.HasFlag("day3_heard_forest_cry") && !playerState.HasFlag("rescued_sister"))
        {
            playerState.AddFlag("day3_heard_forest_cry");
            ShowMessage("远处黑风林方向传来若有若无的哭声。那声音让林昊心口一紧。");
        }
    }

    public bool ShouldShowChapterOneNpc(string npcId, MapCellData currentCell)
    {
        PlayerState playerState = GetState();
        if (playerState == null || currentCell == null || string.IsNullOrEmpty(npcId)) return false;

        if (npcId == "passing_farmer")
        {
            // 农民第 1 天一直留在村口，直到玩家点击“道谢离开”。
            return currentCell.id == "village_gate" && playerState.day == 1 && !playerState.HasFlag("passing_farmer_left");
        }

        if (npcId == "sister")
        {
            return currentCell.id == "ruined_hut" && playerState.HasFlag("sister_at_ruined_hut");
        }

        if (npcId == "jianghe")
        {
            return currentCell.id == "attic" && playerState.day == 6 && !playerState.HasSkill("skill_body_tempering_basic");
        }

        return false;
    }

    public string GetChapterOneNpcName(string npcId)
    {
        if (npcId == "passing_farmer") return "路过的农民";
        if (npcId == "sister") return "妹妹";
        if (npcId == "jianghe") return "江鹤";
        return npcId;
    }

    public void StartChapterOneNpcDialogue(string npcId)
    {
        BindReferences();
        if (npcId == "passing_farmer") ShowPassingFarmerDialogue("");
        else if (npcId == "sister") ShowSisterDialogue();
        else if (npcId == "jianghe") ShowJiangheDialogue();
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

        // 后山悬崖剧情已统一交给 CliffStoryManager，避免旧事件抢占新黑屏剧情和崖底地图跳转。
        if (currentCell.id == "back_mountain") return;

        if (currentCell.id == "sect_gate" && playerState.day >= 5 && !playerState.HasFlag("heard_sect_recruit_notice"))
        {
            playerState.AddFlag("heard_sect_recruit_notice");
            ShowMessage("青云宗将在第十日进行弟子选拔招募，凡有灵根者皆可一试。");
        }
    }

    private void ShowPassingFarmerDialogue(string currentText)
    {
        PlayerState playerState = GetState();
        if (playerState == null) return;

        List<BlockingEncounterOptionData> options = new List<BlockingEncounterOptionData>();
        if (!playerState.HasFlag("heard_qingshi_village_info"))
        {
            options.Add(new BlockingEncounterOptionData { text = "询问青石村", closeOnly = true });
        }
        if (!playerState.HasFlag("heard_zhao_batian_warning"))
        {
            options.Add(new BlockingEncounterOptionData { text = "询问赵霸天", closeOnly = true });
        }
        if (playerState.HasFlag("heard_qingshi_village_info") && playerState.HasFlag("heard_zhao_batian_warning"))
        {
            options.Add(new BlockingEncounterOptionData { text = "道谢离开", closeOnly = true });
        }

        if (string.IsNullOrEmpty(currentText))
        {
            currentText = "一个扛着锄头的农民路过村口，见你衣衫破损，忍不住停下脚步：“小兄弟，你这是从哪儿来的？这里是青石村，往东是山路，往北有破庙，往南是后山。只是你若想在这儿落脚，记住一句话，莫要招惹赵霸天的人。”";
        }

        OpenEvent("陌生村人", currentText, options, delegate(BlockingEncounterOptionData option)
        {
            PlayerState state = GetState();
            if (state == null) return;

            if (option.text == "询问青石村")
            {
                state.AddFlag("heard_qingshi_village_info");
                ShowPassingFarmerDialogue("农民说道：“青石村不大，靠山吃山。村里有药田、商铺，也有些给青云宗打杂的人。”");
            }
            else if (option.text == "询问赵霸天")
            {
                state.AddFlag("heard_zhao_batian_warning");
                ShowPassingFarmerDialogue("农民压低声音：“赵霸天是村里一霸，手下养着不少打手。你这外乡人，最好离他远点。”");
            }
            else
            {
                state.AddFlag("passing_farmer_left");
                CloseEvent("农民摆摆手，扛起锄头继续往村里走去。");
            }
        });
    }

    private void ShowSisterDialogue()
    {
        PlayerState playerState = GetState();
        if (playerState == null) return;

        int bonusTotal = playerState.GetCounter(SisterBonusCounterId);
        int dailyBonusCount = GetSisterTalkCountToday(playerState);
        string text = bonusTotal <= 0
            ? "妹妹坐在破败小屋的干草堆旁，双手紧紧攥着衣角。见你回来，她抬起头，小声喊道：“哥哥……”"
            : GetRandomSisterLine();

        if (bonusTotal >= SisterActionPointBonusTotalLimit)
        {
            text += "\n\n她已经从最初的恐惧中缓过来一些。你们仍可以说话，只是那份支撑已经不会再额外化作行动点。";
        }
        else if (dailyBonusCount >= SisterDailyActionPointBonusLimit)
        {
            text += "\n\n今天你已经从妹妹这里获得了足够的支撑。继续对话不会再增加行动点，明天休息后会重置。";
        }
        else
        {
            text += "\n\n今日妹妹行动点支撑：" + dailyBonusCount + "/" + SisterDailyActionPointBonusLimit + "，累计：" + bonusTotal + "/" + SisterActionPointBonusTotalLimit + "。";
        }

        OpenEvent(
            "妹妹",
            text,
            new List<BlockingEncounterOptionData>
            {
                new BlockingEncounterOptionData { text = "安慰她", closeOnly = true },
                new BlockingEncounterOptionData { text = "询问她还记得什么", closeOnly = true },
                new BlockingEncounterOptionData { text = "陪她坐一会儿", closeOnly = true }
            },
            delegate(BlockingEncounterOptionData option)
            {
                if (option.text == "询问她还记得什么")
                {
                    playerState.AddFlag("asked_sister_memory");
                    ApplySisterTalkReward("妹妹努力回想，却只记得黑暗、绳索和山洞里的腥气。她的声音发颤，你没有继续追问。");
                }
                else if (option.text == "陪她坐一会儿")
                {
                    ApplySisterTalkReward("你陪妹妹在干草堆旁安静坐了一会儿。她慢慢放松下来，靠着墙低声说：“哥哥，我会努力不拖累你。”");
                }
                else
                {
                    ApplySisterTalkReward("你轻声安慰了妹妹几句。她的情绪似乎平稳了一些，你心中也多了几分继续撑下去的力量。");
                }
            });
    }

    private string GetRandomSisterLine()
    {
        int roll = UnityEngine.Random.Range(0, 3);
        if (roll == 0) return "妹妹轻轻拉住你的衣角：“哥哥，你会一直在吗？”";
        if (roll == 1) return "她望着破败小屋外的夜色，小声说道：“我不喜欢这里的风声。”";
        return "妹妹靠在墙边，似乎终于能安心睡一会儿了。";
    }

    private int GetSisterTalkCountToday(PlayerState playerState)
    {
        if (playerState == null) return 0;
        string prefix = "sister_ap_bonus_day_" + playerState.day + "_";
        int count = 0;
        foreach (string record in playerState.dailyActionRecords)
        {
            if (!string.IsNullOrEmpty(record) && record.StartsWith(prefix)) count++;
        }
        return count;
    }

    private void ApplySisterTalkReward(string baseMessage)
    {
        PlayerState playerState = GetState();
        if (playerState == null) return;

        int bonusTotal = playerState.GetCounter(SisterBonusCounterId);
        int dailyBonusCount = GetSisterTalkCountToday(playerState);
        string finalMessage = baseMessage;

        if (bonusTotal < SisterActionPointBonusTotalLimit && dailyBonusCount < SisterDailyActionPointBonusLimit)
        {
            bonusTotal += 1;
            dailyBonusCount += 1;
            playerState.SetCounter(SisterBonusCounterId, bonusTotal);
            playerState.MarkDoneToday("sister_ap_bonus_day_" + playerState.day + "_" + dailyBonusCount);
            playerState.actionPoints += 1;
            finalMessage += " 行动点 +1。（今日 " + dailyBonusCount + "/" + SisterDailyActionPointBonusLimit + "，累计 " + bonusTotal + "/" + SisterActionPointBonusTotalLimit + "）";
        }
        else if (bonusTotal >= SisterActionPointBonusTotalLimit)
        {
            finalMessage += " 你已经获得过妹妹带来的全部精神支撑，本次不再增加行动点。";
        }
        else
        {
            finalMessage += " 今天已经从妹妹这里获得 2 次行动点支撑，本次不再增加行动点。";
        }

        IsChapterOneEventOpen = false;
        RefreshAll();
        ShowMessage(finalMessage);
    }

    private void ShowJiangheDialogue()
    {
        OpenEvent(
            "阁楼中的神秘人",
            "阁楼阴影中坐着一名青衫男子。他似乎早已等在那里，指尖轻轻敲着桌面。“你身上有一页残卷。那东西留着对你无用，反而会招来杀身之祸。不如与我换一门真正能活命的法子。”",
            new List<BlockingEncounterOptionData>
            {
                new BlockingEncounterOptionData { text = "交出乾坤锻体诀（残卷）", requireItems = new[] { "qiankun_body_scroll_fragment" }, closeOnly = true },
                new BlockingEncounterOptionData { text = "询问他的身份", closeOnly = true },
                new BlockingEncounterOptionData { text = "拒绝交易", closeOnly = true }
            },
            delegate(BlockingEncounterOptionData option)
            {
                PlayerState playerState = GetState();
                if (option.text == "交出乾坤锻体诀（残卷）")
                {
                    playerState.RemoveItem("qiankun_body_scroll_fragment");
                    LearnSkill(playerState, "skill_body_tempering_basic");
                    playerState.AddFlag("traded_fragment_with_jianghe");
                    CloseEvent("江鹤收起残卷，随手丢给你一册薄书。《锻体入门》四字映入眼中。");
                }
                else if (option.text == "询问他的身份")
                {
                    playerState.AddFlag("met_jianghe");
                    CloseEvent("江鹤淡淡道：“知道太多，对现在的你没好处。你只需记住，盯上林家的，不止凡人。”");
                }
                else
                {
                    playerState.AddFlag("refused_jianghe_trade");
                    CloseEvent("江鹤轻笑一声：“也好。等你知道它会引来什么，再后悔也不迟。”");
                }
            });
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
        OpenEvent("旧铲子", "林昊在黑风林入口附近发现一把旧铲子。木柄已经磨得发亮，铁刃上还沾着泥土。", new List<BlockingEncounterOptionData> { new BlockingEncounterOptionData { text = "捡起铲子", closeOnly = true }, new BlockingEncounterOptionData { text = "不管它", closeOnly = true } }, delegate(BlockingEncounterOptionData option)
        {
            PlayerState playerState = GetState();
            if (option.text == "捡起铲子")
            {
                playerState.AddItem("shovel");
                playerState.AddFlag("found_shovel");
                CloseEvent("获得物品：铲子");
            }
            else CloseEvent("你暂时没有理会那把旧铲子。");
        });
    }

    private void ShowHiddenMistEvent()
    {
        OpenEvent("远处雾气", "你站在山路东段，发现远处山壁间有一团不同寻常的雾气。雾中似乎隐约露出一条小路。", new List<BlockingEncounterOptionData> { new BlockingEncounterOptionData { text = "靠近查看", closeOnly = true }, new BlockingEncounterOptionData { text = "暂时离开", closeOnly = true } }, delegate(BlockingEncounterOptionData option)
        {
            PlayerState playerState = GetState();
            if (option.text == "靠近查看")
            {
                playerState.AddFlag("discovered_hidden_cave_path");
                UnlockCells("hidden_cave_path_01", "hidden_cave_path_02", "hidden_cave_path_03", "hidden_cave_path_04", "hidden_cave_path_05", "hidden_cave_path_06", "hidden_cave_path_07", "cave_dwelling");
                CloseEvent("你发现了一条被雾气遮掩的小路。");
            }
            else CloseEvent("你记下了雾气所在的位置，决定稍后再来。");
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
        OpenEvent("山洞中的哭声", "你拨开藤蔓，洞中传来微弱的哭声。那个小女孩仍蜷缩在石壁旁，颤声喊着你。", new List<BlockingEncounterOptionData> { new BlockingEncounterOptionData { text = "冲上前救她", startBattleId = "battle_forest_demon" }, new BlockingEncounterOptionData { text = "先观察四周", closeOnly = true } }, delegate(BlockingEncounterOptionData option)
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
            playerState.AddFlag("sister_at_ruined_hut");
            playerState.AddItem("sister_ribbon");
            ShowMessage("妹妹被你救下后，暂时住进了破败小屋。她仍然惊魂未定，只偶尔低声喊你“哥哥”。");
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
        OpenEvent("洞府骸骨", "洞府中盘坐着一具枯骨。你刚踏入其中，枯骨眼眶中忽然亮起幽光，竟摇摇晃晃站了起来。", new List<BlockingEncounterOptionData> { new BlockingEncounterOptionData { text = "迎战骸骨", startBattleId = "battle_cave_skeleton" } }, delegate(BlockingEncounterOptionData option)
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
            CurrencyManager.AddSpiritStones(playerState, 30);
            playerState.AddItem("qiankun_body_scroll_fragment");
            ShowMessage("你在洞府石匣中找到一些灵石，以及一页残破的锻体功法。");
            RefreshAll();
        }
    }

    private void ReadBooksInAttic(int cost)
    {
        PlayerState playerState = GetState();
        if (playerState.HasDoneToday("attic_read_books")) { ShowMessage("今日已经查看过书籍了。"); return; }
        if (!TrySpend(cost)) return;
        playerState.MarkDoneToday("attic_read_books");
        int roll = UnityEngine.Random.Range(1, 101);
        int gain = roll <= 70 ? 1 : (roll <= 95 ? 3 : 5);
        playerState.cultivation += gain;
        ToastManager.TryShowSuccess("修为 +" + gain);
        playerState.AddCounter("attic_read_count", 1);
        string message = "你翻看阁楼旧书，心有所悟，修为 +" + gain + "。";
        if (playerState.GetCounter("attic_read_count") >= 3 && !playerState.HasFlag("attic_books_reward_claimed"))
        {
            playerState.AddFlag("attic_books_reward_claimed");
            LearnSkill(playerState, "skill_qi_training_basic");
            LearnSkill(playerState, "spell_guiyuan_qigong");
            message += "你终于读懂几卷入门书册，学会了练气入门与归元气功。";
        }
        ShowMessage(message);
        RefreshAll();
    }

    private void BurnIncenseAtTemple(int cost)
    {
        PlayerState playerState = GetState();
        if (playerState.HasDoneToday("temple_burn_incense")) { ShowMessage("今日已经上过香了。"); return; }
        if (!TrySpend(cost)) return;
        playerState.MarkDoneToday("temple_burn_incense");
        playerState.AddCounter("temple_incense_count", 1);
        int roll = UnityEngine.Random.Range(0, 3);
        string message;
        if (roll == 0) { playerState.cultivation += 2; ToastManager.TryShowSuccess("修为 +2"); message = "今日心神安宁，修为 +2。"; }
        else if (roll == 1) { playerState.hp = Mathf.Min(playerState.maxHp, playerState.hp + 5); message = "今日气血顺畅，hp +5。"; }
        else { playerState.AddFlag("temple_blessing_today"); message = "今日似有神意庇护。"; }
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
        if (playerState.HasFlag("temple_repaired")) { ShowMessage("寺庙已经修缮过了。"); return; }
        if (playerState.GetCounter("temple_incense_count") < 5) { ShowMessage("你对这里还不够熟悉，暂时不知道该如何修缮。需上香满 5 次。 "); return; }
        if (playerState.spiritStones < 50) { ShowMessage("修缮破庙需要 50 灵石。你现在灵石不足。 "); return; }
        CurrencyManager.SpendSpiritStones(playerState, 50);
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
        CurrencyManager.AddSpiritStones(playerState, reward);
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
        else ShowMessage("你在杂役院四处看了看，只看见柴米、水桶和来往忙碌的杂役弟子。");
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

    private void LearnSkill(PlayerState playerState, string skillId)
    {
        if (playerState == null || string.IsNullOrEmpty(skillId)) return;
        SkillManager skillManager = GetComponent<SkillManager>();
        if (skillManager != null) skillManager.LearnSkill(skillId);
        else playerState.LearnSkill(skillId);
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
