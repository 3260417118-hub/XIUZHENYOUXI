using UnityEngine;

/// <summary>
/// 背包、道具使用、单武器槽装备管理。
/// 第一版只做数量、消耗品使用和武器装备，不做复杂装备系统。
/// </summary>
public class InventoryManager : MonoBehaviour
{
    private GameManager gameManager;
    private LocationUIManager locationUIManager;
    private RealmManager realmManager;
    private BodyRealmManager bodyRealmManager;

    private void Start()
    {
        BindReferences();
        ItemDatabase.EnsureLoaded();
        PlayerState state = GetState();
        if (state != null)
        {
            state.EnsureLists();
            if (!string.IsNullOrEmpty(state.equippedWeaponId) && !HasItem(state.equippedWeaponId))
            {
                state.equippedWeaponId = "";
            }
            RecalculateStats(false);
        }
    }

    private void BindReferences()
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        if (locationUIManager == null) locationUIManager = GetComponent<LocationUIManager>();
        if (realmManager == null) realmManager = GetComponent<RealmManager>();
        if (bodyRealmManager == null) bodyRealmManager = GetComponent<BodyRealmManager>();
    }

    public void AddItem(string itemId)
    {
        AddItem(itemId, 1);
    }

    public void AddItem(string itemId, int count = 1)
    {
        PlayerState state = GetState();
        if (state == null || string.IsNullOrEmpty(itemId) || count <= 0) return;
        state.AddItem(itemId, count);
        RefreshUi();
    }

    public void RemoveItem(string itemId)
    {
        RemoveItem(itemId, 1);
    }

    public void RemoveItem(string itemId, int count = 1)
    {
        PlayerState state = GetState();
        if (state == null || string.IsNullOrEmpty(itemId) || count <= 0) return;
        state.RemoveItem(itemId, count);
        if (!string.IsNullOrEmpty(state.equippedWeaponId) && !HasItem(state.equippedWeaponId))
        {
            state.equippedWeaponId = "";
            RecalculateStats(false);
        }
        RefreshUi();
    }

    public bool HasItem(string itemId)
    {
        return HasItem(itemId, 1);
    }

    public bool HasItem(string itemId, int count = 1)
    {
        PlayerState state = GetState();
        return state != null && state.HasItem(itemId, count);
    }

    public int GetItemCount(string itemId)
    {
        PlayerState state = GetState();
        return state != null ? state.GetItemCount(itemId) : 0;
    }

    public bool UseItem(string itemId)
    {
        if (BattleManager.IsBattleOpen)
        {
            ShowMessage("战斗中暂时不能使用背包。");
            return false;
        }

        PlayerState state = GetState();
        if (state == null || string.IsNullOrEmpty(itemId)) return false;
        if (!state.HasItem(itemId))
        {
            ShowMessage("背包中没有该物品。");
            return false;
        }

        ItemData item = ItemDatabase.GetItem(itemId);
        if (item == null)
        {
            ShowMessage("找不到物品数据：" + itemId);
            return false;
        }

        if (!item.usable)
        {
            ShowMessage(item.name + "不能主动使用。");
            return false;
        }

        bool changed = false;
        string message = "使用了：" + item.name;

        if (item.cultivationGain != 0)
        {
            state.cultivation += item.cultivationGain;
            message += "，修为 +" + item.cultivationGain;
            changed = true;
        }

        if (item.hpGain != 0)
        {
            int oldHp = state.hp;
            state.hp = Mathf.Clamp(state.hp + item.hpGain, 1, state.maxHp);
            message += "，生命 +" + Mathf.Max(0, state.hp - oldHp);
            changed = true;
        }

        if (item.consumable)
        {
            state.RemoveItem(itemId, 1);
            changed = true;
        }

        if (changed)
        {
            RecalculateStats(false);
            RefreshUi();
        }

        ShowMessage(message + "。");
        return true;
    }

    public bool EquipWeapon(string itemId)
    {
        if (BattleManager.IsBattleOpen)
        {
            ShowMessage("战斗中暂时不能更换武器。");
            return false;
        }

        PlayerState state = GetState();
        if (state == null || string.IsNullOrEmpty(itemId)) return false;
        if (!state.HasItem(itemId))
        {
            ShowMessage("背包中没有该武器。");
            return false;
        }

        ItemData item = ItemDatabase.GetItem(itemId);
        if (item == null)
        {
            ShowMessage("找不到物品数据：" + itemId);
            return false;
        }

        if (item.type != "weapon")
        {
            ShowMessage(item.name + "不是武器。");
            return false;
        }

        state.equippedWeaponId = itemId;
        RecalculateStats(false);
        RefreshUi();
        ShowMessage("已装备：" + item.name);
        return true;
    }

    public void UnequipWeapon()
    {
        if (BattleManager.IsBattleOpen)
        {
            ShowMessage("战斗中暂时不能更换武器。");
            return;
        }

        PlayerState state = GetState();
        if (state == null) return;
        state.equippedWeaponId = "";
        RecalculateStats(false);
        RefreshUi();
        ShowMessage("已卸下武器。");
    }

    public ItemData GetEquippedWeapon()
    {
        PlayerState state = GetState();
        if (state == null || string.IsNullOrEmpty(state.equippedWeaponId)) return null;
        return ItemDatabase.GetItem(state.equippedWeaponId);
    }

    public int GetWeaponAttackBonus()
    {
        PlayerState state = GetState();
        return GetWeaponAttackBonus(state);
    }

    public static int GetWeaponAttackBonus(PlayerState state)
    {
        if (state == null || string.IsNullOrEmpty(state.equippedWeaponId)) return 0;
        if (!state.HasItem(state.equippedWeaponId)) return 0;
        ItemData item = ItemDatabase.GetItem(state.equippedWeaponId);
        if (item == null || item.type != "weapon") return 0;
        return item.attackBonus;
    }

    public static int GetWeaponDefenseBonus(PlayerState state)
    {
        if (state == null || string.IsNullOrEmpty(state.equippedWeaponId)) return 0;
        if (!state.HasItem(state.equippedWeaponId)) return 0;
        ItemData item = ItemDatabase.GetItem(state.equippedWeaponId);
        if (item == null || item.type != "weapon") return 0;
        return item.defenseBonus;
    }

    public static int GetWeaponMaxHpBonus(PlayerState state)
    {
        if (state == null || string.IsNullOrEmpty(state.equippedWeaponId)) return 0;
        if (!state.HasItem(state.equippedWeaponId)) return 0;
        ItemData item = ItemDatabase.GetItem(state.equippedWeaponId);
        if (item == null || item.type != "weapon") return 0;
        return item.maxHpBonus;
    }

    public void RecalculateStats(bool healToFull)
    {
        PlayerState state = GetState();
        if (state == null) return;
        if (realmManager == null) realmManager = GetComponent<RealmManager>();
        if (bodyRealmManager == null) bodyRealmManager = GetComponent<BodyRealmManager>();
        PlayerStatCalculator.RecalculateStats(state, realmManager, bodyRealmManager, healToFull);
    }

    public void RefreshUi()
    {
        PlayerState state = GetState();
        if (locationUIManager != null) locationUIManager.RefreshPlayerStatus(state);
        CharacterStatusUIManager statusUI = GetComponent<CharacterStatusUIManager>();
        if (statusUI != null) statusUI.RefreshIfOpen();
        InventoryUIManager inventoryUI = GetComponent<InventoryUIManager>();
        if (inventoryUI != null) inventoryUI.RefreshIfOpen();
    }

    private PlayerState GetState()
    {
        BindReferences();
        return gameManager != null ? gameManager.GetPlayerState() : null;
    }

    private void ShowMessage(string message)
    {
        if (locationUIManager == null) locationUIManager = GetComponent<LocationUIManager>();
        if (locationUIManager != null) locationUIManager.ShowMessage(message);
        else Debug.Log(message);
    }
}
