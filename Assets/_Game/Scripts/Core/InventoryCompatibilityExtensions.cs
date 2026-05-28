/// <summary>
/// 兼容 SaveManager 中的 RecalculateStats 调用，同时保持 InventoryManager 内部统一使用 RecalculateStatsWithEquipment。
/// </summary>
public static class InventoryCompatibilityExtensions
{
    public static void RecalculateStats(this InventoryManager inventoryManager, bool healToFull)
    {
        if (inventoryManager != null)
        {
            inventoryManager.RecalculateStatsWithEquipment(healToFull);
        }
    }
}
