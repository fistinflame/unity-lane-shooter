using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public class UIController : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private Text soldierCountText;
    [SerializeField] private Text fireRateText;
    [SerializeField] private Text fireRateBoostDurationText;
    [SerializeField] private Text piercingDurationText;
    [SerializeField] private Text stateText;
    [SerializeField] private Image bossHpFill;
    [SerializeField] private RectTransform bossHpFillRect;
    [SerializeField] private float bossHpInnerWidth = 496f;
    [SerializeField] private GameObject victoryMenuRoot;
    [SerializeField] private Text victoryTitleText;
    [SerializeField] private Button replayButton;
    [SerializeField] private Button quitButton;

    public void BuildIfNeeded()
    {
        if (canvas != null)
        {
            EnsureRunInfoTexts();
            EnsureEventSystem();
            return;
        }

        var canvasGo = new GameObject("UI");
        canvasGo.transform.SetParent(transform, false);

        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        soldierCountText = CreateText(
            "SoldierCountText",
            canvas.transform,
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(16f, 14f),
            24,
            TextAnchor.LowerLeft,
            "Soldiers: 0");

        EnsureRunInfoTexts();

        stateText = CreateText(
            "StateText",
            canvas.transform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            56,
            TextAnchor.MiddleCenter,
            string.Empty);

        bossHpFill = CreateBar("BossHpBar", canvas.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(420f, 12f), new Color(0.9f, 0.25f, 0.25f));
        bossHpFillRect = bossHpFill != null ? bossHpFill.rectTransform : null;
        if (bossHpFillRect != null)
        {
            bossHpInnerWidth = bossHpFillRect.sizeDelta.x;
        }

        CreateVictoryMenu(canvas.transform);
        EnsureEventSystem();

        SetBossHp(1f);
        SetBossVisible(false);
        HideVictoryMenu();
    }

    public void SetSoldierCount(int count)
    {
        if (soldierCountText != null)
        {
            soldierCountText.text = "Soldiers: " + count;
        }
    }

    public void SetPlayerHealth(int current, int maxIgnored)
    {
        // HP label removed per request.
    }

    public void SetFireRate(float shotsPerSecond)
    {
        if (fireRateText != null)
        {
            fireRateText.text = "Fire Rate: " + Mathf.Max(0f, shotsPerSecond).ToString("0.0") + "/s";
        }
    }

    public void SetPiercingDuration(float secondsRemaining)
    {
        if (piercingDurationText == null)
        {
            return;
        }

        float clamped = Mathf.Max(0f, secondsRemaining);
        bool visible = clamped > 0.01f;
        piercingDurationText.gameObject.SetActive(visible);
        if (visible)
        {
            piercingDurationText.text = "Piercing: " + clamped.ToString("0.0") + "s";
        }
    }

    public void SetFireRateBoostDuration(float secondsRemaining)
    {
        if (fireRateBoostDurationText == null)
        {
            return;
        }

        float clamped = Mathf.Max(0f, secondsRemaining);
        bool visible = clamped > 0.01f;
        fireRateBoostDurationText.gameObject.SetActive(visible);
        if (visible)
        {
            fireRateBoostDurationText.text = "Fire Boost: " + clamped.ToString("0.0") + "s";
        }
    }

    public void SetProgress(float normalized)
    {
        // Progress bar intentionally removed.
    }

    public void SetBossHp(float normalized)
    {
        if (bossHpFill == null)
        {
            return;
        }

        if (bossHpFillRect == null)
        {
            bossHpFillRect = bossHpFill.rectTransform;
            if (bossHpFillRect != null && bossHpFillRect.sizeDelta.x > 1f)
            {
                bossHpInnerWidth = bossHpFillRect.sizeDelta.x;
            }
        }

        if (bossHpFillRect == null)
        {
            return;
        }

        float clamped = Mathf.Clamp01(normalized);
        var size = bossHpFillRect.sizeDelta;
        size.x = bossHpInnerWidth * clamped;
        bossHpFillRect.sizeDelta = size;
        bossHpFill.enabled = clamped > 0.0001f;
    }

    public void SetBossVisible(bool visible)
    {
        if (bossHpFill != null)
        {
            bossHpFill.transform.parent.gameObject.SetActive(visible);
        }
    }

    public void SetState(string value)
    {
        if (stateText != null)
        {
            stateText.text = value;
        }
    }

    public void ShowVictoryMenu(Action onReplay, Action onQuit)
    {
        ShowEndMenu(true, onReplay, onQuit);
    }

    public void ShowEndMenu(bool isVictory, Action onReplay, Action onQuit)
    {
        if (victoryMenuRoot == null)
        {
            return;
        }

        if (victoryTitleText != null)
        {
            victoryTitleText.text = isVictory ? "VICTORY" : "DEFEAT";
            victoryTitleText.color = isVictory ? new Color(0.42f, 1f, 0.45f) : new Color(1f, 0.34f, 0.34f);
        }

        victoryMenuRoot.SetActive(true);

        if (replayButton != null)
        {
            replayButton.onClick.RemoveAllListeners();
            replayButton.onClick.AddListener(() => onReplay?.Invoke());
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(() => onQuit?.Invoke());
        }
    }

    public void HideVictoryMenu()
    {
        if (victoryMenuRoot != null)
        {
            victoryMenuRoot.SetActive(false);
        }
    }

    private void CreateVictoryMenu(Transform parent)
    {
        victoryMenuRoot = new GameObject("VictoryMenu");
        victoryMenuRoot.transform.SetParent(parent, false);

        var rootRect = victoryMenuRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(420f, 260f);

        var rootBg = victoryMenuRoot.AddComponent<Image>();
        rootBg.color = new Color(0f, 0f, 0f, 0.75f);

        victoryTitleText = CreateText(
            "VictoryLabel",
            victoryMenuRoot.transform,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -28f),
            40,
            TextAnchor.MiddleCenter,
            "VICTORY");

        replayButton = CreateButton(victoryMenuRoot.transform, "ReplayButton", "Replay", new Vector2(0f, -110f));
        quitButton = CreateButton(victoryMenuRoot.transform, "QuitButton", "Quit", new Vector2(0f, -180f));
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(220f, 48f);
        rect.anchoredPosition = anchoredPosition;

        var image = go.AddComponent<Image>();
        image.color = new Color(0.25f, 0.25f, 0.25f, 1f);

        var button = go.AddComponent<Button>();

        var text = CreateText(
            "Label",
            go.transform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            26,
            TextAnchor.MiddleCenter,
            label);

        text.rectTransform.sizeDelta = rect.sizeDelta;

        return button;
    }

    private static Text CreateText(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPos,
        int fontSize,
        TextAnchor anchor,
        string initialText)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(420f, 56f);

        var text = go.AddComponent<Text>();
        text.font = GetRuntimeFont();
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = anchor;
        text.text = initialText;

        return text;
    }

    private static Font GetRuntimeFont()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return font;
    }

    private static Image CreateBar(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPos,
        Vector2 size,
        Color fillColor)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);

        var rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = anchorMin;
        rootRect.anchorMax = anchorMax;
        rootRect.pivot = pivot;
        rootRect.anchoredPosition = anchoredPos;
        rootRect.sizeDelta = size;

        var bg = root.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.6f);

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(root.transform, false);

        var fillRect = fillGo.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = new Vector2(2f, 0f);
        fillRect.sizeDelta = new Vector2(size.x - 4f, size.y - 4f);

        var fill = fillGo.AddComponent<Image>();
        fill.color = fillColor;
        fill.type = Image.Type.Simple;

        return fill;
    }

    private void EnsureRunInfoTexts()
    {
        if (canvas == null)
        {
            return;
        }

        if (fireRateText == null)
        {
            fireRateText = CreateText(
                "FireRateText",
                canvas.transform,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(16f, 42f),
                20,
                TextAnchor.LowerLeft,
                "Fire Rate: 0.0/s");
            fireRateText.color = new Color(1f, 0.94f, 0.75f);
        }

        if (piercingDurationText == null)
        {
            piercingDurationText = CreateText(
                "PiercingDurationText",
                canvas.transform,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(16f, 68f),
                20,
                TextAnchor.LowerLeft,
                string.Empty);
            piercingDurationText.color = new Color(0.55f, 1f, 0.55f);
            piercingDurationText.gameObject.SetActive(false);
        }

        if (fireRateBoostDurationText == null)
        {
            fireRateBoostDurationText = CreateText(
                "FireRateBoostDurationText",
                canvas.transform,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(16f, 94f),
                20,
                TextAnchor.LowerLeft,
                string.Empty);
            fireRateBoostDurationText.color = new Color(1f, 0.82f, 0.26f);
            fireRateBoostDurationText.gameObject.SetActive(false);
        }
    }

    private static void EnsureEventSystem()
    {
        var existing = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
        if (existing != null)
        {
            return;
        }

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }
}
