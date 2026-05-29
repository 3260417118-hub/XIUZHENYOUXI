using UnityEngine;

/// <summary>
/// 兼容旧脚本接口。
/// RestManager 在休息过夜后会调用 CheckDayStartEncounter；这里必须转发到真实的 CheckTodayEncounter，
/// 否则第 4 天小混混、第 7 天恶霸帮手这类按日期触发的阻塞事件不会出现。
/// </summary>
public static class CompilerCompatibilityExtensions
{
    public static void CheckDayStartEncounter(this BlockingEncounterManager manager)
    {
        if (manager != null)
        {
            manager.CheckTodayEncounter();
        }
    }

    public static void CheckDayStartBlockingEncounter(this ChapterOneLateStoryFixManager manager)
    {
        // 第 13 天村口争斗、第 21 天赵霸天由 ChapterOneLateStoryFixManager.Update()
        // 按“日期 + 当前格子/当前状态”持续检查，这里不需要额外处理。
    }
}
