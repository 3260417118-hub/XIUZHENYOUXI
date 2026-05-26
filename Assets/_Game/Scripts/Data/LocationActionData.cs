using System;
using System.Collections.Generic;

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
}

/// <summary>
/// JsonUtility 读取行为列表时使用的包装类。
/// </summary>
[Serializable]
public class LocationActionDataList
{
    public List<LocationActionData> actions = new List<LocationActionData>();
}
