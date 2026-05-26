using System;

/// <summary>
/// 玩家当前状态。
/// 这个类只保存数据，不负责具体玩法逻辑，方便以后做存档。
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
}
