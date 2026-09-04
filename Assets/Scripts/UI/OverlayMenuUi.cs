using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared look for pause / result overlays so they stay visually in sync.
/// </summary>
public static class OverlayMenuUi
{
    public static readonly Color DimColor = new Color(0f, 0f, 0f, 0.75f);
    public static readonly Color ButtonColor = new Color(0.15f, 0.15f, 0.2f, 0.95f);
    public static readonly Color ButtonHighlighted = new Color(0.3f, 0.3f, 0.4f, 1f);
    public static readonly Color ButtonPressed = new Color(0.1f, 0.1f, 0.15f, 1f);
    public static readonly Color ClearColor = new Color(0.4f, 1f, 0.5f);
    public static readonly Color GameOverColor = new Color(1f, 0.35f, 0.3f);
    public static readonly Color PausedColor = new Color(1f, 0.9f, 0.45f);

    public static readonly Vector2 ButtonSize = new Vector2(280f, 80f);
    public static readonly Vector2 TitleSize = new Vector2(1200f, 130f);
    public static readonly Vector2 SubtitleSize = new Vector2(1200f, 60f);

    public const float TitleFontSize = 96f;
    public const float SubtitleFontSize = 36f;
    public const float ButtonLabelFontSize = 34f;

    public const int PauseSortingOrder = 400;
    public const int ResultSortingOrder = 500;

    public static GameObject CreateOverlayRoot(string name, int sortingOrder)
    {
        var root = new GameObject(name);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        canvas.additionalShaderChannels =
            AdditionalCanvasShaderChannels.TexCoord1
            | AdditionalCanvasShaderChannels.Normal
            | AdditionalCanvasShaderChannels.Tangent;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        root.AddComponent<GraphicRaycaster>();
        return root;
    }

    public static Image CreateImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    public static TMP_Text CreateText(
        Transform parent,
        string name,
        string text,
        float fontSize,
        Color color,
        TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
            tmp.font = font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    public static Button CreateButton(
        Transform parent,
        string name,
        string label,
        Vector2 anchoredPosition,
        TMP_FontAsset font)
    {
        Image background = CreateImage(parent, name, ButtonColor);
        Place(background.rectTransform, anchoredPosition, ButtonSize);

        var button = background.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = ButtonHighlighted;
        colors.pressedColor = ButtonPressed;
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        TMP_Text text = CreateText(background.transform, "Label", label, ButtonLabelFontSize, Color.white, font);
        text.enableAutoSizing = true;
        text.fontSizeMin = 18f;
        text.fontSizeMax = ButtonLabelFontSize;
        Stretch(text.rectTransform);

        return button;
    }

    public static void Place(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
