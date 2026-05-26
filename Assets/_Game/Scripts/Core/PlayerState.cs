using System;
using System.Collections.Generic;

/// <summary>
/// 玩家当前状态。
/// 这个类只保存数据，不负责复杂玩法逻辑，方便存档。
/// </summary>
[Serializable]
public class PlayerState
{
    public string currentCellId;
    public int currentX;
    public int currentY;

    public int day;
    public int actionPoints;
    public int maxActionPoints;

    public int cultivation;
    public string realm;
    public int spiritStones;

    public bool hasSeenOpening;
    public int hp;
    public int maxHp;
    public int attack;
    public int defense;

    public List<string> flags = new List<string>();
    public List<string> visitedCellIds = new List<string>();
    public List<string> dayEventsTriggered = new List<string>();

    /// <summary>
    /// 兼容旧存档：旧存档里没有列表或战斗属性时，读取后要补默认值。
    /// </summary>
    public void EnsureLists()
    {
        if (flags == null)
        {
            flags = new List<string>();
        }

        if (visitedCellIds == null)
        {
            visitedCellIds = new List<string>();
        }

        if (dayEventsTriggered == null)
        {
            dayEventsTriggered = new List<string>();
        }

        if (maxHp <= 0)
        {
            maxHp = 100;
        }

        if (hp <= 0)
        {
            hp = maxHp;
        }

        if (attack <= 0)
        {
            attack = 15;
        }

        if (defense <= 0)
        {
            defense = 3;
        }
    }

    public bool HasFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag))
        {
            return false;
        }

        EnsureLists();
        return flags.Contains(flag);
    }

    public void AddFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag))
        {
            return;
        }

        EnsureLists();
        if (!flags.Contains(flag))
        {
            flags.Add(flag);
        }
    }

    public void RemoveFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag))
        {
            return;
        }

        EnsureLists();
        flags.Remove(flag);
    }

    public bool HasVisitedCell(string cellId)
    {
        if (string.IsNullOrEmpty(cellId))
        {
            return false;
        }

        EnsureLists();
        return visitedCellIds.Contains(cellId);
    }

    public void AddVisitedCell(string cellId)
    {
        if (string.IsNullOrEmpty(cellId))
        {
            return;
        }

        EnsureLists();
        if (!visitedCellIds.Contains(cellId))
        {
            visitedCellIds.Add(cellId);
        }
    }

    public bool HasTriggeredDayEvent(string eventId)
    {
        if (string.IsNullOrEmpty(eventId))
        {
            return false;
        }

        EnsureLists();
        return dayEventsTriggered.Contains(eventId);
    }

    public void AddTriggeredDayEvent(string eventId)
    {
        if (string.IsNullOrEmpty(eventId))
        {
            return;
        }

        EnsureLists();
        if (!dayEventsTriggered.Contains(eventId))
        {
            dayEventsTriggered.Add(eventId);
        }
    }
}
