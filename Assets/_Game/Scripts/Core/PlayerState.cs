using System;
using System.Collections.Generic;

[Serializable]
public class CounterRecord
{
    public string id;
    public int value;
}

/// <summary>
/// 玩家当前状态。
/// 这个类只保存数据，不负责复杂玩法逻辑，方便存档。
/// </summary>
[Serializable]
public class PlayerState
{
    public string playerName;

    public string currentCellId;
    public int currentX;
    public int currentY;

    /// <summary>当前所在地图区域。main 是主地图，black_forest 是黑风林专属地图。</summary>
    public string currentMapId;

    /// <summary>进入副地图前所在的主地图格子，用于返回。</summary>
    public string returnMainCellId;

    public int day;
    public int actionPoints;
    public int maxActionPoints;

    /// <summary>当前累计总修为。</summary>
    public int cultivation;

    /// <summary>当前修炼境界名称，例如：凡人、炼气一层。</summary>
    public string realm;

    /// <summary>当前修炼境界等级数字，用于判断突破。</summary>
    public int realmLevel;

    /// <summary>下一次修炼突破所需修为。顶部不显示，只做内部判断。</summary>
    public int maxCultivation;

    /// <summary>当前肉身锻体值。</summary>
    public int bodyCultivation;

    /// <summary>当前肉身境界名称，例如：凡体、锻体一层。</summary>
    public string bodyRealm;

    /// <summary>当前肉身境界等级数字，用于判断锻体突破。</summary>
    public int bodyRealmLevel;

    /// <summary>下一次肉身突破所需锻体值。</summary>
    public int maxBodyCultivation;

    public string equippedCultivationSkillId;
    public string equippedBodyMethodId;
    public string equippedSpellSkillId;

    public int spiritStones;

    public bool hasSeenOpening;

    /// <summary>
    /// 当前未解决的阻塞式剧情 NPC 事件。
    /// 为空时可以自由移动；不为空时会阻止移动并在当前地点显示事件 NPC。
    /// </summary>
    public string activeBlockingEncounterId;

    /// <summary>基础属性。最终属性由基础属性 + 修炼境界加成 + 锻体境界加成计算。</summary>
    public int baseMaxHp;
    public int baseAttack;
    public int baseDefense;

    /// <summary>最终战斗属性。BattleManager 直接读取这些值。</summary>
    public int hp;
    public int maxHp;
    public int attack;
    public int defense;

    public List<string> flags = new List<string>();
    public List<string> visitedCellIds = new List<string>();
    public List<string> dayEventsTriggered = new List<string>();

    /// <summary>轻量物品记录：只记 id，不做背包 UI。</summary>
    public List<string> items = new List<string>();

    /// <summary>轻量功法记录：只记 id，不做功法 UI。</summary>
    public List<string> learnedSkills = new List<string>();

    /// <summary>已解锁的隐藏/锁定地图格子。</summary>
    public List<string> unlockedCellIds = new List<string>();

    /// <summary>今日已经做过的一次性行为，休息过夜后清空。</summary>
    public List<string> dailyActionRecords = new List<string>();

    /// <summary>累计次数，例如上香次数、看书次数、杂役经验。</summary>
    public List<CounterRecord> counters = new List<CounterRecord>();

    /// <summary>当晚休息时触发的延迟事件。</summary>
    public List<string> pendingNightEvents = new List<string>();

    /// <summary>当前可休息地点。第一版固定为 ruined_hut。</summary>
    public string currentRestLocationId;

    /// <summary>
    /// 兼容旧存档：旧存档里没有列表或战斗/境界属性时，读取后要补默认值。
    /// </summary>
    public void EnsureLists()
    {
        if (flags == null) flags = new List<string>();
        if (visitedCellIds == null) visitedCellIds = new List<string>();
        if (dayEventsTriggered == null) dayEventsTriggered = new List<string>();
        if (items == null) items = new List<string>();
        if (learnedSkills == null) learnedSkills = new List<string>();
        if (unlockedCellIds == null) unlockedCellIds = new List<string>();
        if (dailyActionRecords == null) dailyActionRecords = new List<string>();
        if (counters == null) counters = new List<CounterRecord>();
        if (pendingNightEvents == null) pendingNightEvents = new List<string>();

        if (string.IsNullOrEmpty(playerName)) playerName = "林昊";
        if (string.IsNullOrEmpty(currentMapId)) currentMapId = "main";
        if (returnMainCellId == null) returnMainCellId = "";
        if (activeBlockingEncounterId == null) activeBlockingEncounterId = "";
        if (string.IsNullOrEmpty(currentRestLocationId)) currentRestLocationId = "ruined_hut";

        if (string.IsNullOrEmpty(realm)) realm = "凡人";
        if (realmLevel < 0) realmLevel = 0;
        if (maxCultivation <= 0) maxCultivation = 150;

        if (string.IsNullOrEmpty(bodyRealm)) bodyRealm = "凡体";
        if (bodyRealmLevel < 0) bodyRealmLevel = 0;
        if (maxBodyCultivation <= 0) maxBodyCultivation = 150;
        if (bodyCultivation < 0) bodyCultivation = 0;

        if (equippedCultivationSkillId == null) equippedCultivationSkillId = "";
        if (equippedBodyMethodId == null) equippedBodyMethodId = "";
        if (equippedSpellSkillId == null) equippedSpellSkillId = "";

        if (learnedSkills.Contains("skill_body_tempering_basic") && string.IsNullOrEmpty(equippedBodyMethodId))
        {
            equippedBodyMethodId = "skill_body_tempering_basic";
        }

        if (learnedSkills.Contains("skill_qi_training_basic") && string.IsNullOrEmpty(equippedCultivationSkillId))
        {
            equippedCultivationSkillId = "skill_qi_training_basic";
        }

        if (learnedSkills.Contains("spell_guiyuan_qigong") && string.IsNullOrEmpty(equippedSpellSkillId))
        {
            equippedSpellSkillId = "spell_guiyuan_qigong";
        }

        if (baseMaxHp <= 0) baseMaxHp = 50;
        if (baseAttack <= 0) baseAttack = 10;
        if (baseDefense < 0) baseDefense = 0;

        if (maxHp <= 0) maxHp = baseMaxHp;
        if (hp <= 0) hp = maxHp;
        if (attack <= 0) attack = baseAttack;
        if (defense < 0) defense = baseDefense;
        if (maxActionPoints <= 0) maxActionPoints = 3;
    }

    public bool HasFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag)) return false;
        EnsureLists();
        return flags.Contains(flag);
    }

    public void AddFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag)) return;
        EnsureLists();
        if (!flags.Contains(flag)) flags.Add(flag);
    }

    public void RemoveFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag)) return;
        EnsureLists();
        flags.Remove(flag);
    }

    public bool HasVisitedCell(string cellId)
    {
        if (string.IsNullOrEmpty(cellId)) return false;
        EnsureLists();
        return visitedCellIds.Contains(cellId);
    }

    public void AddVisitedCell(string cellId)
    {
        if (string.IsNullOrEmpty(cellId)) return;
        EnsureLists();
        if (!visitedCellIds.Contains(cellId)) visitedCellIds.Add(cellId);
    }

    public bool HasTriggeredDayEvent(string eventId)
    {
        if (string.IsNullOrEmpty(eventId)) return false;
        EnsureLists();
        return dayEventsTriggered.Contains(eventId);
    }

    public void AddTriggeredDayEvent(string eventId)
    {
        if (string.IsNullOrEmpty(eventId)) return;
        EnsureLists();
        if (!dayEventsTriggered.Contains(eventId)) dayEventsTriggered.Add(eventId);
    }

    public bool HasItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return false;
        EnsureLists();
        return items.Contains(itemId);
    }

    public void AddItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return;
        EnsureLists();
        if (!items.Contains(itemId)) items.Add(itemId);
    }

    public void RemoveItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return;
        EnsureLists();
        items.Remove(itemId);
    }

    public bool HasSkill(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return false;
        EnsureLists();
        return learnedSkills.Contains(skillId);
    }

    public void LearnSkill(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return;
        EnsureLists();
        if (!learnedSkills.Contains(skillId)) learnedSkills.Add(skillId);
        if (skillId == "skill_body_tempering_basic" && string.IsNullOrEmpty(equippedBodyMethodId)) equippedBodyMethodId = skillId;
        if (skillId == "skill_qi_training_basic" && string.IsNullOrEmpty(equippedCultivationSkillId)) equippedCultivationSkillId = skillId;
        if (skillId == "spell_guiyuan_qigong" && string.IsNullOrEmpty(equippedSpellSkillId)) equippedSpellSkillId = skillId;
    }

    public bool IsCellUnlocked(string cellId)
    {
        if (string.IsNullOrEmpty(cellId)) return false;
        EnsureLists();
        return unlockedCellIds.Contains(cellId);
    }

    public void UnlockCell(string cellId)
    {
        if (string.IsNullOrEmpty(cellId)) return;
        EnsureLists();
        if (!unlockedCellIds.Contains(cellId)) unlockedCellIds.Add(cellId);
    }

    public bool HasDoneToday(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        EnsureLists();
        return dailyActionRecords.Contains(key);
    }

    public void MarkDoneToday(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        EnsureLists();
        if (!dailyActionRecords.Contains(key)) dailyActionRecords.Add(key);
    }

    public int GetCounter(string id)
    {
        if (string.IsNullOrEmpty(id)) return 0;
        EnsureLists();
        foreach (CounterRecord record in counters)
        {
            if (record != null && record.id == id) return record.value;
        }
        return 0;
    }

    public void SetCounter(string id, int value)
    {
        if (string.IsNullOrEmpty(id)) return;
        EnsureLists();
        foreach (CounterRecord record in counters)
        {
            if (record != null && record.id == id)
            {
                record.value = value;
                return;
            }
        }
        counters.Add(new CounterRecord { id = id, value = value });
    }

    public void AddCounter(string id, int amount)
    {
        SetCounter(id, GetCounter(id) + amount);
    }

    public void AddPendingNightEvent(string eventId)
    {
        if (string.IsNullOrEmpty(eventId)) return;
        EnsureLists();
        if (!pendingNightEvents.Contains(eventId)) pendingNightEvents.Add(eventId);
    }
}
