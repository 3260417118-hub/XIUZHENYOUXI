using UnityEngine;

/// <summary>
/// 负责判断和执行玩家在格子地图上的移动。
/// 第一版只允许上下左右移动一格，移动不消耗行动点。
/// </summary>
public class PlayerMapController : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private MapGridManager mapGridManager;
    [SerializeField] private LocationUIManager locationUIManager;
    [SerializeField] private LocationActionManager locationActionManager;
    [SerializeField] private EventManager eventManager;

    public void SetReferences(
        GameManager game,
        MapGridManager mapGrid,
        LocationUIManager locationUI,
        LocationActionManager actionManager)
    {
        gameManager = game;
        mapGridManager = mapGrid;
        locationUIManager = locationUI;
        locationActionManager = actionManager;
        EnsureEventManager();
    }

    private void Start()
    {
        EnsureEventManager();
    }

    private void EnsureEventManager()
    {
        if (eventManager == null)
        {
            eventManager = GetComponent<EventManager>();
        }

        if (eventManager == null)
        {
            eventManager = gameObject.AddComponent<EventManager>();
        }
    }

    /// <summary>
    /// 判断目标格子是否可以移动过去。
    /// 只允许上下左右相邻格子，不能斜着走，不能一次走多格。
    /// </summary>
    public bool CanMoveTo(MapCellData targetCell, out string message)
    {
        message = "";

        if (gameManager == null)
        {
            message = "缺少 GameManager。";
            return false;
        }

        if (targetCell == null)
        {
            message = "目标地点不存在。";
            return false;
        }

        if (!targetCell.walkable)
        {
            message = "这里暂时不能进入。";
            return false;
        }

        PlayerState playerState = gameManager.GetPlayerState();

        if (!MapRuleUtility.IsOrthogonalNeighbor(playerState.currentX, playerState.currentY, targetCell.x, targetCell.y))
        {
            message = "只能移动到上下左右相邻的格子。";
            return false;
        }

        return true;
    }

    public bool TryMoveTo(MapCellData targetCell)
    {
        string message;
        if (!CanMoveTo(targetCell, out message))
        {
            if (locationUIManager != null)
            {
                locationUIManager.ShowMessage(message);
            }

            return false;
        }

        PlayerState playerState = gameManager.GetPlayerState();
        playerState.EnsureLists();
        playerState.currentCellId = targetCell.id;
        playerState.currentX = targetCell.x;
        playerState.currentY = targetCell.y;

        if (mapGridManager != null)
        {
            mapGridManager.RefreshMap();
        }

        if (locationUIManager != null)
        {
            locationUIManager.RefreshLocation(targetCell, playerState);
            locationUIManager.ShowMessage("你来到了：" + targetCell.name);
        }

        if (locationActionManager != null)
        {
            locationActionManager.RefreshCurrentLocation();
        }

        EnsureEventManager();
        if (eventManager != null)
        {
            eventManager.TryShowFirstEnterEvent(targetCell);
        }

        return true;
    }
}
