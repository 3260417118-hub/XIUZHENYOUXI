using NUnit.Framework;
using System.IO;
using UnityEngine;

public class MapAndActionPointRuleTests
{
    [Test]
    public void IsOrthogonalNeighbor_OnlyAllowsUpDownLeftRightOneStep()
    {
        Assert.IsTrue(MapRuleUtility.IsOrthogonalNeighbor(0, 0, 1, 0));
        Assert.IsTrue(MapRuleUtility.IsOrthogonalNeighbor(0, 0, 0, 1));
        Assert.IsFalse(MapRuleUtility.IsOrthogonalNeighbor(0, 0, 1, 1));
        Assert.IsFalse(MapRuleUtility.IsOrthogonalNeighbor(0, 0, 2, 0));
    }

    [Test]
    public void IsInVisibleRange_UsesCoordinateWindow()
    {
        Assert.IsTrue(MapRuleUtility.IsInVisibleRange(2, 1, 0, 0, 2));
        Assert.IsTrue(MapRuleUtility.IsInVisibleRange(2, 1, 1, -1, 2));
        Assert.IsFalse(MapRuleUtility.IsInVisibleRange(2, 1, -1, 0, 2));
    }

    [Test]
    public void ShouldConnectCells_OnlyConnectsWalkableOrthogonalNeighbors()
    {
        MapCellData left = NewCell("left", 1, 0, true);
        MapCellData right = NewCell("right", 2, 0, true);
        MapCellData diagonal = NewCell("diagonal", 2, 1, true);
        MapCellData blocked = NewCell("blocked", 1, 1, false);

        Assert.IsTrue(MapRuleUtility.ShouldConnectCells(left, right));
        Assert.IsFalse(MapRuleUtility.ShouldConnectCells(left, diagonal));
        Assert.IsFalse(MapRuleUtility.ShouldConnectCells(left, blocked));
    }

    [Test]
    public void GetCellAnchoredPosition_UsesUniformSpacing()
    {
        Vector2 position = MapRuleUtility.GetCellAnchoredPosition(2, 1, 0, 1, new Vector2(100f, 80f), new Vector2(16f, 16f));

        Assert.AreEqual(216f, position.x);
        Assert.AreEqual(-16f, position.y);
    }

    [Test]
    public void ActionPointRules_SpendAndEndDay()
    {
        PlayerState playerState = new PlayerState();
        playerState.day = 1;
        playerState.actionPoints = 3;
        playerState.maxActionPoints = 3;

        Assert.IsTrue(ActionPointRules.CanSpend(playerState, 1));
        Assert.IsTrue(ActionPointRules.TrySpend(playerState, 1));
        Assert.AreEqual(2, playerState.actionPoints);

        Assert.IsFalse(ActionPointRules.TrySpend(playerState, 3));
        Assert.AreEqual(2, playerState.actionPoints);

        ActionPointRules.EndDay(playerState);
        Assert.AreEqual(2, playerState.day);
        Assert.AreEqual(3, playerState.actionPoints);
    }

    [Test]
    public void MapCellData_GetMapId_AllowsCliffBottomMap()
    {
        MapCellData cell = NewCell("cliff_bottom", 0, 0, true);
        cell.mapId = "cliff_bottom";

        Assert.AreEqual("cliff_bottom", cell.GetMapId());
    }

    [Test]
    public void ChapterOneManager_NoLongerContainsLegacyCliffPlaceholder()
    {
        string sourcePath = Path.Combine(Application.dataPath, "_Game/Scripts/Core/ChapterOneLocationMechanicsManager.cs");
        string source = File.ReadAllText(sourcePath);

        Assert.IsFalse(source.Contains("新的地图内容暂未开放"));
        Assert.IsFalse(source.Contains("HandleBackMountainEnter(playerState)"));
    }

    [Test]
    public void CliffTrialData_RemovesDelayChoice()
    {
        string mapPath = Path.Combine(Application.dataPath, "_Game/Resources/Data/map_cells.json");
        string actionPath = Path.Combine(Application.dataPath, "_Game/Resources/Data/location_actions.json");

        Assert.IsFalse(File.ReadAllText(mapPath).Contains("delay_cliff_trial"));
        Assert.IsFalse(File.ReadAllText(actionPath).Contains("delay_cliff_trial"));
        Assert.IsFalse(File.ReadAllText(actionPath).Contains("暂时离开"));
    }

    [Test]
    public void LocationActionButtonWidth_FitsLongChineseActionName()
    {
        Assert.GreaterOrEqual(LocationActionManager.GetActionButtonPreferredWidth("沿藤蔓爬回后山"), 160f);
    }

    [Test]
    public void CliffStoryIntroAndTrial_DoNotPassVisibleTitles()
    {
        string sourcePath = Path.Combine(Application.dataPath, "_Game/Scripts/Core/CliffStoryManager.cs");
        string source = File.ReadAllText(sourcePath);

        Assert.IsFalse(source.Contains("PlayStoryAndReturn(\r\n            \"悬崖下的呼唤\""));
        Assert.IsFalse(source.Contains("PlayStoryAndReturn(\n            \"悬崖下的呼唤\""));
        Assert.IsFalse(source.Contains("PlayStoryAndReturn(\r\n            \"悬崖下的试炼\""));
        Assert.IsFalse(source.Contains("PlayStoryAndReturn(\n            \"悬崖下的试炼\""));
    }

    private static MapCellData NewCell(string id, int x, int y, bool walkable)
    {
        MapCellData cell = new MapCellData();
        cell.id = id;
        cell.x = x;
        cell.y = y;
        cell.walkable = walkable;
        return cell;
    }
}
