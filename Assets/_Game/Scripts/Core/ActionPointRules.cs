/// <summary>
/// 行动点纯规则。
/// ActionPointManager 会调用这里，测试也可以直接验证这里。
/// </summary>
public static class ActionPointRules
{
    public static bool CanSpend(PlayerState playerState, int cost)
    {
        if (playerState == null)
        {
            return false;
        }

        if (cost <= 0)
        {
            return true;
        }

        return playerState.actionPoints >= cost;
    }

    public static bool TrySpend(PlayerState playerState, int cost)
    {
        if (!CanSpend(playerState, cost))
        {
            return false;
        }

        if (cost > 0)
        {
            playerState.actionPoints -= cost;
        }

        return true;
    }

    public static void EndDay(PlayerState playerState)
    {
        if (playerState == null)
        {
            return;
        }

        playerState.day += 1;
        playerState.actionPoints = playerState.maxActionPoints;
    }
}
