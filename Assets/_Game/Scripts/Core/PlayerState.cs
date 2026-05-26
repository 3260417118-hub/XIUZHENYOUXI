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

    /// <summary>
    /// 已获得的剧情标记。
    /// 例如：heard_mountain_bell、found_glowing_herb。
    /// </summary>
    public List<string> flags = new List<string>();

    /// <summary>
    /// 已触发过首次进入事件的地点 id。
    /// 用来避免同一个地点事件反复触发。
    /// </summary>
    public List<string> visitedCellIds = new List<string>();

    /// <summary>
    /// 兼容旧存档：旧存档里没有 flags / visitedCellIds 时，读取后要补上空列表。
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
}
