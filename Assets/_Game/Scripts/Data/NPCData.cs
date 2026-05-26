using System;
using System.Collections.Generic;

/// <summary>
/// NPC 基础数据。
/// 数据来自 Resources/Data/npcs.json。
/// </summary>
[Serializable]
public class NPCData
{
    public string id;
    public string name;
    public string description;
    public string dialogueId;
}

/// <summary>
/// JsonUtility 不能直接读取最外层数组，所以用这个包装类承接 JSON。
/// </summary>
[Serializable]
public class NPCDataList
{
    public List<NPCData> npcs = new List<NPCData>();
}
