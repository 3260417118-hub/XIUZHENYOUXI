using System;
using System.Collections.Generic;

/// <summary>
/// 单个对话选项数据。
/// action 支持：close、message、dialogue。
/// 并支持轻量条件显示。
/// </summary>
[Serializable]
public class DialogueOptionData
{
    public string text;
    public string action;
    public string target;
    public string message;

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
/// 单段对话数据。
/// 数据来自 Resources/Data/dialogues.json。
/// </summary>
[Serializable]
public class DialogueData
{
    public string id;
    public string speaker;
    public string text;
    public List<DialogueOptionData> options = new List<DialogueOptionData>();
}

/// <summary>
/// JsonUtility 不能直接读取最外层数组，所以用这个包装类承接 JSON。
/// </summary>
[Serializable]
public class DialogueDataList
{
    public List<DialogueData> dialogues = new List<DialogueData>();
}
