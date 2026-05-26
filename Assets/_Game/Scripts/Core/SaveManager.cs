using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 第一版本地存档管理器。
/// 只保存 PlayerState，不保存背包、任务等复杂数据。
/// 存档文件放在 Application.persistentDataPath 下。
/// </summary>
public class SaveManager : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private MapGridManager mapGridManager;
    [SerializeField] private LocationUIManager locationUIManager;
    [SerializeField] private LocationActionManager locationActionManager;
    [SerializeField] private DialogueManager dialogueManager;

    [Header("存档按钮")]
    [SerializeField] private Button saveGameButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button newGameButton;

    private Font cachedFont;

    private string SaveFilePath
    {
        get { return Path.Combine(Application.persistentDataPath, "xianxia_save.json"); }
    }

    public void SetReferences(
        GameManager game,
        MapGridManager mapGrid,
        LocationUIManager locationUI,
        LocationActionManager actionManager,
        DialogueManager dialogue)
    {
        gameManager = game;
        mapGridManager = mapGrid;
        locationUIManager = locationUI;
        locationActionManager = actionManager;
        dialogueManager = dialogue;

        EnsureSaveButtons();
        BindButtons();
    }

    private void Start()
    {
        FindMissingReferences();
        EnsureSaveButtons();
        BindButtons();
    }

    private void FindMissingReferences()
    {
        if (gameManager == null)
        {
            gameManager = GetComponent<GameManager>();
        }

        if (mapGridManager == null)
        {
            mapGridManager = GetComponent<MapGridManager>();
        }

        if (locationUIManager == null)
        {
            locationUIManager = GetComponent<LocationUIManager>();
        }

        if (locationActionManager == null)
        {
            locationActionManager = GetComponent<LocationActionManager>();
        }

        if (dialogueManager == null)
        {
            dialogueManager = GetComponent<DialogueManager>();
        }
    }

    private void BindButtons()
    {
        if (saveGameButton != null)
        {
            saveGameButton.onClick.RemoveListener(SaveGame);
            saveGameButton.onClick.AddListener(SaveGame);
        }

        if (loadGameButton != null)
        {
            loadGameButton.onClick.RemoveListener(LoadGame);
            loadGameButton.onClick.AddListener(LoadGame);
        }

        if (newGameButton != null)
        {
            newGameButton.onClick.RemoveListener(NewGame);
            newGameButton.onClick.AddListener(NewGame);
        }
    }

    /// <summary>
    /// 保存当前 PlayerState 到本地 JSON。
    /// </summary>
    public void SaveGame()
    {
        if (gameManager == null)
        {
            ShowMessage("保存失败：缺少 GameManager。");
            return;
        }

        try
        {
            string json = JsonUtility.ToJson(gameManager.GetPlayerState(), true);
            File.WriteAllText(SaveFilePath, json);
            ShowMessage("保存成功：" + SaveFilePath);
        }
        catch (Exception exception)
        {
            Debug.LogError("保存游戏失败：" + exception.Message);
            ShowMessage("保存失败，请查看 Console。 ");
        }
    }

    public bool HasSave()
    {
        return File.Exists(SaveFilePath);
    }

    /// <summary>
    /// 从本地 JSON 读取 PlayerState。
    /// 读取后刷新地图、状态栏、地点描述、地点行为按钮和 NPC 按钮。
    /// </summary>
    public void LoadGame()
    {
        if (!HasSave())
        {
            ShowMessage("暂无存档");
            return;
        }

        if (gameManager == null)
        {
            ShowMessage("读取失败：缺少 GameManager。");
            return;
        }

        try
        {
            string json = File.ReadAllText(SaveFilePath);
            PlayerState loadedState = JsonUtility.FromJson<PlayerState>(json);

            if (loadedState == null || string.IsNullOrEmpty(loadedState.currentCellId))
            {
                ShowMessage("存档数据无效。");
                return;
            }

            CopyPlayerState(loadedState, gameManager.GetPlayerState());
            RefreshAfterStateChanged("读取存档成功。");
        }
        catch (Exception exception)
        {
            Debug.LogError("读取游戏失败：" + exception.Message);
            ShowMessage("读取失败，请查看 Console。 ");
        }
    }

    /// <summary>
    /// 开始新游戏。
    /// 只重置当前游戏状态，不删除已有存档。
    /// 如果想覆盖旧存档，可以新游戏后再点“保存游戏”。
    /// </summary>
    public void NewGame()
    {
        if (gameManager == null)
        {
            ShowMessage("新游戏失败：缺少 GameManager。");
            return;
        }

        gameManager.InitNewGame();
        RefreshAfterStateChanged("新游戏开始。旧存档不会自动删除。 ");
    }

    private void CopyPlayerState(PlayerState source, PlayerState target)
    {
        if (source == null || target == null)
        {
            return;
        }

        target.currentCellId = source.currentCellId;
        target.currentX = source.currentX;
        target.currentY = source.currentY;
        target.day = source.day;
        target.actionPoints = source.actionPoints;
        target.maxActionPoints = source.maxActionPoints <= 0 ? 3 : source.maxActionPoints;
        target.cultivation = source.cultivation;
        target.realm = string.IsNullOrEmpty(source.realm) ? "凡人" : source.realm;
        target.spiritStones = source.spiritStones;
    }

    private void RefreshAfterStateChanged(string message)
    {
        // 读取或新游戏时，先关闭可能正在显示的对话选项。
        if (dialogueManager != null)
        {
            dialogueManager.CloseDialogueSilently();
        }

        if (mapGridManager != null)
        {
            mapGridManager.RefreshMap();
        }

        PlayerState playerState = gameManager != null ? gameManager.GetPlayerState() : null;
        MapCellData currentCell = mapGridManager != null ? mapGridManager.GetCurrentCell() : null;

        if (locationUIManager != null)
        {
            locationUIManager.RefreshLocation(currentCell, playerState);
            locationUIManager.ShowMessage(message);
        }

        if (locationActionManager != null)
        {
            locationActionManager.RefreshCurrentLocation();
        }
    }

    private void ShowMessage(string message)
    {
        if (locationUIManager != null)
        {
            locationUIManager.ShowMessage(message);
        }
        else
        {
            Debug.Log(message);
        }
    }

    private void EnsureSaveButtons()
    {
        if (saveGameButton == null)
        {
            saveGameButton = FindButton("SaveGameButton");
        }

        if (loadGameButton == null)
        {
            loadGameButton = FindButton("LoadGameButton");
        }

        if (newGameButton == null)
        {
            newGameButton = FindButton("NewGameButton");
        }

        if (saveGameButton != null && loadGameButton != null && newGameButton != null)
        {
            return;
        }

        Button endDayButton = FindButton("EndDayButton");
        Transform parent = null;
        RectTransform endDayRect = null;

        if (endDayButton != null)
        {
            parent = endDayButton.transform.parent;
            endDayRect = endDayButton.GetComponent<RectTransform>();
        }
        else
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                parent = canvas.transform;
            }
        }

        if (parent == null)
        {
            return;
        }

        if (newGameButton == null)
        {
            newGameButton = CreateTopButton(parent, "NewGameButton", "新游戏", endDayRect, -330f);
        }

        if (loadGameButton == null)
        {
            loadGameButton = CreateTopButton(parent, "LoadGameButton", "读取游戏", endDayRect, -220f);
        }

        if (saveGameButton == null)
        {
            saveGameButton = CreateTopButton(parent, "SaveGameButton", "保存游戏", endDayRect, -110f);
        }
    }

    private Button FindButton(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        if (target == null)
        {
            return null;
        }

        return target.GetComponent<Button>();
    }

    private Button CreateTopButton(Transform parent, string objectName, string text, RectTransform referenceRect, float offsetX)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        if (referenceRect != null)
        {
            rect.anchorMin = referenceRect.anchorMin;
            rect.anchorMax = referenceRect.anchorMax;
            rect.pivot = referenceRect.pivot;
            rect.sizeDelta = new Vector2(100f, referenceRect.sizeDelta.y <= 0f ? 44f : referenceRect.sizeDelta.y);
            rect.anchoredPosition = referenceRect.anchoredPosition + new Vector2(offsetX, 0f);
        }
        else
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(100f, 44f);
            rect.anchoredPosition = new Vector2(-20f + offsetX, -20f);
        }

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.30f, 0.34f, 0.38f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.40f, 0.46f, 0.50f, 1f);
        colors.pressedColor = new Color(0.22f, 0.25f, 0.28f, 1f);
        button.colors = colors;

        Text label = CreateButtonText(buttonObject.transform, text);
        StretchToParent(label.rectTransform);

        return button;
    }

    private Text CreateButtonText(Transform parent, string text)
    {
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        Text label = textObject.GetComponent<Text>();
        label.text = text;
        label.font = GetDefaultFont();
        label.fontSize = 18;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;

        return label;
    }

    private Font GetDefaultFont()
    {
        if (cachedFont != null)
        {
            return cachedFont;
        }

        cachedFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 16);
        if (cachedFont != null)
        {
            return cachedFont;
        }

        cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return cachedFont;
    }

    private void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
