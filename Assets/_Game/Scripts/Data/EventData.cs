using System;
using System.Collections.Generic;

/// <summary>
/// 剧情事件选项数据。
/// 第一版支持：显示提示、设置 Flag、增加修为、增加/减少灵石、跳转下一个事件、关闭事件。
/// 并支持轻量条件显示。
/// </summary>
[Serializable]
public class EventOptionData
{
    public string text;
    public string message;
    public string[] setFlags;
    public int cultivationGain;
    public int spiritStoneGain;
    public string nextEventId;
    public bool closeEvent;

    public ConditionData condition;
    public string[] requireFlags;
    public string[] excludeFlags;
    public string[] requireItems;
    public string[] excludeItems;
    public string[] requireSkills;
    public string[] excludeSkills;
    public int minCultivation;
    public int minDay;
    public int maxDay;
}

/// <summary>
/// 单个剧情事件数据。
/// 数据来自 Resources/Data/events.json。
/// </summary>
[Serializable]
public class EventData
{
    public string id;
    public string title;
    public string text;
    public List<EventOptionData> options = new List<EventOptionData>();
}

/// <summary>
/// JsonUtility 不能直接读取最外层数组，所以用这个包装类承接 JSON。
/// </summary>
[Serializable]
public class EventDataList
{
    public List<EventData> events = new List<EventData>();
}
