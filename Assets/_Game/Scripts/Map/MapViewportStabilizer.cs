using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 地图视窗稳定器。
/// 作用：修正大地图移动时，格子因为视窗中心重算而突然跳动的问题。
/// 规则：
/// 1. 当前视窗能装下可见格子时，格子位置保持不动。
/// 2. 只有可见格子快溢出地图框时，才移动视窗。
/// 3. 回到村口附近时，村口 (0,0) 自动回到地图中心。
/// </summary>
public class MapViewportStabilizer : MonoBehaviour
{
    private MapGridManager mapGridManager;
    private RectTransform mapPanel;
    private readonly Dictionary<string, MapCellData> cellById = new Dictionary<string, MapCellData>();

    private int viewportCenterX;
    private int viewportCenterY;
    private bool hasViewportCenter;

    private Vector2 cellSize = new Vector2(92f, 58f);
    private Vector2 cellStep = new Vector2(108f, 70f);
    private Vector2 edgePadding = new Vector2(18f, 18f);
    private int visibleRange = 3;
    private float connectionLineThickness = 4f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindObjectOfType<MapViewportStabilizer>() != null)
        {
            return;
        }

        GameObject fixerObject = new GameObject("MapViewportStabilizer");
        fixerObject.AddComponent<MapViewportStabilizer>();
    }

    private void LateUpdate()
    {
        if (!EnsureReferences())
        {
            return;
        }

        PlayerState playerState = GameManager.Instance != null ? GameManager.Instance.GetPlayerState() : null;
        if (playerState == null)
        {
            return;
        }

        List<MapCellData> visibleCells = GetVisibleCells(playerState);
        if (visibleCells.Count == 0)
        {
            return;
        }

        UpdateViewportCenter(visibleCells, playerState);
        RepositionVisibleMapObjects(visibleCells);
    }

    private bool EnsureReferences()
    {
        if (mapGridManager == null)
        {
            mapGridManager = FindObjectOfType<MapGridManager>();
            hasViewportCenter = false;
        }

        if (mapGridManager == null)
        {
            return false;
        }

        mapPanel = GetPrivateField<RectTransform>(mapGridManager, "mapPanel");
        if (mapPanel == null)
        {
            return false;
        }

        visibleRange = Mathf.Max(3, GetPrivateField<int>(mapGridManager, "visibleRange"));
        cellSize = GetPrivateField<Vector2>(mapGridManager, "cellSize");
        cellStep = GetPrivateField<Vector2>(mapGridManager, "cellStep");
        edgePadding = GetPrivateField<Vector2>(mapGridManager, "edgePadding");
        connectionLineThickness = GetPrivateField<float>(mapGridManager, "connectionLineThickness");

        if (cellSize.x <= 1f || cellSize.y <= 1f)
        {
            cellSize = new Vector2(92f, 58f);
        }

        if (cellStep.x <= 1f || cellStep.y <= 1f)
        {
            cellStep = new Vector2(108f, 70f);
        }

        RebuildCellCache();
        return cellById.Count > 0;
    }

    private void RebuildCellCache()
    {
        List<MapCellData> allCells = GetPrivateField<List<MapCellData>>(mapGridManager, "allCells");
        if (allCells == null || allCells.Count == cellById.Count)
        {
            return;
        }

        cellById.Clear();
        foreach (MapCellData cell in allCells)
        {
            if (cell == null || string.IsNullOrEmpty(cell.id))
            {
                continue;
            }

            cellById[cell.id] = cell;
        }
    }

    private List<MapCellData> GetVisibleCells(PlayerState playerState)
    {
        List<MapCellData> visibleCells = new List<MapCellData>();
        foreach (MapCellData cell in cellById.Values)
        {
            if (MapRuleUtility.IsInVisibleRange(playerState.currentX, playerState.currentY, cell.x, cell.y, visibleRange))
            {
                visibleCells.Add(cell);
            }
        }

        return visibleCells;
    }

    private void UpdateViewportCenter(List<MapCellData> visibleCells, PlayerState playerState)
    {
        MapBounds bounds = CalculateBounds(visibleCells);
        float maxOffsetX = Mathf.Max(cellStep.x, mapPanel.rect.width * 0.5f - cellSize.x * 0.5f - edgePadding.x);
        float maxOffsetY = Mathf.Max(cellStep.y, mapPanel.rect.height * 0.5f - cellSize.y * 0.5f - edgePadding.y);

        if (!hasViewportCenter)
        {
            viewportCenterX = 0;
            viewportCenterY = 0;
            hasViewportCenter = true;
        }

        if (ContainsVisibleCellAt(visibleCells, 0, 0) && BoundsFitWithCenter(bounds, 0, 0, maxOffsetX, maxOffsetY))
        {
            viewportCenterX = 0;
            viewportCenterY = 0;
            return;
        }

        if (BoundsFitWithCenter(bounds, viewportCenterX, viewportCenterY, maxOffsetX, maxOffsetY))
        {
            return;
        }

        viewportCenterX = ClampViewportCenter(playerState.currentX, bounds.minX, bounds.maxX, maxOffsetX, cellStep.x);
        viewportCenterY = ClampViewportCenter(playerState.currentY, bounds.minY, bounds.maxY, maxOffsetY, cellStep.y);
    }

    private void RepositionVisibleMapObjects(List<MapCellData> visibleCells)
    {
        foreach (MapCellData cell in visibleCells)
        {
            Transform buttonTransform = mapPanel.Find(cell.id + "_Button");
            if (buttonTransform == null)
            {
                continue;
            }

            RectTransform rect = buttonTransform.GetComponent<RectTransform>();
            if (rect == null)
            {
                continue;
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = cellSize;
            rect.anchoredPosition = GetCellCenterPosition(cell);
        }

        RepositionLines();
    }

    private void RepositionLines()
    {
        foreach (Transform child in mapPanel)
        {
            if (!child.name.EndsWith("_Line"))
            {
                continue;
            }

            string lineName = child.name.Substring(0, child.name.Length - "_Line".Length);
            int splitIndex = lineName.IndexOf("_to_");
            if (splitIndex < 0)
            {
                continue;
            }

            string firstId = lineName.Substring(0, splitIndex);
            string secondId = lineName.Substring(splitIndex + "_to_".Length);

            MapCellData first;
            MapCellData second;
            if (!cellById.TryGetValue(firstId, out first) || !cellById.TryGetValue(secondId, out second))
            {
                continue;
            }

            RectTransform rect = child.GetComponent<RectTransform>();
            Image image = child.GetComponent<Image>();
            if (rect == null)
            {
                continue;
            }

            if (image != null)
            {
                image.raycastTarget = false;
            }

            Vector2 firstCenter = GetCellCenterPosition(first);
            Vector2 secondCenter = GetCellCenterPosition(second);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = (firstCenter + secondCenter) * 0.5f;
            rect.sizeDelta = first.y == second.y
                ? new Vector2(cellStep.x, connectionLineThickness)
                : new Vector2(connectionLineThickness, cellStep.y);
        }
    }

    private Vector2 GetCellCenterPosition(MapCellData cell)
    {
        return new Vector2((cell.x - viewportCenterX) * cellStep.x, (cell.y - viewportCenterY) * cellStep.y);
    }

    private MapBounds CalculateBounds(List<MapCellData> cells)
    {
        MapBounds bounds = new MapBounds();
        bounds.minX = cells[0].x;
        bounds.maxX = cells[0].x;
        bounds.minY = cells[0].y;
        bounds.maxY = cells[0].y;

        foreach (MapCellData cell in cells)
        {
            bounds.minX = Mathf.Min(bounds.minX, cell.x);
            bounds.maxX = Mathf.Max(bounds.maxX, cell.x);
            bounds.minY = Mathf.Min(bounds.minY, cell.y);
            bounds.maxY = Mathf.Max(bounds.maxY, cell.y);
        }

        return bounds;
    }

    private bool ContainsVisibleCellAt(List<MapCellData> visibleCells, int x, int y)
    {
        foreach (MapCellData cell in visibleCells)
        {
            if (cell.x == x && cell.y == y)
            {
                return true;
            }
        }

        return false;
    }

    private bool BoundsFitWithCenter(MapBounds bounds, int centerX, int centerY, float maxOffsetX, float maxOffsetY)
    {
        float leftOffset = Mathf.Abs((bounds.minX - centerX) * cellStep.x);
        float rightOffset = Mathf.Abs((bounds.maxX - centerX) * cellStep.x);
        float bottomOffset = Mathf.Abs((bounds.minY - centerY) * cellStep.y);
        float topOffset = Mathf.Abs((bounds.maxY - centerY) * cellStep.y);

        return leftOffset <= maxOffsetX && rightOffset <= maxOffsetX && bottomOffset <= maxOffsetY && topOffset <= maxOffsetY;
    }

    private int ClampViewportCenter(int desiredCenter, int minCell, int maxCell, float maxOffset, float step)
    {
        float cellsThatFitEachSide = maxOffset / Mathf.Max(1f, step);
        int lowerLimit = Mathf.CeilToInt(maxCell - cellsThatFitEachSide);
        int upperLimit = Mathf.FloorToInt(minCell + cellsThatFitEachSide);

        if (lowerLimit > upperLimit)
        {
            return Mathf.RoundToInt((minCell + maxCell) * 0.5f);
        }

        return Mathf.Clamp(desiredCenter, lowerLimit, upperLimit);
    }

    private T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo fieldInfo = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (fieldInfo == null)
        {
            return default(T);
        }

        object value = fieldInfo.GetValue(target);
        if (value is T)
        {
            return (T)value;
        }

        return default(T);
    }

    private struct MapBounds
    {
        public int minX;
        public int maxX;
        public int minY;
        public int maxY;
    }
}
