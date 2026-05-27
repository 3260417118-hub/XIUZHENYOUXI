using System;
using System.Collections.Generic;

/// <summary>
/// 条件显示数据。
/// 后续剧情、对话、地点行为都可以复用这套轻量条件。
/// </summary>
[Serializable]
public class ConditionData
{
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
/// 地点行为数据。
/// 例如闭关修炼、采集灵草、观察四周。
/// </summary>
[Serializable]
public class LocationActionData
{
    public string id;
    public string name;
    public string description;
    public int costActionPoint;
    public int cultivationGain;
    public string message;

    /// <summary>行为显示条件。为空时默认显示。</summary>
    public ConditionData condition;
}

/// <summary>
/// JsonUtility 读取行为列表时使用的包装类。
/// </summary>
[Serializable]
public class LocationActionDataList
{
    public List<LocationActionData> actions = new List<LocationActionData>();
}
