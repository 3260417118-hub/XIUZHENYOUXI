using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 负责刷新 Demo 场景里的地点、状态文字和提示文字。
/// 地点行为、人物按钮由 LocationActionManager 负责。
/// </summary>
public class LocationUIManager : MonoBehaviour
{
    [Header("顶部状态栏")]
    [SerializeField] private Text dayText;
    [SerializeField] private Text actionPointText;
    [SerializeField] private Text realmText;
    [SerializeField] private Text cultivationText;

    [Header("下方地点信息")]
    [SerializeField] private Text locationNameText;
    [SerializeField] private Text locationDescriptionText;
    [SerializeField] private Text messageText;

    public void SetReferences(
        Text day,
        Text actionPoint,
        Text realm,
        Text cultivation,
        Text locationName,
        Text locationDescription,
        Text message)
    {
        dayText = day;
        actionPointText = actionPoint;
        realmText = realm;
        cultivationText = cultivation;
        locationNameText = locationName;
        locationDescriptionText = locationDescription;
        messageText = message;
    }

    /// <summary>
    /// 刷新当前地点名称、描述，以及顶部状态栏。
    /// </summary>
    public void RefreshLocation(MapCellData currentCell, PlayerState playerState)
    {
        if (currentCell != null)
        {
            string displayName = currentCell.name;
            string displayDescription = currentCell.description;

            if (currentCell.id == "ruined_temple" && playerState != null && playerState.HasFlag("temple_repaired"))
            {
                displayName = "寺庙";
                displayDescription = "修缮后的寺庙虽然简陋，却多了几分庄严气息。";
            }

            if (locationNameText != null)
            {
                locationNameText.text = "【" + displayName + "】";
            }

            if (locationDescriptionText != null)
            {
                locationDescriptionText.text = displayDescription;
            }
        }

        RefreshPlayerStatus(playerState);
    }

    public void RefreshPlayerStatus(PlayerState playerState)
    {
        if (playerState == null)
        {
            return;
        }
        playerState.EnsureLists();

        if (dayText != null)
        {
            dayText.text = "第 " + playerState.day + " 天";
        }

        if (actionPointText != null)
        {
            actionPointText.text = "行动点：" + playerState.actionPoints + "/" + playerState.maxActionPoints;
        }

        if (realmText != null)
        {
            realmText.text = "境界：" + playerState.realm;
        }

        if (cultivationText != null)
        {
            cultivationText.text = "修为：" + playerState.cultivation + "/" + playerState.maxCultivation + "    灵石：" + playerState.spiritStones;
        }
    }

    public void ShowMessage(string message)
    {
        if (messageText != null)
        {
            messageText.text = "提示：" + NormalizeInlineText(message);
        }
    }

    public void ShowDialogue(string speaker, string text)
    {
        if (messageText != null)
        {
            messageText.text = "【" + speaker + "】 " + NormalizeInlineText(text);
        }
    }

    public void ShowEvent(string title, string text)
    {
        if (messageText != null)
        {
            // 下方提示栏高度较矮，如果文本开头是换行，只会看到标题。
            // 这里把剧情文本压成同一行开头显示，避免“只有【标题】没有内容”的问题。
            messageText.text = "【" + title + "】 " + NormalizeInlineText(text);
        }
    }

    private string NormalizeInlineText(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        string result = text.Replace("\r", " ").Replace("\n", " ").Trim();
        while (result.Contains("  "))
        {
            result = result.Replace("  ", " ");
        }
        return result;
    }
}
