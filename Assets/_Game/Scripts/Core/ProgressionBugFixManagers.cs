using UnityEngine;

/// <summary>
/// 兜底修复：肉身突破只检查锻体值，不消耗锻体值。
/// 如果旧 BodyRealmManager 在突破时把 bodyCultivation 清零，本组件会在下一帧恢复突破前的锻体值。
/// </summary>
public class BodyBreakthroughNoCostFixManager : MonoBehaviour
{
    private GameManager gameManager;
    private LocationUIManager locationUIManager;
    private int lastBodyRealmLevel;
    private int lastBodyCultivation;
    private bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttach()
    {
        GameManager manager = FindObjectOfType<GameManager>();
        if (manager != null && manager.GetComponent<BodyBreakthroughNoCostFixManager>() == null)
        {
            manager.gameObject.AddComponent<BodyBreakthroughNoCostFixManager>();
        }
    }

    private void Update()
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        if (locationUIManager == null) locationUIManager = GetComponent<LocationUIManager>();
        PlayerState state = gameManager != null ? gameManager.GetPlayerState() : null;
        if (state == null) return;

        if (!initialized)
        {
            initialized = true;
            lastBodyRealmLevel = state.bodyRealmLevel;
            lastBodyCultivation = state.bodyCultivation;
            return;
        }

        if (state.bodyRealmLevel > lastBodyRealmLevel && state.bodyCultivation < lastBodyCultivation)
        {
            state.bodyCultivation = lastBodyCultivation;
            if (locationUIManager != null) locationUIManager.RefreshPlayerStatus(state);
            CharacterStatusUIManager status = GetComponent<CharacterStatusUIManager>();
            if (status != null) status.RefreshIfOpen();
        }

        lastBodyRealmLevel = state.bodyRealmLevel;
        lastBodyCultivation = state.bodyCultivation;
    }
}

/// <summary>
/// 兜底修复：第六天江鹤交易后，确保背包扣除 1 个乾坤锻体诀（残卷）。
/// 旧逻辑只调用 PlayerState.RemoveItem；如果背包系统未同步，这里会补扣一次并打标，防止重复扣除。
/// </summary>
public class JiangheTradeInventoryFixManager : MonoBehaviour
{
    private const string FragmentId = "qiankun_body_scroll_fragment";
    private const string TradeFlag = "traded_fragment_with_jianghe";
    private const string RemovedFlag = "jianghe_fragment_removed_from_inventory";

    private GameManager gameManager;
    private InventoryManager inventoryManager;
    private LocationUIManager locationUIManager;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttach()
    {
        GameManager manager = FindObjectOfType<GameManager>();
        if (manager != null && manager.GetComponent<JiangheTradeInventoryFixManager>() == null)
        {
            manager.gameObject.AddComponent<JiangheTradeInventoryFixManager>();
        }
    }

    private void Update()
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        if (inventoryManager == null) inventoryManager = GetComponent<InventoryManager>();
        if (locationUIManager == null) locationUIManager = GetComponent<LocationUIManager>();

        PlayerState state = gameManager != null ? gameManager.GetPlayerState() : null;
        if (state == null) return;
        if (!state.HasFlag(TradeFlag) || state.HasFlag(RemovedFlag)) return;

        if (state.HasItem(FragmentId))
        {
            if (inventoryManager != null) inventoryManager.RemoveItem(FragmentId, 1);
            else state.RemoveItem(FragmentId, 1);
        }

        state.AddFlag(RemovedFlag);
        if (locationUIManager != null) locationUIManager.RefreshPlayerStatus(state);
        InventoryUIManager inventoryUI = GetComponent<InventoryUIManager>();
        if (inventoryUI != null) inventoryUI.RefreshIfOpen();
        CharacterStatusUIManager status = GetComponent<CharacterStatusUIManager>();
        if (status != null) status.RefreshIfOpen();
    }
}
