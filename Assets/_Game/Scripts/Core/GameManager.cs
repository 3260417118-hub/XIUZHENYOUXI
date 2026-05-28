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
        EnsureComponent<InventoryLiteManager>();
        EnsureComponent<InventoryManager>();
        EnsureComponent<InventoryUIManager>();
        EnsureComponent<SkillManager>();
        EnsureComponent<DailyLimitManager>();
        EnsureComponent<CounterManager>();
        EnsureComponent<MapUnlockManager>();
        EnsureComponent<NightEventManager>();
        EnsureComponent<RestManager>();
        EnsureComponent<RealmManager>();
        EnsureComponent<CultivationManager>();
        EnsureComponent<BodyRealmManager>();
        EnsureComponent<BodyCultivationManager>();
        EnsureComponent<CharacterStatusUIManager>();
        EnsureComponent<ChapterOneLocationMechanicsManager>();
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
        playerState.playerName = "林昊";
        playerState.currentCellId = "village_gate";
        playerState.currentX = 0;
        playerState.currentY = 0;
        playerState.currentMapId = "main";
        playerState.returnMainCellId = "";
        playerState.day = 1;
        playerState.maxActionPoints = 3;
        playerState.actionPoints = 3;
        playerState.realm = "凡人";
        playerState.realmLevel = 0;
        playerState.cultivation = 0;
        playerState.maxCultivation = 150;
        playerState.bodyRealm = "凡体";
        playerState.bodyRealmLevel = 0;
        playerState.bodyCultivation = 0;
        playerState.maxBodyCultivation = 150;
        playerState.equippedCultivationSkillId = "";
        playerState.equippedBodyMethodId = "";
        playerState.equippedSpellSkillId = "";
        playerState.equippedWeaponId = "";
        playerState.spiritStones = 0;
        playerState.hasSeenOpening = false;
        playerState.activeBlockingEncounterId = "";
        playerState.currentRestLocationId = "ruined_hut";
        playerState.baseMaxHp = 50;
        playerState.baseAttack = 10;
        playerState.baseDefense = 0;
        playerState.maxHp = 50;
        playerState.hp = 50;
        playerState.attack = 10;
        playerState.defense = 0;
        playerState.EnsureLists();
        playerState.SetCounter("labor_level", 1);
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
