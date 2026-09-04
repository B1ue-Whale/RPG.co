using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Full-screen overlay on the main menu for picking a world.
/// World 1 loads the level-select scene.
/// </summary>
public class WorldSelectionController : MonoBehaviour
{
    [Serializable]
    public class WorldEntry
    {
        public string label = "World 1";
        public string levelSelectSceneName = "LevelSelectScene";
        public bool unlocked = true;
    }

    [Header("Worlds")]
    [SerializeField] private List<WorldEntry> worlds = new List<WorldEntry>
    {
        new WorldEntry()
    };

    [Header("Look")]
    [Tooltip("Leave empty to use TextMesh Pro's default.")]
    [SerializeField] private TMP_FontAsset font;

    private GameObject overlayRoot;
    private Button firstWorldButton;
    private TMP_FontAsset resolvedFont;
    private bool isOpen;

    public event Action Closed;

    public bool IsOpen => isOpen;

    private void Start()
    {
        resolvedFont = font != null ? font : TMP_Settings.defaultFontAsset;
        EnsureDefaultWorlds();
    }

    private void OnDestroy()
    {
        if (overlayRoot != null)
            Destroy(overlayRoot);
    }

    private void Update()
    {
        if (!isOpen || Keyboard.current == null)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }

    public void Open()
    {
        if (isOpen)
            return;

        if (overlayRoot == null)
        {
            resolvedFont = font != null ? font : TMP_Settings.defaultFontAsset;
            EnsureDefaultWorlds();
            BuildOverlay();
        }

        isOpen = true;
        overlayRoot.SetActive(true);

        if (GameManager.Instance != null)
            GameManager.Instance.ChangeState(Enums.GameState.WorldSelection);

        if (EventSystem.current != null && firstWorldButton != null)
            EventSystem.current.SetSelectedGameObject(firstWorldButton.gameObject);
    }

    public void Close()
    {
        if (!isOpen)
            return;

        isOpen = false;

        if (overlayRoot != null)
            overlayRoot.SetActive(false);

        if (GameManager.Instance != null)
            GameManager.Instance.ChangeState(Enums.GameState.Main);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        Closed?.Invoke();
    }

    private void SelectWorld(WorldEntry world)
    {
        if (world == null || !world.unlocked)
            return;

        string sceneName = string.IsNullOrWhiteSpace(world.levelSelectSceneName)
            ? "LevelSelectScene"
            : world.levelSelectSceneName;

        LevelTransition.GoToLevelSelection(sceneName);
    }

    private void EnsureDefaultWorlds()
    {
        if (worlds != null && worlds.Count > 0)
            return;

        worlds = new List<WorldEntry> { new WorldEntry() };
    }

    private void BuildOverlay()
    {
        overlayRoot = OverlayMenuUi.CreateOverlayRoot(
            "WorldSelectionOverlay",
            OverlayMenuUi.WorldSelectionSortingOrder);
        overlayRoot.SetActive(false);

        Image dim = OverlayMenuUi.CreateImage(overlayRoot.transform, "Dim", OverlayMenuUi.WorldSelectDimColor);
        OverlayMenuUi.Stretch(dim.rectTransform);

        TMP_Text title = OverlayMenuUi.CreateText(
            overlayRoot.transform,
            "Title",
            "World Select",
            OverlayMenuUi.TitleFontSize,
            OverlayMenuUi.PausedColor,
            resolvedFont);
        OverlayMenuUi.Place(title.rectTransform, new Vector2(0f, 280f), OverlayMenuUi.TitleSize);

        int count = worlds.Count;
        float spacing = OverlayMenuUi.WorldButtonSize.x + 48f;
        float startX = -((count - 1) * spacing) * 0.5f;

        for (int i = 0; i < count; i++)
        {
            WorldEntry world = worlds[i];
            Vector2 position = new Vector2(startX + i * spacing, 20f);
            Button button = OverlayMenuUi.CreateButton(
                overlayRoot.transform,
                $"WorldButton_{i}",
                world.label,
                position,
                resolvedFont,
                OverlayMenuUi.WorldButtonSize,
                OverlayMenuUi.WorldButtonLabelFontSize);

            if (i == 0)
                firstWorldButton = button;

            if (world.unlocked)
            {
                WorldEntry selected = world;
                button.onClick.AddListener(() => SelectWorld(selected));
            }
            else
            {
                button.interactable = false;
            }
        }

        Button back = OverlayMenuUi.CreateButton(
            overlayRoot.transform,
            "BackButton",
            "Back",
            new Vector2(0f, -220f),
            resolvedFont,
            OverlayMenuUi.WorldBackButtonSize,
            OverlayMenuUi.ButtonLabelFontSize + 10f);
        back.onClick.AddListener(Close);
    }
}
