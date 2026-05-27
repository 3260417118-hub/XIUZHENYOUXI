using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 负责加载地图 JSON，并在 MapPanel 里按 x,y 坐标生成格子按钮。
/// 第一章地图使用“世界坐标固定显示”：村口 (0,0) 默认显示在地图面板中心。
/// </summary>
public class MapGridManager : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private PlayerMapController playerMapController;
    [SerializeField] private LocationUIManager locationUIManager;
    [SerializeField] private LocationActionManager locationActionManager;
    [SerializeField] private RectTransform mapPanel;

    [SerializeField] private string mapDataResourcePath = "Data/map_cells";
    [SerializeField] private int visibleRange = 3;
    [SerializeField] private Vector2 cellSize = new Vector2(92f, 58f);
    [SerializeField] private Vector2 cellStep = new Vector2(108f, 70f);
    [SerializeField] private float connectionLineThickness = 4f;
    [SerializeField] private Vector2 edgePadding = new Vector2(18f, 18f);

    private readonly Dictionary<string, MapCellData> cellById = new Dictionary<string, MapCellData>();
    private readonly List<MapCellData> allCells = new List<MapCellData>();
    private readonly List<GameObject> createdMapObjects = new List<GameObject>();
    private Font cachedFont;

    private int viewportCenterX;
    private int viewportCenterY;

    public void SetReferences(
        GameManager game,
        PlayerMapController mapController,
        LocationUIManager locationUI,
        LocationActionManager actionManager,
        RectTransform panel)
    {
        gameManager = game;
        playerMapController = mapController;
        locationUIManager = locationUI;
        locationActionManager = actionManager;
        mapPanel = panel;
    }

    public void SetLayoutSettings(Vector2 newCellSize, Vector2 newCellStep, Vector2 newMapPadding)
    {
        cellSize = newCellSize;
        cellStep = newCellStep;
        edgePadding = newMapPadding;
        viewportCenterX = 0;
        viewportCenterY = 0;
    }

    private void Start()
    {
        ApplyChapterOneMapLayoutDefaults();
        LoadMapCells();
        EnsurePlayerStartsOnValidCell();
        RefreshMap();

        if (locationUIManager != null)
        {
            locationUIManager.RefreshLocation(GetCurrentCell(), gameManager.GetPlayerState());
            locationUIManager.ShowMessage("点击相邻格子开始移动。");
        }

        if (locationActionManager != null)
        {
            locationActionManager.RefreshCurrentLocation();
        }
    }

    private void ApplyChapterOneMapLayoutDefaults()
    {
        visibleRange = Mathf.Max(visibleRange, 3);
        cellSize = new Vector2(92f, 58f);
        cellStep = new Vector2(108f, 70f);
        edgePadding = new Vector2(18f, 18f);
    }

    public void LoadMapCells()
    {
        cellById.Clear();
        allCells.Clear();
        viewportCenterX = 0;
        viewportCenterY = 0;

        TextAsset jsonAsset = Resources.Load<TextAsset>(mapDataResourcePath);
        if (jsonAsset == null)
        {
            Debug.LogError("找不到地图数据：Resources/" + mapDataResourcePath + ".json");
            return;
        }

        MapCellDataList dataList = JsonUtility.FromJson<MapCellDataList>(jsonAsset.text);
        if (dataList == null || dataList.cells == null)
        {
            Debug.LogError("地图数据格式不正确：" + mapDataResourcePath);
            return;
        }

        foreach (MapCellData cell in dataList.cells)
        {
            if (cell == null || string.IsNullOrEmpty(cell.id)) continue;
            allCells.Add(cell);
            cellById[cell.id] = cell;
        }
    }

    public MapCellData GetCellById(string cellId)
    {
        if (string.IsNullOrEmpty(cellId)) return null;
        MapCellData cell;
        cellById.TryGetValue(cellId, out cell);
        return cell;
    }

    public MapCellData GetCurrentCell()
    {
        if (gameManager == null) return null;
        return GetCellById(gameManager.GetPlayerState().currentCellId);
    }

    public void SyncPlayerPositionToCurrentCell()
    {
        if (gameManager == null) return;
        PlayerState playerState = gameManager.GetPlayerState();
        if (playerState == null) return;
        MapCellData currentCell = GetCellById(playerState.currentCellId);
        if (currentCell == null) return;
        playerState.currentX = currentCell.x;
        playerState.currentY = currentCell.y;
    }

    public void RefreshMap()
    {
        ApplyChapterOneMapLayoutDefaults();
        ClearCreatedButtons();

        if (mapPanel == null || gameManager == null) return;
        PrepareMapPanelForManualLayout();

        PlayerState playerState = gameManager.GetPlayerState();
        List<MapCellData> visibleCells = new List<MapCellData>();
        foreach (MapCellData cell in allCells)
        {
            if (!ShouldDisplayCell(cell, playerState)) continue;
            if (IsInVisibleRange(cell, playerState)) visibleCells.Add(cell);
        }

        UpdateViewportCenterForVisibleCells(visibleCells);
        visibleCells.Sort(CompareCellsForDisplay);
        CreateConnectionLines(visibleCells);

        foreach (MapCellData cell in visibleCells)
        {
            CreateCellButton(cell, playerState);
        }
    }

    private bool ShouldDisplayCell(MapCellData cell, PlayerState playerState)
    {
        if (cell == null || playerState == null) return false;
        playerState.EnsureLists();
        if (!cell.hiddenUntilUnlocked) return true;
        return IsUnlockedByState(cell, playerState);
    }

    private bool IsUnlockedByState(MapCellData cell, PlayerState playerState)
    {
        if (cell == null || playerState == null) return false;
        if (playerState.IsCellUnlocked(cell.id)) return true;
        if (!string.IsNullOrEmpty(cell.unlockFlag) && playerState.HasFlag(cell.unlockFlag)) return true;
        if (!string.IsNullOrEmpty(cell.unlockItem) && playerState.HasItem(cell.unlockItem)) return true;
        if (!string.IsNullOrEmpty(cell.unlockSkill) && playerState.HasSkill(cell.unlockSkill)) return true;
        return false;
    }

    private void EnsurePlayerStartsOnValidCell()
    {
        if (gameManager == null || allCells.Count == 0) return;
        PlayerState playerState = gameManager.GetPlayerState();
        MapCellData currentCell = GetCellById(playerState.currentCellId);
        if (currentCell != null)
        {
            playerState.currentX = currentCell.x;
            playerState.currentY = currentCell.y;
            return;
        }

        MapCellData startCell = FindCellAt(0, 0);
        if (startCell == null) startCell = allCells[0];
        playerState.currentCellId = startCell.id;
        playerState.currentX = startCell.x;
        playerState.currentY = startCell.y;
    }

    private MapCellData FindCellAt(int x, int y)
    {
        foreach (MapCellData cell in allCells)
        {
            if (cell.x == x && cell.y == y) return cell;
        }
        return null;
    }

    private bool IsInVisibleRange(MapCellData cell, PlayerState playerState)
    {
        return MapRuleUtility.IsInVisibleRange(playerState.currentX, playerState.currentY, cell.x, cell.y, visibleRange);
    }

    private int CompareCellsForDisplay(MapCellData left, MapCellData right)
    {
        int yCompare = right.y.CompareTo(left.y);
        if (yCompare != 0) return yCompare;
        return left.x.CompareTo(right.x);
    }

    private MapBounds CalculateBounds(List<MapCellData> cells)
    {
        MapBounds bounds = new MapBounds();
        if (cells == null || cells.Count == 0) return bounds;
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

    private void UpdateViewportCenterForVisibleCells(List<MapCellData> visibleCells)
    {
        if (visibleCells == null || visibleCells.Count == 0 || mapPanel == null) return;
        MapBounds bounds = CalculateBounds(visibleCells);
        float maxOffsetX = Mathf.Max(cellStep.x, mapPanel.rect.width * 0.5f - cellSize.x * 0.5f - edgePadding.x);
        float maxOffsetY = Mathf.Max(cellStep.y, mapPanel.rect.height * 0.5f - cellSize.y * 0.5f - edgePadding.y);
        viewportCenterX = ClampViewportCenter(0, bounds.minX, bounds.maxX, maxOffsetX, cellStep.x);
        viewportCenterY = ClampViewportCenter(0, bounds.minY, bounds.maxY, maxOffsetY, cellStep.y);
    }

    private int ClampViewportCenter(int desiredCenter, int minCell, int maxCell, float maxOffset, float step)
    {
        float cellsThatFitEachSide = maxOffset / Mathf.Max(1f, step);
        int lowerLimit = Mathf.CeilToInt(maxCell - cellsThatFitEachSide);
        int upperLimit = Mathf.FloorToInt(minCell + cellsThatFitEachSide);
        if (lowerLimit > upperLimit) return Mathf.RoundToInt((minCell + maxCell) * 0.5f);
        return Mathf.Clamp(desiredCenter, lowerLimit, upperLimit);
    }

    private Vector2 GetCellCenterPosition(MapCellData cell)
    {
        return new Vector2((cell.x - viewportCenterX) * cellStep.x, (cell.y - viewportCenterY) * cellStep.y);
    }

    private void CreateCellButton(MapCellData cell, PlayerState playerState)
    {
        GameObject buttonObject = new GameObject(cell.id + "_Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(mapPanel, false);
        createdMapObjects.Add(buttonObject);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        PlaceCellButton(rect, cell);

        Image image = buttonObject.GetComponent<Image>();
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        bool isCurrentCell = cell.id == playerState.currentCellId;
        bool canMove = false;
        string reason;
        if (!isCurrentCell && playerMapController != null) canMove = playerMapController.CanMoveTo(cell, out reason);

        Color cellColor = GetCellColor(cell, playerState, isCurrentCell, canMove);
        image.color = cellColor;
        button.interactable = canMove;

        ColorBlock colors = button.colors;
        colors.normalColor = cellColor;
        colors.highlightedColor = cellColor + new Color(0.08f, 0.08f, 0.08f, 0f);
        colors.pressedColor = cellColor * 0.85f;
        colors.disabledColor = cellColor;
        colors.colorMultiplier = 1f;
        button.colors = colors;

        MapCellData capturedCell = cell;
        button.onClick.AddListener(delegate { playerMapController.TryMoveTo(capturedCell); });

        Text label = CreateButtonText(buttonObject.transform);
        label.text = cell.name + "\n(" + cell.x + "," + cell.y + ")";
    }

    private void PlaceCellButton(RectTransform rect, MapCellData cell)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = cellSize;
        rect.anchoredPosition = GetCellCenterPosition(cell);
    }

    private Color GetCellColor(MapCellData cell, PlayerState playerState, bool isCurrentCell, bool canMove)
    {
        if (isCurrentCell) return new Color(0.92f, 0.76f, 0.28f, 1f);
        if (!cell.walkable || (cell.locked && !IsUnlockedByState(cell, playerState))) return new Color(0.25f, 0.25f, 0.25f, 1f);
        if (canMove) return new Color(0.36f, 0.60f, 0.38f, 1f);
        return new Color(0.38f, 0.41f, 0.45f, 1f);
    }

    private Text CreateButtonText(Transform parent)
    {
        GameObject textObject = new GameObject("CellText", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(4f, 4f);
        rect.offsetMax = new Vector2(-4f, -4f);
        Text text = textObject.GetComponent<Text>();
        text.font = GetDefaultFont();
        text.fontSize = 17;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private Font GetDefaultFont()
    {
        if (cachedFont != null) return cachedFont;
        cachedFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 16);
        if (cachedFont != null) return cachedFont;
        cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return cachedFont;
    }

    private void ClearCreatedButtons()
    {
        foreach (GameObject buttonObject in createdMapObjects)
        {
            if (buttonObject == null) continue;
            buttonObject.SetActive(false);
            Destroy(buttonObject);
        }
        createdMapObjects.Clear();
    }

    private void PrepareMapPanelForManualLayout()
    {
        if (cellStep.x < cellSize.x || cellStep.y < cellSize.y)
        {
            cellSize = new Vector2(92f, 58f);
            cellStep = new Vector2(108f, 70f);
        }

        LayoutGroup[] layoutGroups = mapPanel.GetComponents<LayoutGroup>();
        foreach (LayoutGroup layoutGroup in layoutGroups) layoutGroup.enabled = false;
    }

    private struct MapBounds
    {
        public int minX;
        public int maxX;
        public int minY;
        public int maxY;
    }

    private void CreateConnectionLines(List<MapCellData> visibleCells)
    {
        for (int i = 0; i < visibleCells.Count; i++)
        {
            for (int j = i + 1; j < visibleCells.Count; j++)
            {
                MapCellData first = visibleCells[i];
                MapCellData second = visibleCells[j];
                if (MapRuleUtility.ShouldConnectCells(first, second)) CreateConnectionLine(first, second);
            }
        }
    }

    private void CreateConnectionLine(MapCellData first, MapCellData second)
    {
        GameObject lineObject = new GameObject(first.id + "_to_" + second.id + "_Line", typeof(RectTransform), typeof(Image));
        lineObject.transform.SetParent(mapPanel, false);
        lineObject.transform.SetAsFirstSibling();
        createdMapObjects.Add(lineObject);

        Image image = lineObject.GetComponent<Image>();
        image.color = new Color(0.72f, 0.72f, 0.72f, 0.55f);
        image.raycastTarget = false;

        RectTransform rect = lineObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Vector2 firstCenter = GetCellCenterPosition(first);
        Vector2 secondCenter = GetCellCenterPosition(second);
        rect.anchoredPosition = (firstCenter + secondCenter) * 0.5f;

        bool horizontal = first.y == second.y;
        rect.sizeDelta = horizontal ? new Vector2(cellStep.x, connectionLineThickness) : new Vector2(connectionLineThickness, cellStep.y);
    }
}
