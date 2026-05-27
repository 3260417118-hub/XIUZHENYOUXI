using UnityEngine;

/// <summary>
/// 兼容旧脚本接口，避免不同版本剧情管理器缺少入口方法导致编译中断。
/// 真正的第 13 / 21 天阻塞剧情仍由现有 BlockingEncounterManager / ChapterOneLateStoryFixManager 自身逻辑处理。
/// </summary>
public static class CompilerCompatibilityExtensions
{
    public static void CheckDayStartEncounter(this BlockingEncounterManager manager)
    {
        // 旧版本 BlockingEncounterManager 没有这个方法时使用空实现，避免编译失败。
    }

    public static void CheckDayStartBlockingEncounter(this ChapterOneLateStoryFixManager manager)
    {
        // 如果 ChapterOneLateStoryFixManager 本身已有同名实例方法，Unity 会优先调用原方法。
        // 这里只作为兼容兜底。
    }
}
