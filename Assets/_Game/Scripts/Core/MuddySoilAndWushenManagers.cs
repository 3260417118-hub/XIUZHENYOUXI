using UnityEngine;

/// <summary>
/// 黑风林泥泞土壤挖掘逻辑。
/// 由 LocationActionManager 在 dig_muddy_soil 行为被点击时调用。
/// </summary>
public class MuddySoilDigManager : MonoBehaviour
{
    private const string DigCountKey = "dig_muddy_soil_count_today";
    private const int MaxDigCountPerDay = 3;

    private GameManager gameManager;
    private ActionPointManager actionPointManager;
    private LocationUIManager locationUIManager;
    private LocationActionManager locationActionManager;
    private InventoryManager inventoryManager;

    private void BindReferences()
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        if (actionPointManager == null) actionPointManager = GetComponent<ActionPointManager>();
        if (locationUIManager == null) locationUIManager = GetComponent<LocationUIManager>();
        if (locationActionManager == null) locationActionManager = GetComponent<LocationActionManager>();
        if (inventoryManager == null) inventoryManager = GetComponent<InventoryManager>();
    }

    public bool TryHandleDig(LocationActionData actionData, MapCellData currentCell)
    {
        BindReferences();
        if (actionData == null || currentCell == null) return false;
        if (actionData.id != "dig_muddy_soil") return false;

        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState == null) return true;

        if (currentCell.id != "forest_muddy_soil")
        {
            ShowMessage("这里没有可以挖掘的泥土。");
            return true;
        }

        if (!playerState.HasItem("shovel"))
        {
            ShowMessage("你没有合适的工具，无法挖开这片泥土。 ");
            RefreshAll(playerState);
            return true;
        }

        int digCount = playerState.GetCounter(DigCountKey);
        if (digCount >= MaxDigCountPerDay)
        {
            ShowMessage("这片泥地今日已经被你翻得差不多了，继续挖也难有收获。 ");
            RefreshAll(playerState);
            return true;
        }

        if (actionPointManager == null || !actionPointManager.HasEnoughActionPoints(1))
        {
            ShowMessage("你已经没有力气继续挖掘了。 ");
            RefreshAll(playerState);
            return true;
        }

        if (!actionPointManager.TrySpendActionPoints(1))
        {
            ShowMessage("你已经没有力气继续挖掘了。 ");
            RefreshAll(playerState);
            return true;
        }

        playerState.AddCounter(DigCountKey, 1);
        RollDigReward(playerState);
        RefreshAll(playerState);
        return true;
    }

    private void RollDigReward(PlayerState playerState)
    {
        float roll = Random.value;

        if (roll < 0.60f)
        {
            playerState.spiritStones += 10;
            ShowMessage("你挖开泥土，发现几枚散落的灵石。获得 10 灵石。 ");
            return;
        }

        if (roll < 0.70f)
        {
            playerState.spiritStones += 50;
            ShowMessage("铲尖碰到一块硬物。你扒开泥土，竟发现一小袋灵石。获得 50 灵石。 ");
            return;
        }

        if (roll < 0.90f)
        {
            AddItem(playerState, "healing_pill", 1);
            ShowMessage("你从泥土中挖出一个破旧小瓶，里面还剩一枚疗伤丹。获得：疗伤丹。 ");
            return;
        }

        if (roll < 0.95f)
        {
            AddItem(playerState, "shovel", 1);
            ShowMessage("你挖着挖着，竟又挖出一把旧铲子。获得：铲子。 ");
            return;
        }

        if (playerState.HasItem("wushen_body_scroll_fragment") || playerState.HasFlag("found_wushen_body_scroll_fragment"))
        {
            playerState.spiritStones += 50;
            ShowMessage("你挖到一些深埋的灵石。获得 50 灵石。 ");
            return;
        }

        AddItem(playerState, "wushen_body_scroll_fragment", 1);
        playerState.AddFlag("found_wushen_body_scroll_fragment");
        ShowMessage("你从泥土深处挖出一页残破古卷。卷页入手微热，似有气血轰鸣之声。获得：武神锻体诀（残）。 ");
    }

    private void AddItem(PlayerState playerState, string itemId, int count)
    {
        if (inventoryManager != null) inventoryManager.AddItem(itemId, count);
        else playerState.AddItem(itemId, count);
    }

    private void RefreshAll(PlayerState playerState)
    {
        if (locationUIManager != null) locationUIManager.RefreshPlayerStatus(playerState);
        CharacterStatusUIManager statusUI = GetComponent<CharacterStatusUIManager>();
        if (statusUI != null) statusUI.RefreshIfOpen();
        InventoryUIManager inventoryUI = GetComponent<InventoryUIManager>();
        if (inventoryUI != null) inventoryUI.RefreshIfOpen();
        if (locationActionManager != null) locationActionManager.RefreshCurrentLocation();
    }

    private void ShowMessage(string message)
    {
        if (locationUIManager != null) locationUIManager.ShowMessage(message);
        else Debug.Log(message);
    }
}

/// <summary>
/// 武神锻体诀（残）的第一版效果：拥有残卷后，每次正常锻体修炼额外 +25 锻体值。
/// 不改锻体系统主体，避免破坏现有逻辑；这里只监听破败小屋锻体后的数值变化并补发奖励。
/// </summary>
public class WushenBodyTrainingBonusManager : MonoBehaviour
{
    private GameManager gameManager;
    private MapGridManager mapGridManager;
    private LocationUIManager locationUIManager;
    private int lastBodyCultivation;
    private int lastDay;
    private bool initialized;

    private void BindReferences()
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        if (mapGridManager == null) mapGridManager = GetComponent<MapGridManager>();
        if (locationUIManager == null) locationUIManager = GetComponent<LocationUIManager>();
    }

    private void Update()
    {
        BindReferences();
        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        if (playerState == null) return;

        if (!initialized || playerState.day != lastDay)
        {
            initialized = true;
            lastDay = playerState.day;
            lastBodyCultivation = playerState.bodyCultivation;
            return;
        }

        int delta = playerState.bodyCultivation - lastBodyCultivation;
        if (delta > 0 && ShouldApplyBonus(playerState, delta))
        {
            playerState.bodyCultivation += 25;
            lastBodyCultivation = playerState.bodyCultivation;
            if (locationUIManager != null)
            {
                locationUIManager.RefreshPlayerStatus(playerState);
                locationUIManager.ShowMessage("你体内气血被《武神锻体诀（残）》牵引，锻体修炼额外获得 25 点锻体值。 ");
            }
            CharacterStatusUIManager statusUI = GetComponent<CharacterStatusUIManager>();
            if (statusUI != null) statusUI.RefreshIfOpen();
            return;
        }

        lastBodyCultivation = playerState.bodyCultivation;
    }

    private bool ShouldApplyBonus(PlayerState playerState, int delta)
    {
        if (!playerState.HasSkill("skill_body_tempering_basic")) return false;
        if (!playerState.HasItem("wushen_body_scroll_fragment") && !playerState.HasFlag("found_wushen_body_scroll_fragment")) return false;
        MapCellData currentCell = mapGridManager != null ? mapGridManager.GetCurrentCell() : null;
        if (currentCell == null || currentCell.id != "ruined_hut") return false;
        // 目前破败小屋中锻体修炼基础收益为 10；避免战斗奖励或剧情奖励误触发。
        return delta <= 15;
    }
}
