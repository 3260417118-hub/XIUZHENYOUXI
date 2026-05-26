#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Demo 场景管理器自动挂载工具。
/// 点击菜单后，会把地图移动系统需要的脚本挂到 GameRoot 上，并自动绑定 UI 引用。
/// </summary>
public static class XianxiaDemoManagerSetup
{
    private const string DemoScenePath = "Assets/_Game/Scenes/DemoScene.unity";

    [MenuItem("Tools/Xianxia/Setup Demo Managers")]
    public static void SetupDemoManagers()
    {
        if (!OpenDemoScene())
        {
            return;
        }

        GameObject gameRoot = GameObject.Find("GameRoot");
        if (gameRoot == null)
        {
            gameRoot = new GameObject("GameRoot");
        }

        GameManager gameManager = GetOrAddComponent<GameManager>(gameRoot);
        MapGridManager mapGridManager = GetOrAddComponent<MapGridManager>(gameRoot);
        LocationUIManager locationUIManager = GetOrAddComponent<LocationUIManager>(gameRoot);
        PlayerMapController playerMapController = GetOrAddComponent<PlayerMapController>(gameRoot);

        RectTransform mapPanel = FindRectTransform("MapPanel");
        RemoveMapPanelLayoutGroups(mapPanel);

        locationUIManager.SetReferences(
            FindText("DayText"),
            FindText("ActionPointText"),
            FindText("RealmText"),
            FindText("CultivationText"),
            FindText("LocationNameText"),
            FindText("LocationDescriptionText"),
            FindText("MessageText"));

        mapGridManager.SetReferences(gameManager, playerMapController, locationUIManager, mapPanel);
        playerMapController.SetReferences(gameManager, mapGridManager, locationUIManager);
        gameManager.InitNewGame();

        EditorUtility.SetDirty(gameManager);
        EditorUtility.SetDirty(mapGridManager);
        EditorUtility.SetDirty(locationUIManager);
        EditorUtility.SetDirty(playerMapController);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.Refresh();

        Selection.activeGameObject = gameRoot;

        EditorUtility.DisplayDialog(
            "Demo 管理器设置完成",
            "已在 GameRoot 上挂载并绑定：\nGameManager\nMapGridManager\nLocationUIManager\nPlayerMapController\n\n点击 Play 后会从 map_cells.json 生成地图按钮。",
            "好的");
    }

    private static bool OpenDemoScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoScenePath) == null)
        {
            EditorUtility.DisplayDialog("找不到 Demo 场景", "请先创建场景：\n" + DemoScenePath, "好的");
            return false;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path == DemoScenePath)
        {
            return true;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return false;
        }

        EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
        return true;
    }

    private static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null)
        {
            component = target.AddComponent<T>();
        }

        return component;
    }

    private static Text FindText(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        if (target == null)
        {
            Debug.LogWarning("找不到 UI 文本对象：" + objectName);
            return null;
        }

        return target.GetComponent<Text>();
    }

    private static RectTransform FindRectTransform(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        if (target == null)
        {
            Debug.LogWarning("找不到 UI 对象：" + objectName);
            return null;
        }

        return target.GetComponent<RectTransform>();
    }

    private static void RemoveMapPanelLayoutGroups(RectTransform mapPanel)
    {
        if (mapPanel == null)
        {
            return;
        }

        // 地图按钮需要按 x,y 坐标摆放，不能让 GridLayoutGroup 自动排成普通列表。
        LayoutGroup[] layoutGroups = mapPanel.GetComponents<LayoutGroup>();
        foreach (LayoutGroup layoutGroup in layoutGroups)
        {
            Object.DestroyImmediate(layoutGroup);
        }

        EditorUtility.SetDirty(mapPanel);
    }
}
#endif
