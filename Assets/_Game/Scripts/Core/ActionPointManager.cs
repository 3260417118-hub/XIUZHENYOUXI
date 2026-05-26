using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 每日行动点管理器。
/// 负责检查行动点、扣除行动点、结束今日和刷新 UI。
/// </summary>
public class ActionPointManager : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private LocationUIManager locationUIManager;
    [SerializeField] private LocationActionManager locationActionManager;
    [SerializeField] private Button endDayButton;

    public void SetReferences(
        GameManager game,
        LocationUIManager locationUI,
        LocationActionManager actionManager,
        Button endDay)
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
        if (endDayButton == null)
        {
            return;
        }

        endDayButton.onClick.RemoveListener(EndDay);
        endDayButton.onClick.AddListener(EndDay);
    }

    public bool HasEnoughActionPoints(int cost)
    {
        if (gameManager == null)
        {
            return false;
        }

        return ActionPointRules.CanSpend(gameManager.GetPlayerState(), cost);
    }

    public bool TrySpendActionPoints(int cost)
    {
        if (gameManager == null)
        {
            return false;
        }

        PlayerState playerState = gameManager.GetPlayerState();
        if (!ActionPointRules.TrySpend(playerState, cost))
        {
            if (locationUIManager != null)
            {
                locationUIManager.ShowMessage("今日行动点不足");
            }

            return false;
        }

        RefreshUI();
        return true;
    }

    /// <summary>
    /// 结束今日：天数 +1，行动点恢复到最大值。
    /// </summary>
    public void EndDay()
    {
        if (gameManager == null)
        {
            return;
        }

        ActionPointRules.EndDay(gameManager.GetPlayerState());
        RefreshUI();

        if (locationActionManager != null)
        {
            locationActionManager.RefreshCurrentLocation();
        }

        if (locationUIManager != null)
        {
            locationUIManager.ShowMessage("新的一天开始了，行动点已恢复。");
        }
    }

    public void RefreshUI()
    {
        if (gameManager == null || locationUIManager == null)
        {
            return;
        }

        locationUIManager.RefreshPlayerStatus(gameManager.GetPlayerState());
    }
}
