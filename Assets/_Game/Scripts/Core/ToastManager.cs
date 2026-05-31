using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 通用短提示 UI。用于物品、灵石、条件不足、购买出售、战斗结果等轻量反馈。
/// Toast 不改变剧情文本，也不阻塞玩家操作。
/// </summary>
public class ToastManager : MonoBehaviour
{
    public static ToastManager Instance { get; private set; }

    [SerializeField] private float showSeconds = 2f;
    [SerializeField] private int maxVisibleToastCount = 3;

    private readonly Queue<ToastRequest> pendingToasts = new Queue<ToastRequest>();
    private readonly List<GameObject> visibleToasts = new List<GameObject>();

    private RectTransform containerRect;
    private Font cachedFont;

    private void Awake()
    {
        Instance = this;
        EnsureContainer();
    }

    private void Update()
    {
        EnsureContainer();
        CleanupVisibleToasts();

        if (IsStoryOverlayActive()) return;
        while (pendingToasts.Count > 0 && visibleToasts.Count < maxVisibleToastCount)
        {
            ToastRequest request = pendingToasts.Dequeue();
            CreateToast(request);
        }
    }

    public void ShowToast(string message)
    {
        Enqueue(message, ToastKind.Normal);
    }

    public void ShowItemToast(string itemName, int count)
    {
        string safeName = string.IsNullOrEmpty(itemName) ? "未知物品" : itemName;
        int safeCount = Mathf.Max(1, count);
        Enqueue("获得物品：" + safeName + " x" + safeCount, ToastKind.Success);
    }

    public void ShowSpiritStoneToast(int amount)
    {
        Enqueue("获得灵石：" + Mathf.Max(0, amount), ToastKind.Success);
    }

    public void ShowCostToast(string label, int amount)
    {
        string safeLabel = string.IsNullOrEmpty(label) ? "资源" : label;
        Enqueue("消耗" + safeLabel + "：" + Mathf.Max(0, amount), ToastKind.Warning);
    }

    public void ShowWarning(string message)
    {
        Enqueue(message, ToastKind.Warning);
    }

    public void ShowSuccess(string message)
    {
        Enqueue(message, ToastKind.Success);
    }

    public static void TryShowToast(string message)
    {
        if (Instance != null) Instance.ShowToast(message);
    }

    public static void TryShowItemToast(string itemName, int count)
    {
        if (Instance != null) Instance.ShowItemToast(itemName, count);
    }

    public static void TryShowSpiritStoneToast(int amount)
    {
        if (Instance != null) Instance.ShowSpiritStoneToast(amount);
    }

    public static void TryShowCostToast(string label, int amount)
    {
        if (Instance != null) Instance.ShowCostToast(label, amount);
    }

    public static void TryShowWarning(string message)
    {
        if (Instance != null) Instance.ShowWarning(message);
    }

    public static void TryShowSuccess(string message)
    {
        if (Instance != null) Instance.ShowSuccess(message);
    }

    private void Enqueue(string message, ToastKind kind)
    {
        if (string.IsNullOrEmpty(message)) return;
        pendingToasts.Enqueue(new ToastRequest { message = message, kind = kind });
    }

    private void EnsureContainer()
    {
        if (containerRect != null) return;
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        GameObject containerObject = new GameObject("ToastContainer", typeof(RectTransform));
        containerObject.transform.SetParent(canvas.transform, false);
        containerObject.transform.SetAsLastSibling();
        containerRect = containerObject.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(1f, 1f);
        containerRect.anchorMax = new Vector2(1f, 1f);
        containerRect.pivot = new Vector2(1f, 1f);
        containerRect.sizeDelta = new Vector2(360f, 190f);
        containerRect.anchoredPosition = new Vector2(-24f, -72f);
    }

    private void CreateToast(ToastRequest request)
    {
        if (containerRect == null) return;

        GameObject toastObject = new GameObject("ToastItem", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        toastObject.transform.SetParent(containerRect, false);
        visibleToasts.Add(toastObject);

        RectTransform rect = toastObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(340f, 46f);
        rect.anchoredPosition = new Vector2(0f, -GetToastIndex(toastObject) * 54f);

        Image image = toastObject.GetComponent<Image>();
        image.color = GetBackgroundColor(request.kind);
        image.raycastTarget = false;

        CanvasGroup canvasGroup = toastObject.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        Text label = CreateText(toastObject.transform, request.message, 17, TextAnchor.MiddleLeft, Color.white);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(14f, 6f);
        labelRect.offsetMax = new Vector2(-14f, -6f);

        StartCoroutine(ToastRoutine(toastObject, canvasGroup));
        ReflowToasts();
    }

    private IEnumerator ToastRoutine(GameObject toastObject, CanvasGroup canvasGroup)
    {
        yield return Fade(canvasGroup, 0f, 1f, 0.16f);
        yield return new WaitForSeconds(showSeconds);
        yield return Fade(canvasGroup, 1f, 0f, 0.22f);

        visibleToasts.Remove(toastObject);
        if (toastObject != null) Destroy(toastObject);
        ReflowToasts();
    }

    private IEnumerator Fade(CanvasGroup canvasGroup, float from, float to, float duration)
    {
        if (canvasGroup == null) yield break;
        float timer = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (timer < safeDuration)
        {
            timer += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(timer / safeDuration));
            yield return null;
        }
        canvasGroup.alpha = to;
    }

    private void ReflowToasts()
    {
        for (int i = 0; i < visibleToasts.Count; i++)
        {
            GameObject toastObject = visibleToasts[i];
            if (toastObject == null) continue;
            RectTransform rect = toastObject.GetComponent<RectTransform>();
            if (rect != null) rect.anchoredPosition = new Vector2(0f, -i * 54f);
        }
    }

    private void CleanupVisibleToasts()
    {
        for (int i = visibleToasts.Count - 1; i >= 0; i--)
        {
            if (visibleToasts[i] == null) visibleToasts.RemoveAt(i);
        }
    }

    private int GetToastIndex(GameObject toastObject)
    {
        int index = visibleToasts.IndexOf(toastObject);
        return index < 0 ? 0 : index;
    }

    private bool IsStoryOverlayActive()
    {
        if (OpeningStoryManager.IsOpeningActive) return true;
        if (ChapterTitleManager.IsChapterTitleActive) return true;
        if (RestManager.IsRestingTransition) return true;
        if (CliffStoryManager.IsCliffStoryOpen) return true;
        return false;
    }

    private Color GetBackgroundColor(ToastKind kind)
    {
        if (kind == ToastKind.Success) return new Color(0.13f, 0.34f, 0.24f, 0.94f);
        if (kind == ToastKind.Warning) return new Color(0.42f, 0.25f, 0.08f, 0.94f);
        return new Color(0.16f, 0.19f, 0.23f, 0.94f);
    }

    private Text CreateText(Transform parent, string text, int fontSize, TextAnchor alignment, Color color)
    {
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text label = textObject.GetComponent<Text>();
        label.text = text;
        label.font = GetDefaultFont();
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = color;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        return label;
    }

    private Font GetDefaultFont()
    {
        if (cachedFont != null) return cachedFont;
        cachedFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 16);
        if (cachedFont != null) return cachedFont;
        cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return cachedFont;
    }

    private struct ToastRequest
    {
        public string message;
        public ToastKind kind;
    }

    private enum ToastKind
    {
        Normal,
        Success,
        Warning
    }
}

/// <summary>
/// 灵石变化的轻量封装。第一版只负责数值变化与 Toast，不接管复杂经济系统。
/// </summary>
public static class CurrencyManager
{
    public static void AddSpiritStones(PlayerState state, int amount)
    {
        if (state == null || amount <= 0) return;
        state.spiritStones += amount;
        ToastManager.TryShowSpiritStoneToast(amount);
    }

    public static bool SpendSpiritStones(PlayerState state, int amount)
    {
        if (state == null || amount <= 0) return false;
        if (state.spiritStones < amount)
        {
            ToastManager.TryShowWarning("灵石不足");
            return false;
        }
        state.spiritStones -= amount;
        ToastManager.TryShowCostToast("灵石", amount);
        return true;
    }
}

public static class ToastMessageUtility
{
    public static void TryShowCommonWarning(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        string text = message.Trim();
        if (ContainsAny(text, "行动点不足", "没有力气"))
        {
            ToastManager.TryShowWarning("行动点不足");
            return;
        }
        if (ContainsAny(text, "灵石不足", "灵石不够"))
        {
            ToastManager.TryShowWarning("灵石不足");
            return;
        }
        if (ContainsAny(text, "售罄", "卖完"))
        {
            ToastManager.TryShowWarning("商品售罄");
            return;
        }
        if (ContainsAny(text, "尚未掌握锻体", "尚未掌握锻体法门"))
        {
            ToastManager.TryShowWarning("你尚未掌握锻体法门");
            return;
        }
        if (ContainsAny(text, "缺少物品", "没有合适的工具"))
        {
            ToastManager.TryShowWarning(text);
            return;
        }
        if (ContainsAny(text, "不足", "不够", "无法", "不能", "不可", "请先", "缺少", "尚未"))
        {
            ToastManager.TryShowWarning(text);
        }
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        if (string.IsNullOrEmpty(text) || values == null) return false;
        foreach (string value in values)
        {
            if (!string.IsNullOrEmpty(value) && text.Contains(value)) return true;
        }
        return false;
    }
}
