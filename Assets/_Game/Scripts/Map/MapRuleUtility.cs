using UnityEngine;

/// <summary>
/// 地图规则工具类。
/// 放纯计算逻辑，方便测试，也方便新手阅读地图规则。
/// </summary>
public static class MapRuleUtility
{
    public static bool IsOrthogonalNeighbor(int fromX, int fromY, int toX, int toY)
    {
        int dx = Mathf.Abs(toX - fromX);
        int dy = Mathf.Abs(toY - fromY);
        return dx + dy == 1;
    }

    public static bool IsInVisibleRange(int currentX, int currentY, int targetX, int targetY, int visibleRange)
    {
        int dx = Mathf.Abs(targetX - currentX);
        int dy = Mathf.Abs(targetY - currentY);
        return dx <= visibleRange && dy <= visibleRange;
    }

    public static bool ShouldConnectCells(MapCellData first, MapCellData second)
    {
        if (first == null || second == null)
        {
            return false;
        }

        if (!first.walkable || !second.walkable)
        {
            return false;
        }

        return IsOrthogonalNeighbor(first.x, first.y, second.x, second.y);
    }

    public static Vector2 GetCellAnchoredPosition(
        int cellX,
        int cellY,
        int minX,
        int maxY,
        Vector2 cellSpacing,
        Vector2 mapPadding)
    {
        int column = cellX - minX;
        int row = maxY - cellY;

        return new Vector2(
            mapPadding.x + column * cellSpacing.x,
            -mapPadding.y - row * cellSpacing.y);
    }
}
