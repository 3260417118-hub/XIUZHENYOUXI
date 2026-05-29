using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class BattleWinRewardData
{
    public int spiritStones;
    public string[] items;
    public int cultivation;
    public int bodyCultivation;
}

[Serializable]
public class AdvancedBattleData
{
    public string id;
    public string title;
    public string enemyName;
    public int enemyHp;
    public int enemyAttack;
    public int enemyDefense;
    public string enemySkillName;
    public float enemySkillChance;
    public int enemySkillDamage;
    public string winMessage;
    public string loseMessage;
    public string[] winFlags;
    public string[] loseFlags;
    public BattleWinRewardData winRewards;
    public string losePenaltyMessage;
}

[Serializable]
public class AdvancedBattleDataList
{
    public List<AdvancedBattleData> battles = new List<AdvancedBattleData>();
}

/// <summary>
/// 第一章回合制文字战斗增强器。
/// 不重建 DemoScene，不删除旧 BattleManager；旧 BattleManager 仍负责被阻塞剧情调用，
/// 本组件在战斗开始后接管界面和回合逻辑。
/// </summary>
public class AdvancedBattleSystemManager : MonoBehaviour
{
    private readonly Dictionary<string, AdvancedBattleData> battleById = new Dictionary<string, AdvancedBattleData>();
    private readonly List<string> battleLogs = new List<string>();

    private BattleManager oldBattleManager;
    private GameManager gameManager;
    private LocationUIManager locationUIManager;
    private LocationActionManager locationActionManager;
    private InventoryManager inventoryManager;
    private SkillManager skillManager;

    private FieldInfo currentBattleField;
    private FieldInfo enemyHpField;
    private FieldInfo callbackField;
    private FieldInfo oldPanelField;
    private FieldInfo resultMessageField;

    private BattleData legacyBattle;
    private AdvancedBattleData currentBattle;
    private int enemyHp;
    private bool defending;
    private bool spellUsed;
    private bool battleEnded;
    private bool enhancedBattleActive;
    private Action finishCallback;
    private string finalMessage;

    private GameObject panelObject;
    private Text titleText;
    private Text statusText;
    private Text logText;
    private Button attackButton;
    private Button defendButton;
    private Button spellButton;
    private Button healButton;
    private Button finishButton;
    private Font cachedFont;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttach()
    {
        GameManager manager = UnityEngine.Object.FindObjectOfType<GameManager>();
        if (manager != null && manager.GetComponent<AdvancedBattleSystemManager>() == null)
        {
            manager.gameObject.AddComponent<AdvancedBattleSystemManager>();
        }
    }

    private void Start()
    {
        BindReferences();
        LoadBattleData();
    }

    private void Update()
    {
        BindReferences();
        if (oldBattleManager == null) return;

        BattleData detectedBattle = GetLegacyCurrentBattle();
        if (BattleManager.IsBattleOpen && detectedBattle != null)
        {
            if (!enhancedBattleActive || legacyBattle != detectedBattle)
            {
                BeginEnhancedBattle(detectedBattle);
            }
            HideLegacyBattlePanel();
        }
        else if (enhancedBattleActive)
        {
            HidePanel();
            enhancedBattleActive = false;
            legacyBattle = null;
            currentBattle = null;
        }
    }

    private void BindReferences()
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        if (locationUIManager == null) locationUIManager = GetComponent<LocationUIManager>();
        if (locationActionManager == null) locationActionManager = GetComponent<LocationActionManager>();
        if (inventoryManager == null) inventoryManager = GetComponent<InventoryManager>();
        if (skillManager == null) skillManager = GetComponent<SkillManager>();
        if (oldBattleManager == null) oldBattleManager = GetComponent<BattleManager>();
        CacheReflectionFields();
    }

    private void CacheReflectionFields()
    {
        if (currentBattleField != null || oldBattleManager == null) return;
        Type type = typeof(BattleManager);
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        currentBattleField = type.GetField("currentBattle", flags);
        enemyHpField = type.GetField("enemyHp", flags);
        callbackField = type.GetField("battleFinishedCallback", flags);
        oldPanelField = type.GetField("panelObject", flags);
        resultMessageField = type.GetField("lastBattleResultMessage", flags);
    }

    private void LoadBattleData()
    {
        battleById.Clear();
        TextAsset jsonAsset = Resources.Load<TextAsset>("Data/battles");
        if (jsonAsset == null) return;
        AdvancedBattleDataList list = JsonUtility.FromJson<AdvancedBattleDataList>(jsonAsset.text);
        if (list == null || list.battles == null) return;
        foreach (AdvancedBattleData data in list.battles)
        {
            if (data != null && !string.IsNullOrEmpty(data.id)) battleById[data.id] = data;
        }
    }

    private BattleData GetLegacyCurrentBattle()
    {
        if (oldBattleManager == null || currentBattleField == null) return null;
        return currentBattleField.GetValue(oldBattleManager) as BattleData;
    }

    private void BeginEnhancedBattle(BattleData detectedBattle)
    {
        LoadBattleData();
        legacyBattle = detectedBattle;
        battleById.TryGetValue(detectedBattle.id, out currentBattle);
        if (currentBattle == null)
        {
            currentBattle = new AdvancedBattleData
            {
                id = detectedBattle.id,
                title = detectedBattle.title,
                enemyName = detectedBattle.enemyName,
                enemyHp = detectedBattle.enemyHp,
                enemyAttack = detectedBattle.enemyAttack,
                enemyDefense = detectedBattle.enemyDefense,
                winMessage = detectedBattle.winMessage,
                loseMessage = detectedBattle.loseMessage,
                winFlags = detectedBattle.winFlags,
                loseFlags = detectedBattle.loseFlags
            };
        }

        enemyHp = currentBattle.enemyHp > 0 ? currentBattle.enemyHp : detectedBattle.enemyHp;
        defending = false;
        spellUsed = false;
        battleEnded = false;
        enhancedBattleActive = true;
        finalMessage = "";
        finishCallback = callbackField != null ? callbackField.GetValue(oldBattleManager) as Action : null;
        battleLogs.Clear();
        AddLog("战斗开始：" + currentBattle.enemyName + "出现了。");
        EnsurePanel();
        if (panelObject != null)
        {
            panelObject.SetActive(true);
            panelObject.transform.SetAsLastSibling();
        }
        HideLegacyBattlePanel();
        RefreshBattleUi();
    }

    private void HideLegacyBattlePanel()
    {
        if (oldBattleManager == null || oldPanelField == null) return;
        GameObject oldPanel = oldPanelField.GetValue(oldBattleManager) as GameObject;
        if (oldPanel != null && oldPanel != panelObject && oldPanel.activeSelf) oldPanel.SetActive(false);
    }

    public void PlayerNormalAttack()
    {
        if (!CanAct()) return;
        PlayerState state = GetState();
        int damage = Mathf.Max(1, state.attack - currentBattle.enemyDefense);
        enemyHp -= damage;

        string weaponName = GetEquippedWeaponName(state);
        if (!string.IsNullOrEmpty(weaponName)) AddLog("林昊挥动【" + weaponName + "】，击中【" + currentBattle.enemyName + "】，造成 " + damage + " 点伤害。");
        else AddLog("林昊挥拳攻向【" + currentBattle.enemyName + "】，造成 " + damage + " 点伤害。");

        if (!CheckBattleEnd()) EnemyTurn();
        RefreshBattleUi();
    }

    public void PlayerUseSpell()
    {
        if (!CanAct()) return;
        PlayerState state = GetState();
        if (state == null || string.IsNullOrEmpty(state.equippedSpellSkillId))
        {
            AddLog("你尚未装备可用的战斗法术。");
            RefreshBattleUi();
            return;
        }
        if (spellUsed)
        {
            AddLog("此法术本场战斗已经使用过。");
            RefreshBattleUi();
            return;
        }

        spellUsed = true;
        int damage = 25;
        enemyHp -= damage;
        string spellName = skillManager != null ? skillManager.GetSkillName(state.equippedSpellSkillId) : state.equippedSpellSkillId;
        AddLog("你运转《" + spellName + "》，一道气劲轰向【" + currentBattle.enemyName + "】，造成 " + damage + " 点伤害。");
        if (!CheckBattleEnd()) EnemyTurn();
        RefreshBattleUi();
    }

    public void PlayerDefend()
    {
        if (!CanAct()) return;
        defending = true;
        AddLog("林昊稳住身形，准备抵挡敌人的攻击。");
        EnemyTurn();
        RefreshBattleUi();
    }

    public void PlayerUseItem(string itemId)
    {
        if (!CanAct()) return;
        PlayerState state = GetState();
        if (state == null || string.IsNullOrEmpty(itemId)) return;
        if (itemId != "healing_pill")
        {
            AddLog("战斗中只能使用疗伤丹。修为丹等物品请在背包界面非战斗状态使用。");
            RefreshBattleUi();
            return;
        }
        if (!state.HasItem(itemId))
        {
            AddLog("你没有疗伤丹。 ");
            RefreshBattleUi();
            return;
        }

        int oldHp = state.hp;
        state.hp = Mathf.Clamp(state.hp + 30, 1, state.maxHp);
        state.RemoveItem(itemId, 1);
        int recovered = Mathf.Max(0, state.hp - oldHp);
        AddLog("林昊服下一枚疗伤丹，生命恢复 " + recovered + " 点。");
        RefreshOtherUi();
        EnemyTurn();
        RefreshBattleUi();
    }

    private void EnemyTurn()
    {
        if (battleEnded || currentBattle == null) return;
        PlayerState state = GetState();
        if (state == null) return;

        bool useSkill = !string.IsNullOrEmpty(currentBattle.enemySkillName) && currentBattle.enemySkillChance > 0f && UnityEngine.Random.value < currentBattle.enemySkillChance;
        int rawDamage = useSkill ? currentBattle.enemyAttack + currentBattle.enemySkillDamage - state.defense : currentBattle.enemyAttack - state.defense;
        int damage = Mathf.Max(1, rawDamage);
        if (defending)
        {
            damage = Mathf.Max(1, Mathf.CeilToInt(damage * 0.5f));
        }

        state.hp -= damage;
        if (state.hp < 0) state.hp = 0;
        if (useSkill) AddLog("【" + currentBattle.enemyName + "】施展【" + currentBattle.enemySkillName + "】，造成 " + damage + " 点伤害。");
        else AddLog("【" + currentBattle.enemyName + "】向你袭来，造成 " + damage + " 点伤害。");
        if (defending)
        {
            AddLog("你提前稳住身形，挡下了部分伤害。");
            defending = false;
        }

        CheckBattleEnd();
        RefreshOtherUi();
    }

    private bool CheckBattleEnd()
    {
        if (battleEnded) return true;
        PlayerState state = GetState();
        if (enemyHp <= 0)
        {
            EndBattleWin();
            return true;
        }
        if (state != null && state.hp <= 0)
        {
            EndBattleLose();
            return true;
        }
        return false;
    }

    private void EndBattleWin()
    {
        battleEnded = true;
        enemyHp = 0;
        ApplyFlags(currentBattle.winFlags);
        ApplyWinRewards();
        finalMessage = string.IsNullOrEmpty(currentBattle.winMessage) ? "你获得了胜利。" : currentBattle.winMessage;
        AddLog(currentBattle.enemyName + "倒下了。");
        AddLog(finalMessage);
        ShowFinishOnly();
        RefreshOtherUi();
    }

    private void EndBattleLose()
    {
        battleEnded = true;
        PlayerState state = GetState();
        if (state != null) state.hp = 1;
        ApplyFlags(currentBattle.loseFlags);
        string penalty = string.IsNullOrEmpty(currentBattle.losePenaltyMessage) ? "" : "\n" + currentBattle.losePenaltyMessage;
        finalMessage = (string.IsNullOrEmpty(currentBattle.loseMessage) ? "你败下阵来。" : currentBattle.loseMessage) + penalty;
        AddLog(finalMessage);
        ShowFinishOnly();
        RefreshOtherUi();
    }

    private void ApplyWinRewards()
    {
        if (currentBattle == null || currentBattle.winRewards == null) return;
        PlayerState state = GetState();
        if (state == null) return;

        if (currentBattle.winRewards.spiritStones != 0)
        {
            state.spiritStones += currentBattle.winRewards.spiritStones;
            AddLog("获得灵石：" + currentBattle.winRewards.spiritStones);
        }
        if (currentBattle.winRewards.items != null)
        {
            foreach (string itemId in currentBattle.winRewards.items)
            {
                if (string.IsNullOrEmpty(itemId)) continue;
                if (inventoryManager != null) inventoryManager.AddItem(itemId, 1);
                else state.AddItem(itemId, 1);
                AddLog("获得物品：" + InventoryItemDatabase.GetItemName(itemId));
            }
        }
        if (currentBattle.winRewards.cultivation != 0)
        {
            state.cultivation += currentBattle.winRewards.cultivation;
            AddLog("修为 +" + currentBattle.winRewards.cultivation);
        }
        if (currentBattle.winRewards.bodyCultivation != 0)
        {
            state.bodyCultivation += currentBattle.winRewards.bodyCultivation;
            AddLog("锻体值 +" + currentBattle.winRewards.bodyCultivation);
        }
    }

    private void FinishBattle()
    {
        Action callback = finishCallback;
        string message = finalMessage;
        HidePanel();
        enhancedBattleActive = false;
        legacyBattle = null;
        currentBattle = null;
        finishCallback = null;
        finalMessage = "";
        if (resultMessageField != null && oldBattleManager != null) resultMessageField.SetValue(oldBattleManager, message);
        if (oldBattleManager != null) oldBattleManager.CloseBattleSilently();
        if (callback != null) callback.Invoke();
        if (locationActionManager != null) locationActionManager.RefreshCurrentLocation();
        if (!string.IsNullOrEmpty(message) && locationUIManager != null) locationUIManager.ShowMessage(message);
    }

    private bool CanAct()
    {
        return enhancedBattleActive && !battleEnded && currentBattle != null && GetState() != null;
    }

    private PlayerState GetState()
    {
        return gameManager != null ? gameManager.GetPlayerState() : null;
    }

    private string GetEquippedWeaponName(PlayerState state)
    {
        if (state == null || string.IsNullOrEmpty(state.equippedWeaponId)) return "";
        if (!state.HasItem(state.equippedWeaponId)) return "";
        InventoryItemData item = InventoryItemDatabase.GetItem(state.equippedWeaponId);
        return item != null ? item.name : state.equippedWeaponId;
    }

    private void ApplyFlags(string[] flags)
    {
        PlayerState state = GetState();
        if (state == null || flags == null) return;
        foreach (string flag in flags) state.AddFlag(flag);
    }

    private void AddLog(string log)
    {
        if (string.IsNullOrEmpty(log)) return;
        battleLogs.Add(log);
        while (battleLogs.Count > 10) battleLogs.RemoveAt(0);
    }

    private void RefreshBattleUi()
    {
        if (currentBattle == null || titleText == null || statusText == null || logText == null) return;
        PlayerState state = GetState();
        if (state == null) return;
        if (enemyHpField != null && oldBattleManager != null) enemyHpField.SetValue(oldBattleManager, enemyHp);

        titleText.text = currentBattle.title;
        statusText.text = "敌人：" + currentBattle.enemyName + "\n敌人生命：" + Mathf.Max(0, enemyHp) + " / " + currentBattle.enemyHp + "\n玩家生命：" + state.hp + " / " + state.maxHp;
        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < battleLogs.Count; i++) builder.AppendLine(battleLogs[i]);
        logText.text = builder.ToString();

        bool hasSpell = !string.IsNullOrEmpty(state.equippedSpellSkillId);
        if (spellButton != null)
        {
            spellButton.gameObject.SetActive(hasSpell);
            spellButton.interactable = hasSpell && !spellUsed && !battleEnded;
            Text label = spellButton.GetComponentInChildren<Text>();
            if (label != null)
            {
                string spellName = hasSpell && skillManager != null ? skillManager.GetSkillName(state.equippedSpellSkillId) : "法术";
                label.text = spellUsed ? "法术已用" : "释放" + spellName;
            }
        }

        bool hasHeal = state.HasItem("healing_pill");
        if (healButton != null)
        {
            healButton.gameObject.SetActive(hasHeal && !battleEnded);
            healButton.interactable = hasHeal && !battleEnded;
            Text label = healButton.GetComponentInChildren<Text>();
            if (label != null) label.text = "疗伤丹(" + state.GetItemCount("healing_pill") + ")";
        }

        if (attackButton != null) attackButton.gameObject.SetActive(!battleEnded);
        if (defendButton != null) defendButton.gameObject.SetActive(!battleEnded);
        if (finishButton != null) finishButton.gameObject.SetActive(battleEnded);
    }

    private void RefreshOtherUi()
    {
        PlayerState state = GetState();
        if (locationUIManager != null) locationUIManager.RefreshPlayerStatus(state);
        CharacterStatusUIManager status = GetComponent<CharacterStatusUIManager>();
        if (status != null) status.RefreshIfOpen();
        InventoryUIManager inventoryUI = GetComponent<InventoryUIManager>();
        if (inventoryUI != null) inventoryUI.RefreshIfOpen();
    }

    private void ShowFinishOnly()
    {
        if (attackButton != null) attackButton.gameObject.SetActive(false);
        if (defendButton != null) defendButton.gameObject.SetActive(false);
        if (spellButton != null) spellButton.gameObject.SetActive(false);
        if (healButton != null) healButton.gameObject.SetActive(false);
        if (finishButton != null) finishButton.gameObject.SetActive(true);
    }

    private void EnsurePanel()
    {
        if (panelObject != null) return;
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;
        panelObject = new GameObject("AdvancedBattlePanel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        Image image = panelObject.GetComponent<Image>();
        image.color = new Color(0.03f, 0.02f, 0.02f, 0.94f);
        image.raycastTarget = true;

        Font font = GetDefaultFont();
        titleText = CreateText(panelObject.transform, "BattleTitle", font, 30, TextAnchor.MiddleCenter, new Vector2(0.15f, 0.80f), new Vector2(0.85f, 0.90f));
        statusText = CreateText(panelObject.transform, "BattleStatus", font, 22, TextAnchor.MiddleCenter, new Vector2(0.20f, 0.66f), new Vector2(0.80f, 0.79f));
        logText = CreateText(panelObject.transform, "BattleLog", font, 20, TextAnchor.UpperLeft, new Vector2(0.18f, 0.34f), new Vector2(0.82f, 0.64f));

        attackButton = CreateButton(panelObject.transform, "NormalAttackButton", "普通攻击", new Vector2(-270f, 0f));
        attackButton.onClick.AddListener(PlayerNormalAttack);
        defendButton = CreateButton(panelObject.transform, "DefendButton", "防御", new Vector2(-90f, 0f));
        defendButton.onClick.AddListener(PlayerDefend);
        spellButton = CreateButton(panelObject.transform, "SpellButton", "释放法术", new Vector2(90f, 0f));
        spellButton.onClick.AddListener(PlayerUseSpell);
        healButton = CreateButton(panelObject.transform, "HealButton", "疗伤丹", new Vector2(270f, 0f));
        healButton.onClick.AddListener(delegate { PlayerUseItem("healing_pill"); });
        finishButton = CreateButton(panelObject.transform, "FinishBattleButton", "结束战斗", new Vector2(0f, -72f));
        finishButton.onClick.AddListener(FinishBattle);
        finishButton.gameObject.SetActive(false);
        panelObject.SetActive(false);
    }

    private Text CreateText(Transform parent, string name, Font font, int size, TextAnchor align, Vector2 min, Vector2 max)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Text text = obj.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = align;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private Button CreateButton(Transform parent, string name, string text, Vector2 offset)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.25f);
        rect.anchorMax = new Vector2(0.5f, 0.25f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(160f, 46f);
        rect.anchoredPosition = offset;
        Image image = obj.GetComponent<Image>();
        image.color = new Color(0.28f, 0.15f, 0.15f, 1f);
        Button button = obj.GetComponent<Button>();
        button.targetGraphic = image;
        Text label = CreateText(obj.transform, "Text", GetDefaultFont(), 18, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
        label.text = text;
        return button;
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
