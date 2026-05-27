using System;
using System.Collections.Generic;

/// <summary>
/// 单个地图格子的数据。
/// 数据来自 Resources/Data/map_cells.json。
/// </summary>
[Serializable]
public class MapCellData
{
    public string id;
    public string name;
    public int x;
    public int y;
    public string description;
    public bool walkable;
    public string[] npcIds;
    public string[] actionIds;

    /// <summary>
    /// 首次进入该地点时触发的剧情事件 id。
    /// 为空时表示该地点没有首次进入事件。
    /// </summary>
    public string firstEnterEventId;

    /// <summary>是否锁定。锁定格子显示但不能进入，除非满足条件或已解锁。</summary>
    public bool locked;

    /// <summary>拥有该 flag 后可进入/显示。</summary>
    public string unlockFlag;

    /// <summary>拥有该物品后可进入/显示。</summary>
    public string unlockItem;

    /// <summary>拥有该功法后可进入/显示。</summary>
    public string unlockSkill;

    /// <summary>为 true 时，未满足解锁条件前不在地图上显示。</summary>
    public bool hiddenUntilUnlocked;
}

/// <summary>
/// JsonUtility 不能直接读取最外层数组，所以用这个包装类承接 JSON。
/// </summary>
[Serializable]
public class MapCellDataList
{
    public List<MapCellData> cells = new List<MapCellData>();
}
