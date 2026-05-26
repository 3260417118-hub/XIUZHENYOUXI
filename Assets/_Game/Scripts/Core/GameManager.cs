using UnityEngine;

/// <summary>
/// 游戏总管理器。
/// 第一版只负责创建和保存当前玩家状态，其他系统通过它读取 PlayerState。
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private PlayerState playerState = new PlayerState();

    public PlayerState PlayerState
    {
        get { return playerState; }
    }

    private void Awake()
    {
        Instance = this;

        if (playerState == null || string.IsNullOrEmpty(playerState.currentCellId))
        {
            InitNewGame();
        }
        else
        {
            playerState.EnsureLists();
        }

        EnsureRuntimeManagers();
    }

    /// <summary>
    /// 确保 Demo 场景里存在必要管理器。
    /// 这样旧场景不用手动拖组件，进入 Play 后也能自动创建新系统。
    /// </summary>
    private void EnsureRuntimeManagers()
    {
        EnsureComponent<SaveManager>();
        EnsureComponent<OpeningStoryManager>();
        EnsureComponent<BlockingEncounterManager>();
        EnsureComponent<DayEventManager>();
        EnsureComponent<BattleManager>();
        EnsureComponent<TutorialManager>();
        EnsureComponent<SaveButtonOverrideManager>();
    }

    private void EnsureComponent<T>() where T : Component
    {
        if (GetComponent<T>() == null)
        {
            gameObject.AddComponent<T>();
        }
    }

    /// <summary>
    /// 初始化一个新游戏。
    /// 起点是 map_cells.json 里的“村口”。
    /// </summary>
    public void InitNewGame()
    {
        playerState = new PlayerState();
        playerState.currentCellId = "village_gate";
        playerState.currentX = 0;
        playerState.currentY = 0;
        playerState.day = 1;
        playerState.maxActionPoints = 3;
        playerState.actionPoints = 3;
        playerState.cultivation = 0;
        playerState.realm = "凡人";
        playerState.spiritStones = 0;
        playerState.hasSeenOpening = false;
        playerState.activeBlockingEncounterId = "";
        playerState.maxHp = 100;
        playerState.hp = 100;
        playerState.attack = 15;
        playerState.defense = 3;
        playerState.EnsureLists();
    }

    public PlayerState GetPlayerState()
    {
        if (playerState != null)
        {
            playerState.EnsureLists();
        }

        return playerState;
    }
}
