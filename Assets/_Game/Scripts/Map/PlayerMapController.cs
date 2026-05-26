using UnityEngine;

/// <summary>
/// 负责判断和执行玩家在格子地图上的移动。
/// 第一版只允许上下左右移动一格，移动不消耗行动点。
/// 事件或对话打开时，禁止移动，避免 UI 混在一起。
/// </summary>
public class PlayerMapController : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private MapGridManager mapGridManager;
    [SerializeField] private LocationUIManager locationUIManager;
    [SerializeField] private LocationActionManager locationActionManager;
    [SerializeField] private EventManager eventManager;
    [SerializeField] private DialogueManager dialogueManager;

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
        EnsureManagers();
    }

    private void Start()
    {
        EnsureManagers();
    }

    private void EnsureManagers()
    {
        if (eventManager == null)
        {
            eventManager = GetComponent<EventManager>();
        }

        if (eventManager == null)
        {
            eventManager = gameObject.AddComponent<EventManager>();
        }

        if (dialogueManager == null)
        {
            dialogueManager = GetComponent<DialogueManager>();
        }
    }

    /// <summary>
    /// 事件或对话打开时，不允许玩家点击地图移动。
    /// 这样玩家必须先处理完当前事件/对话，底部按钮不会和地点按钮混在一起。
    /// </summary>
    private bool IsInteractionBlockingMovement(out string message)
    {
        EnsureManagers();
        message = "";

        if (eventManager != null && eventManager.IsEventOpen)
        {
            message = "请先处理当前事件。";
            return true;
        }

        if (dialogueManager != null && dialogueManager.IsDialogueOpen)
        {
            message = "请先结束当前对话。";
            return true;
        }

        return false;
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

        if (IsInteractionBlockingMovement(out message))
        {
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
            if (locationUIManager != null && !string.IsNullOrEmpty(message))
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

        EnsureManagers();
        if (eventManager != null)
        {
            eventManager.TryShowFirstEnterEvent(targetCell);
        }

        return true;
    }
}
