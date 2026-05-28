using UnityEngine;
using UnityEngine.UI;

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
    private Text statusWeaponOverlayText;

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

    private void LateUpdate()
    {
        // 其他系统突破、读档或刷新时会调用 PlayerStatCalculator。
        // 这里用轻量同步保证装备加成不会被后续重算覆盖，也不会重复叠加。
        PlayerState state = GetState();
        if (state != null && !string.IsNullOrEmpty(state.equippedWeaponId))
        {
            int expectedAttack = CalculateExpectedAttackWithEquipment(state);
            if (state.attack != expectedAttack) RecalculateStats(false);
        }
        UpdateCharacterStatusWeaponLine();
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

        int oldMaxHp = state.maxHp > 0 ? state.maxHp : state.baseMaxHp;
        int oldHp = state.hp > 0 ? state.hp : oldMaxHp;

        PlayerStatCalculator.RecalculateStats(state, realmManager, bodyRealmManager, healToFull);

        int maxHpBonus = GetWeaponMaxHpBonus(state);
        int defenseBonus = GetWeaponDefenseBonus(state);
        int attackBonus = GetWeaponAttackBonus(state);

        if (maxHpBonus != 0)
        {
            state.maxHp = Mathf.Max(1, state.maxHp + maxHpBonus);
            state.hp = healToFull ? state.maxHp : Mathf.Clamp(oldHp, 1, state.maxHp);
        }
        if (defenseBonus != 0) state.defense = Mathf.Max(0, state.defense + defenseBonus);
        if (attackBonus != 0) state.attack = Mathf.Max(1, state.attack + attackBonus);
    }

    private int CalculateExpectedAttackWithEquipment(PlayerState state)
    {
        if (state == null) return 0;
        int attack = state.baseAttack;
        if (realmManager == null) realmManager = GetComponent<RealmManager>();
        if (bodyRealmManager == null) bodyRealmManager = GetComponent<BodyRealmManager>();
        RealmData currentRealm = realmManager != null ? realmManager.GetCurrentRealm() : null;
        if (currentRealm != null) attack += currentRealm.attackBonus;
        BodyRealmData currentBodyRealm = bodyRealmManager != null ? bodyRealmManager.GetCurrentBodyRealm() : null;
        if (currentBodyRealm != null) attack += currentBodyRealm.attackBonus;
        attack += GetWeaponAttackBonus(state);
        return Mathf.Max(1, attack);
    }

    public void RefreshUi()
    {
        PlayerState state = GetState();
        if (locationUIManager != null) locationUIManager.RefreshPlayerStatus(state);
        CharacterStatusUIManager statusUI = GetComponent<CharacterStatusUIManager>();
        if (statusUI != null) statusUI.RefreshIfOpen();
        InventoryUIManager inventoryUI = GetComponent<InventoryUIManager>();
        if (inventoryUI != null) inventoryUI.RefreshIfOpen();
        UpdateCharacterStatusWeaponLine();
    }

    private void UpdateCharacterStatusWeaponLine()
    {
        GameObject panel = GameObject.Find("CharacterStatusPanel");
        if (panel == null || !panel.activeInHierarchy)
        {
            statusWeaponOverlayText = null;
            return;
        }

        if (statusWeaponOverlayText == null)
        {
            Transform existing = panel.transform.Find("EquippedWeaponLine");
            if (existing != null) statusWeaponOverlayText = existing.GetComponent<Text>();
        }

        if (statusWeaponOverlayText == null)
        {
            GameObject textObject = new GameObject("EquippedWeaponLine", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(panel.transform, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(44f, -146f);
            rect.offsetMax = new Vector2(-44f, -116f);
            statusWeaponOverlayText = textObject.GetComponent<Text>();
            statusWeaponOverlayText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Font font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 16);
            if (font != null) statusWeaponOverlayText.font = font;
            statusWeaponOverlayText.fontSize = 18;
            statusWeaponOverlayText.alignment = TextAnchor.UpperLeft;
            statusWeaponOverlayText.color = new Color(1f, 0.92f, 0.65f, 1f);
            statusWeaponOverlayText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusWeaponOverlayText.verticalOverflow = VerticalWrapMode.Overflow;
            textObject.transform.SetAsLastSibling();
        }

        PlayerState state = GetState();
        string weaponName = "无";
        if (state != null && !string.IsNullOrEmpty(state.equippedWeaponId))
        {
            ItemData item = ItemDatabase.GetItem(state.equippedWeaponId);
            if (item != null) weaponName = item.name;
        }
        statusWeaponOverlayText.text = "武器：" + weaponName;
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
