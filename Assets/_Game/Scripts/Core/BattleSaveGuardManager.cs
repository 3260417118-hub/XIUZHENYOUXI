using UnityEngine;

/// <summary>
/// 战斗存档保护占位管理器。
/// GameManager 会自动挂载它；SaveManager 内部会根据 BattleManager.IsBattleOpen 拒绝战斗中保存。
/// 这个组件保留为独立入口，避免缺少类型导致编译失败。
/// </summary>
public class BattleSaveGuardManager : MonoBehaviour
{
}
