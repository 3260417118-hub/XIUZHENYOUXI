#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 修仙游戏 Demo 场景生成工具。
/// 放在 Editor 文件夹中，只会在 Unity 编辑器里运行，不会进入正式游戏包。
/// </summary>
public static class XianxiaDemoSceneCreator
{
    private const string DemoScenePath = "Assets/_Game/Scenes/DemoScene.unity";
    private static Font defaultFont;

    /// <summary>
    /// Unity 顶部菜单：Tools/Xianxia/Create Demo Scene
    /// 点击后会创建一个新的 Demo 场景，并自动摆好基础 UGUI。
    /// </summary>
    [MenuItem("Tools/Xianxia/Create Demo Scene")]
    public static void CreateDemoScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EnsureProjectFolders();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateMainCamera();
        CreateEventSystem();

        Canvas canvas = CreateCanvas();
        CreateTopStatusBar(canvas.transform);
        CreateMapPanel(canvas.transform);
        CreateBottomPanel(canvas.transform);
        CreateEndDayButton(canvas.transform);

        GameObject gameRoot = new GameObject("GameRoot");
        Selection.activeGameObject = gameRoot;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, DemoScenePath);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Demo 场景创建完成",
            "已创建并保存 Demo 场景：\n" + DemoScenePath + "\n\n接着点击 Tools/Xianxia/Setup Demo Managers，可以自动挂载并绑定地图移动系统脚本。",
            "好的");
    }

    private static void EnsureProjectFolders()
    {
        EnsureFolder("Assets", "_Game");
        EnsureFolder("Assets/_Game", "Scenes");
        EnsureFolder("Assets/_Game", "Scripts");
        EnsureFolder("Assets/_Game/Scripts", "Core");
        EnsureFolder("Assets/_Game/Scripts", "Data");
        EnsureFolder("Assets/_Game/Scripts", "Map");
        EnsureFolder("Assets/_Game/Scripts", "UI");
        EnsureFolder("Assets/_Game/Scripts", "Dialogue");
        EnsureFolder("Assets/_Game/Scripts", "Save");
        EnsureFolder("Assets/_Game/Scripts", "Editor");
        EnsureFolder("Assets/_Game", "Resources");
        EnsureFolder("Assets/_Game/Resources", "Data");
        EnsureFolder("Assets/_Game", "Prefabs");
        EnsureFolder("Assets/_Game", "Art");
        EnsureFolder("Assets/_Game", "Audio");
    }

    private static void EnsureFolder(string parentPath, string folderName)
    {
        string fullPath = parentPath + "/" + folderName;
        if (!AssetDatabase.IsValidFolder(fullPath))
        {
            AssetDatabase.CreateFolder(parentPath, folderName);
        }
    }

    private static void CreateMainCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.13f, 0.15f);
        camera.orthographic = true;

        cameraObject.AddComponent<AudioListener>();
    }

    private static void CreateEventSystem()
    {
        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private static Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject(
            "Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private static void CreateTopStatusBar(Transform parent)
    {
        RectTransform bar = CreatePanel(
            "TopStatusBar",
            parent,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, 80f),
            Vector2.zero,
            new Color(0.18f, 0.19f, 0.22f, 1f));

        HorizontalLayoutGroup layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 12, 12);
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        CreateStatusText("DayText", "第 1 天", bar);
        CreateStatusText("ActionPointText", "行动点：3/3", bar);
        CreateStatusText("RealmText", "境界：凡人", bar);
        CreateStatusText("CultivationText", "修为：0", bar);
    }

    private static void CreateMapPanel(Transform parent)
    {
        RectTransform mapPanel = CreatePanel(
            "MapPanel",
            parent,
            new Vector2(0f, 0.30f),
            new Vector2(1f, 0.92f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.22f, 0.24f, 0.26f, 1f));

        mapPanel.offsetMin = new Vector2(24f, 16f);
        mapPanel.offsetMax = new Vector2(-24f, -16f);

        // 地图格子由 MapGridManager 按 x,y 坐标摆放，这里不加 GridLayoutGroup。
    }

    private static void CreateBottomPanel(Transform parent)
    {
        RectTransform bottomPanel = CreatePanel(
            "BottomPanel",
            parent,
            new Vector2(0f, 0f),
            new Vector2(1f, 0.30f),
            new Vector2(0.5f, 0f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.16f, 0.17f, 0.20f, 1f));

        bottomPanel.offsetMin = new Vector2(24f, 24f);
        bottomPanel.offsetMax = new Vector2(-24f, -8f);

        VerticalLayoutGroup layout = bottomPanel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 14, 14);
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateText("LocationNameText", "青石小镇", bottomPanel, 26, TextAnchor.MiddleLeft, 34f, Color.white);
        CreateText("LocationDescriptionText", "这里显示当前格子的描述。", bottomPanel, 20, TextAnchor.UpperLeft, 64f, new Color(0.86f, 0.86f, 0.86f));
        CreateButtonContainer("ActionButtonContainer", bottomPanel);
        CreateButtonContainer("NPCButtonContainer", bottomPanel);
        CreateText("MessageText", "消息提示会显示在这里。", bottomPanel, 18, TextAnchor.MiddleLeft, 34f, new Color(0.95f, 0.82f, 0.45f));
    }

    private static void CreateEndDayButton(Transform parent)
    {
        Button button = CreateButton(
            "EndDayButton",
            "结束今日",
            parent,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(160f, 52f),
            new Vector2(-24f, -96f));

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.55f, 0.35f, 0.16f);
        colors.highlightedColor = new Color(0.68f, 0.44f, 0.20f);
        colors.pressedColor = new Color(0.42f, 0.25f, 0.12f);
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
    }

    private static void CreateStatusText(string name, string content, RectTransform parent)
    {
        Text text = CreateText(name, content, parent, 22, TextAnchor.MiddleCenter, 0f, Color.white);
        text.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
    }

    private static void CreateButtonContainer(string name, RectTransform parent)
    {
        RectTransform container = CreatePanel(
            name,
            parent,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 42f),
            Vector2.zero,
            new Color(0.10f, 0.11f, 0.13f, 1f));

        LayoutElement layoutElement = container.gameObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 42f;

        HorizontalLayoutGroup layout = container.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 6, 6);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
    }

    private static Button CreateButton(
        string name,
        string label,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 size,
        Vector2 anchoredPosition)
    {
        RectTransform rect = CreatePanel(
            name,
            parent,
            anchorMin,
            anchorMax,
            pivot,
            size,
            anchoredPosition,
            new Color(0.32f, 0.33f, 0.36f, 1f));

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();

        Text buttonText = CreateText(name + "Text", label, rect, 22, TextAnchor.MiddleCenter, 0f, Color.white);
        StretchToParent(buttonText.rectTransform);

        return button;
    }

    private static RectTransform CreatePanel(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 size,
        Vector2 anchoredPosition,
        Color color)
    {
        GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        Image image = panelObject.GetComponent<Image>();
        image.color = color;

        return rect;
    }

    private static Text CreateText(
        string name,
        string content,
        Transform parent,
        int fontSize,
        TextAnchor alignment,
        float preferredHeight,
        Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.text = content;
        text.font = GetDefaultFont();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        if (preferredHeight > 0f)
        {
            LayoutElement layoutElement = textObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;
        }

        return text;
    }

    private static Font GetDefaultFont()
    {
        if (defaultFont != null)
        {
            return defaultFont;
        }

        // 优先使用系统中文字体，避免中文在 UGUI Text 里显示成方块。
        defaultFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 16);
        if (defaultFont != null)
        {
            return defaultFont;
        }

        // Unity/Tuanjie 新版本不再支持旧的内置字体名，官方提示改用 LegacyRuntime.ttf。
        defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return defaultFont;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
#endif
