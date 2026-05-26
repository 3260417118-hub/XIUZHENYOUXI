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

        BlockingEncounterManager blockingEncounterManager = BlockingEncounterManager.Instance != null
            ? BlockingEncounterManager.Instance
            : GetComponent<BlockingEncounterManager>();
        if (blockingEncounterManager != null && blockingEncounterManager.HasActiveBlockingEncounter())
        {
            if (locationUIManager != null)
            {
                locationUIManager.ShowMessage(blockingEncounterManager.GetBlockMoveMessageOrDefault());
            }

            return false;
        }

        EventManager eventManager = GetComponent<EventManager>();
        if (eventManager != null && eventManager.IsEventOpen)
        {
            if (locationUIManager != null)
            {
                locationUIManager.ShowMessage("请先处理当前事件。");
            }

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
    /// 如果有首次进入事件、对话、阻塞式剧情或战斗未处理，不能跳过当天。
    /// </summary>
    public void EndDay()
    {
        if (gameManager == null)
        {
            return;
        }

        BlockingEncounterManager blockingEncounterManager = BlockingEncounterManager.Instance != null
            ? BlockingEncounterManager.Instance
            : GetComponent<BlockingEncounterManager>();
        EventManager eventManager = GetComponent<EventManager>();
        DialogueManager dialogueManager = GetComponent<DialogueManager>();

        if (OpeningStoryManager.IsOpeningActive || BattleManager.IsBattleOpen || (blockingEncounterManager != null && blockingEncounterManager.HasActiveBlockingEncounter()))
        {
            if (locationUIManager != null)
            {
                string message = blockingEncounterManager != null && blockingEncounterManager.HasActiveBlockingEncounter()
                    ? blockingEncounterManager.GetBlockMoveMessageOrDefault()
                    : "请先处理当前事件。";
                locationUIManager.ShowMessage(message);
            }

            return;
        }

        if (eventManager != null && eventManager.IsEventOpen)
        {
            if (locationUIManager != null)
            {
                locationUIManager.ShowMessage("请先处理当前事件。");
            }

            return;
        }

        if (dialogueManager != null && dialogueManager.IsDialogueOpen)
        {
            if (locationUIManager != null)
            {
                locationUIManager.ShowMessage("请先结束当前对话。");
            }

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

        if (blockingEncounterManager != null)
        {
            blockingEncounterManager.CheckTodayEncounter();
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
