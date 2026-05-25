using UnityEngine;
using UnityEngine.UI;

namespace TheFloorIsLava;

/// <summary>
/// Slim bottom-center health bar.
///   - Sits between the player's hands with a few pixels of margin from the
///     bottom of the screen.
///   - White 1px outline around a dark interior; red fill draws left-to-right.
///   - HP number floats above the bar; its colour fades from white (full) to
///     red (empty) so the player gets an obvious "danger" cue without us
///     having to mask through the fill image.
/// </summary>
internal sealed class HealthBar
{
    private GameObject? _root;
    private RectTransform? _container;
    private CanvasGroup? _group;
    private Image? _fill;
    private Text? _label;
    private bool _hidden;

    public RectTransform? Container => _container;
    public CanvasGroup? Group => _group;

    public void Show(float current, float max)
    {
        Build();
        if (_root == null) return;
        if (_hidden) { _root.SetActive(true); _hidden = false; }
        if (_fill == null || _label == null) return;

        var pct = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        _fill.fillAmount = pct;

        _label.text = Mathf.CeilToInt(Mathf.Max(0f, current)).ToString();
        // White when full, red as it depletes.
        _label.color = Color.Lerp(new Color(1f, 0.2f, 0.2f), Color.white, pct);
    }

    public void Hide()
    {
        if (_root != null && !_hidden)
        {
            _root.SetActive(false);
            _hidden = true;
        }
    }

    public void Destroy()
    {
        if (_root != null)
            Object.Destroy(_root);
        _root = null;
        _container = null;
        _group = null;
        _fill = null;
        _label = null;
        _hidden = false;
    }

    /// <summary>Reset the bar to its canonical position/scale/opacity. Used by
    /// the easter-egg animation when it finishes (so a fresh run shows a
    /// pristine bar).</summary>
    public void ResetTransform()
    {
        if (_container != null)
        {
            _container.anchoredPosition = new Vector2(0f, 16f);
            _container.localScale = Vector3.one;
            _container.localRotation = Quaternion.identity;
        }
        if (_group != null) _group.alpha = 1f;
    }

    private void Build()
    {
        if (_root != null) return;

        _root = new GameObject("TheFloorIsLava_HealthUI");
        Object.DontDestroyOnLoad(_root);

        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9500;
        var scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        var sprite = WhiteSprite();

        // Bottom-center container — fits between the visible hands and is
        // sized to the bar exactly. The label floats above it.
        const float barWidth = 280f;
        const float barHeight = 16f;

        var container = new GameObject("Container");
        container.transform.SetParent(_root.transform, false);
        _container = container.AddComponent<RectTransform>();
        _container.anchorMin = new Vector2(0.5f, 0f);
        _container.anchorMax = new Vector2(0.5f, 0f);
        _container.pivot = new Vector2(0.5f, 0f);
        _container.sizeDelta = new Vector2(barWidth, barHeight);
        _container.anchoredPosition = new Vector2(0f, 16f);
        _group = container.AddComponent<CanvasGroup>();
        _group.alpha = 1f;

        // Number label, drawn ABOVE the bar with a small gap.
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(container.transform, false);
        var labelR = labelGo.AddComponent<RectTransform>();
        labelR.anchorMin = new Vector2(0f, 1f);
        labelR.anchorMax = new Vector2(1f, 1f);
        labelR.pivot = new Vector2(0.5f, 0f);
        labelR.sizeDelta = new Vector2(0f, 20f);
        labelR.anchoredPosition = new Vector2(0f, 4f);
        _label = labelGo.AddComponent<Text>();
        _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _label.alignment = TextAnchor.LowerCenter;
        _label.fontStyle = FontStyle.Bold;
        _label.fontSize = 18;
        _label.color = Color.white;
        _label.horizontalOverflow = HorizontalWrapMode.Overflow;
        _label.verticalOverflow = VerticalWrapMode.Overflow;

        // White border = full-container white image. The dark interior + red
        // fill sit a few pixels inside, so white shows as a thin frame.
        var border = new GameObject("Border");
        border.transform.SetParent(container.transform, false);
        var borderR = border.AddComponent<RectTransform>();
        borderR.anchorMin = Vector2.zero;
        borderR.anchorMax = Vector2.one;
        borderR.offsetMin = Vector2.zero;
        borderR.offsetMax = Vector2.zero;
        var borderImg = border.AddComponent<Image>();
        borderImg.sprite = sprite;
        borderImg.color = new Color(1f, 1f, 1f, 0.95f);

        var inner = new GameObject("Inner");
        inner.transform.SetParent(border.transform, false);
        var innerR = inner.AddComponent<RectTransform>();
        innerR.anchorMin = Vector2.zero;
        innerR.anchorMax = Vector2.one;
        innerR.offsetMin = new Vector2(1.5f, 1.5f);
        innerR.offsetMax = new Vector2(-1.5f, -1.5f);
        var innerImg = inner.AddComponent<Image>();
        innerImg.sprite = sprite;
        innerImg.color = new Color(0.05f, 0.05f, 0.07f, 0.95f);

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(inner.transform, false);
        var fillR = fillGo.AddComponent<RectTransform>();
        fillR.anchorMin = Vector2.zero;
        fillR.anchorMax = Vector2.one;
        fillR.offsetMin = Vector2.zero;
        fillR.offsetMax = Vector2.zero;
        _fill = fillGo.AddComponent<Image>();
        _fill.sprite = sprite;
        _fill.color = new Color(0.92f, 0.13f, 0.10f, 1f);
        _fill.type = Image.Type.Filled;
        _fill.fillMethod = Image.FillMethod.Horizontal;
        _fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        _fill.fillAmount = 1f;
    }

    private static Sprite? _white;
    private static Sprite WhiteSprite()
    {
        if (_white != null) return _white;
        var t = new Texture2D(1, 1) { name = "TheFloorIsLava_WhitePixel" };
        t.SetPixel(0, 0, Color.white);
        t.Apply();
        _white = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        return _white;
    }
}
