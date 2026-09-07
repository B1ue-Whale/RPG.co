using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class LevelSelectionController : MonoBehaviour
{
    [System.Serializable]
    public class LevelEntry
    {
        public string label = "Level 1";
        public string sceneName = string.Empty;
    }

    [Header("Levels")]
    [SerializeField] private List<LevelEntry> levels = new List<LevelEntry>();

    [Header("Look")]
    [Tooltip("Leave empty to use TextMesh Pro's default.")]
    [SerializeField] private TMP_FontAsset font;

    [Header("Background")]
    [Tooltip("Preferred. Import the file as Sprite (2D and UI), then drop it here.")]
    [SerializeField] private Sprite sprite;

    [Tooltip("Use this if the file is imported as a Default texture instead of a Sprite.")]
    [SerializeField] private Texture texture;

    [SerializeField] private Color tint = Color.white;
    [SerializeField] private int sortingOrder = -10;

    private GameObject backgroundRoot;
    private GameObject overlayRoot;
    private TMP_FontAsset resolvedFont;
    private Button firstLevelButton;

    private static readonly LevelEntry[] DefaultLevels =
    {
        new LevelEntry { label = "Level 1", sceneName = "World1_Level 1" },
        new LevelEntry { label = "Level 2", sceneName = "World1_Level 2" },
        new LevelEntry { label = "Level 3", sceneName = "World1_Level 3" }
    };

    private void Awake()
    {
        Texture resolved = ResolveTexture();
        if (resolved == null)
        {
            Debug.LogWarning("[LevelSelectionController] Assign a Sprite or Texture for the background.");
            return;
        }

        BuildBackground(resolved);
    }

    private void Start()
    {
        resolvedFont = font != null ? font : TMP_Settings.defaultFontAsset;
        EnsureDefaultLevels();
        HideLegacyButtons();
        BuildOverlay();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            GoBackToMainMenu();
    }

    private void OnDestroy()
    {
        if (backgroundRoot != null)
            Destroy(backgroundRoot);
        if (overlayRoot != null)
            Destroy(overlayRoot);
    }

    public void GoBackToMainMenu()
    {
        LevelTransition.GoToMain();
    }

    private void EnsureDefaultLevels()
    {
        if (levels == null)
            levels = new List<LevelEntry>();

        for (int i = 0; i < DefaultLevels.Length; i++)
        {
            if (i >= levels.Count)
                levels.Add(new LevelEntry());

            LevelEntry entry = levels[i];
            if (entry == null)
            {
                entry = new LevelEntry();
                levels[i] = entry;
            }

            if (string.IsNullOrWhiteSpace(entry.label))
                entry.label = DefaultLevels[i].label;
            if (string.IsNullOrWhiteSpace(entry.sceneName))
                entry.sceneName = DefaultLevels[i].sceneName;
        }
    }

    private void HideLegacyButtons()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
                continue;

            if (!IsLegacyLevelSelectButton(button))
                continue;

            Canvas canvas = button.GetComponentInParent<Canvas>();
            if (canvas != null
                && canvas.gameObject != overlayRoot
                && canvas.gameObject != backgroundRoot)
            {
                canvas.gameObject.SetActive(false);
                return;
            }

            button.gameObject.SetActive(false);
        }
    }

    private static bool IsLegacyLevelSelectButton(Button button)
    {
        string name = button.name;
        if (name == "World1Level1" || name == "World1Level2" || name == "World1Level3" || name == "Back")
            return true;

        TMP_Text tmp = button.GetComponentInChildren<TMP_Text>();
        if (tmp == null)
            return false;

        return tmp.text == "World1Level1" || tmp.text == "Back";
    }

    private void BuildOverlay()
    {
        overlayRoot = OverlayMenuUi.CreateOverlayRoot(
            "LevelSelectButtons",
            OverlayMenuUi.LevelSelectionSortingOrder);

        Image dim = OverlayMenuUi.CreateImage(
            overlayRoot.transform,
            "Dim",
            OverlayMenuUi.LevelSelectDimColor);
        OverlayMenuUi.Stretch(dim.rectTransform);

        TMP_Text title = OverlayMenuUi.CreateText(
            overlayRoot.transform,
            "Title",
            "Stage Select",
            OverlayMenuUi.TitleFontSize,
            OverlayMenuUi.PausedColor,
            resolvedFont);
        title.outlineWidth = 0.25f;
        title.outlineColor = new Color(0f, 0f, 0f, 0.85f);
        OverlayMenuUi.Place(title.rectTransform, new Vector2(0f, 320f), OverlayMenuUi.TitleSize);

        int count = levels.Count;
        float spacing = OverlayMenuUi.LevelButtonSize.x + 48f;
        float startX = -((count - 1) * spacing) * 0.5f;

        for (int i = 0; i < count; i++)
        {
            LevelEntry level = levels[i];
            if (level == null || string.IsNullOrWhiteSpace(level.sceneName))
            {
                Debug.LogWarning("[LevelSelectionController] A level entry has an empty scene name.");
                continue;
            }

            string label = string.IsNullOrWhiteSpace(level.label)
                ? $"Level {i + 1}"
                : level.label;
            Vector2 position = new Vector2(startX + i * spacing, 20f);
            Button button = OverlayMenuUi.CreateButton(
                overlayRoot.transform,
                $"LevelButton_{i + 1}",
                label,
                position,
                resolvedFont,
                OverlayMenuUi.LevelButtonSize,
                OverlayMenuUi.LevelButtonLabelFontSize);

            ApplyLabelOutline(button);

            string sceneName = level.sceneName;
            button.onClick.AddListener(() => LevelTransition.EnterLevel(sceneName));

            if (firstLevelButton == null)
                firstLevelButton = button;
        }

        Button back = OverlayMenuUi.CreateButton(
            overlayRoot.transform,
            "BackButton",
            "Back",
            new Vector2(0f, -280f),
            resolvedFont,
            OverlayMenuUi.LevelBackButtonSize,
            OverlayMenuUi.ButtonLabelFontSize + 10f);
        ApplyLabelOutline(back);
        back.onClick.AddListener(GoBackToMainMenu);

        if (EventSystem.current != null && firstLevelButton != null)
            EventSystem.current.SetSelectedGameObject(firstLevelButton.gameObject);
    }

    private static void ApplyLabelOutline(Button button)
    {
        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        if (label == null)
            return;

        label.outlineWidth = 0.2f;
        label.outlineColor = new Color(0f, 0f, 0f, 0.85f);
    }

    private Texture ResolveTexture()
    {
        if (sprite != null)
            return sprite.texture;
        return texture;
    }

    private void BuildBackground(Texture resolved)
    {
        backgroundRoot = new GameObject("LevelSelectBackground");

        var canvas = backgroundRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = backgroundRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var fill = new GameObject("Fill", typeof(RectTransform));
        fill.transform.SetParent(backgroundRoot.transform, false);
        OverlayMenuUi.Stretch(fill.GetComponent<RectTransform>());

        var imageGo = new GameObject(
            "Image",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage),
            typeof(AspectRatioFitter));
        imageGo.transform.SetParent(fill.transform, false);

        var rawImage = imageGo.GetComponent<RawImage>();
        rawImage.texture = resolved;
        rawImage.color = tint;
        rawImage.raycastTarget = false;

        if (sprite != null)
        {
            Rect rect = sprite.textureRect;
            float texW = resolved.width;
            float texH = resolved.height;
            if (texW > 0f && texH > 0f)
                rawImage.uvRect = new Rect(
                    rect.x / texW,
                    rect.y / texH,
                    rect.width / texW,
                    rect.height / texH);
        }

        var aspectFitter = imageGo.GetComponent<AspectRatioFitter>();
        aspectFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        aspectFitter.aspectRatio = ResolveAspect(resolved);
    }

    private float ResolveAspect(Texture resolved)
    {
        if (sprite != null && sprite.rect.height > 0f)
            return sprite.rect.width / sprite.rect.height;

        if (resolved != null && resolved.height > 0)
            return (float)resolved.width / resolved.height;

        return 16f / 9f;
    }
}
