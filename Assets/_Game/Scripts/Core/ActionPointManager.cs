using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 每日行动点管理器。
/// 负责检查行动点、扣除行动点和刷新 UI。
/// 注意：现在不能通过全局按钮直接结束今日，必须去破败小屋休息。
/// </summary>
public class ActionPointManager : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private LocationUIManager locationUIManager;
    [SerializeField] private LocationActionManager locationActionManager;
    [SerializeField] private Button endDayButton;

    public void SetReferences(GameManager game, LocationUIManager locationUI, LocationActionManager actionManager, Button endDay)
    {
        gameManager = game;
        locationUIManager = locationUI;
        locationActionManager = actionManager;
        endDayButton = endDay;
        BindEndDayButton();
    }

    private void Start()
    {
        BindEndDayButton();
        RefreshUI();
    }

    private void BindEndDayButton()
    {
        if (endDayButton == null) return;
        endDayButton.onClick.RemoveAllListeners();
        endDayButton.onClick.AddListener(EndDay);
        Text label = endDayButton.GetComponentInChildren<Text>();
        if (label != null) label.text = "休息过夜";
    }

    public bool HasEnoughActionPoints(int cost)
    {
        if (gameManager == null) return false;
        return ActionPointRules.CanSpend(gameManager.GetPlayerState(), cost);
    }

    public bool TrySpendActionPoints(int cost)
    {
        if (gameManager == null) return false;

        if (RestManager.IsRestingTransition)
        {
            ToastManager.TryShowWarning("正在休息过夜");
            if (locationUIManager != null) locationUIManager.ShowMessage("正在休息过夜。");
            return false;
        }

        BlockingEncounterManager blockingEncounterManager = BlockingEncounterManager.Instance != null
            ? BlockingEncounterManager.Instance
            : GetComponent<BlockingEncounterManager>();
        if (blockingEncounterManager != null && blockingEncounterManager.HasActiveBlockingEncounter())
        {
            ToastManager.TryShowWarning("请先处理当前事件");
            if (locationUIManager != null) locationUIManager.ShowMessage(blockingEncounterManager.GetBlockMoveMessageOrDefault());
            return false;
        }

        EventManager eventManager = GetComponent<EventManager>();
        if (eventManager != null && eventManager.IsEventOpen)
        {
            ToastManager.TryShowWarning("请先处理当前事件");
            if (locationUIManager != null) locationUIManager.ShowMessage("请先处理当前事件。");
            return false;
        }

        PlayerState playerState = gameManager.GetPlayerState();
        if (!ActionPointRules.TrySpend(playerState, cost))
        {
            ToastManager.TryShowWarning("行动点不足");
            if (locationUIManager != null) locationUIManager.ShowMessage("你已经没有力气继续行动了。");
            return false;
        }

        RefreshUI();
        return true;
    }

    /// <summary>
    /// 全局按钮不再推进天数，只提示玩家回破败小屋休息。
    /// 真正 day + 1 的逻辑在 RestManager.SleepUntilNextDay()。
    /// </summary>
    public void EndDay()
    {
        if (locationUIManager != null)
        {
            locationUIManager.ShowMessage("你需要回到破败小屋休息，才能结束今日。");
        }
    }

    public void RefreshUI()
    {
        if (gameManager == null || locationUIManager == null) return;
        locationUIManager.RefreshPlayerStatus(gameManager.GetPlayerState());
    }
}
