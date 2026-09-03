using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

/// <summary>
/// Screen-corner minimap. Bakes a simplified image of the level tilemaps once at
/// startup (ground + hideable layers), then redraws live markers every frame:
/// green dot = player, blue dot = NPC, magenta cells = bug-infected tiles.
/// Holding the expand key (TAB by default) enlarges the map to fill the screen.
///
/// Builds its own screen-space canvas at runtime and finds every scene reference
/// automatically when the inspector fields are left empty, so it works by simply
/// existing in the scene (see MinimapBootstrap at the bottom of this file).
/// Assumes 1x1 world-unit cells, which all level grids currently use.
/// </summary>
public class MinimapController : MonoBehaviour
{
    [Header("Map Sources (found automatically when left empty)")]
    [Tooltip("Tilemaps drawn as the level layout. Defaults to every tilemap with a TilemapCollider2D.")]
    [SerializeField] private Tilemap[] layoutTilemaps;
    [Tooltip("Hideable layer, drawn in its own color. Defaults to the BugZone's target tilemap.")]
    [SerializeField] private Tilemap hideableTilemap;
    [SerializeField] private BugZone bugZone;

    [Header("Resolution / Layout")]
    [Tooltip("Texture pixels per tilemap cell.")]
    [SerializeField] private int pixelsPerCell = 4;
    [Tooltip("Radius of the player/NPC dots, in texture pixels.")]
    [SerializeField] private int dotRadius = 5;
    [Tooltip("The minimap is scaled to fit inside this on-screen rectangle (reference 1920x1080).")]
    [SerializeField] private Vector2 maxDisplaySize = new Vector2(510f, 300f);
    [SerializeField] private Vector2 screenMargin = new Vector2(16f, 16f);
    [SerializeField] private float framePadding = 6f;

    [Header("Fullscreen (hold key)")]
    [Tooltip("While this key is held the minimap expands to fill the screen.")]
    [SerializeField] private Key expandKey = Key.Tab;
    [Tooltip("How fast the minimap grows/shrinks, in transitions per second.")]
    [SerializeField] private float expandSpeed = 6f;
    [Tooltip("Screen border kept around the map while fullscreen.")]
    [SerializeField] private Vector2 fullscreenMargin = new Vector2(40f, 40f);

    [Header("Colors")]
    [SerializeField] private Color frameColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color emptyColor = new Color(0.05f, 0.05f, 0.08f, 0.85f);
    [SerializeField] private Color groundColor = new Color(0.6f, 0.6f, 0.65f, 1f);
    [SerializeField] private Color hideableColor = new Color(0.55f, 0.5f, 0.32f, 1f);
    [SerializeField] private Color infectedColor = new Color(1f, 0f, 1f, 1f);
    [SerializeField] private Color playerColor = new Color(0.2f, 1f, 0.25f, 1f);
    [SerializeField] private Color npcColor = new Color(0.25f, 0.55f, 1f, 1f);

    [Header("Crash Danger Marker")]
    [Tooltip("Color for infected cells currently contributing to the Crash Gauge (BugZone.CrashContributingCells). Pulses between this and infectedColor.")]
    [SerializeField] private Color crashDangerColor = new Color(1f, 0.15f, 0.1f, 1f);
    [Tooltip("Pulse cycles per second.")]
    [SerializeField] private float dangerPulseSpeed = 2.5f;
    [Tooltip("How far the pulse dips back toward infectedColor (0 = no pulse, stays at crashDangerColor; 1 = pulses all the way down to infectedColor).")]
    [SerializeField, Range(0f, 1f)] private float dangerPulseIntensity = 0.5f;
    [Tooltip("Strength of the 1-pixel outer-edge size pulse at its peak (synced with the color pulse), as a blend factor against whatever is already drawn there. 0 disables it.")]
    [SerializeField, Range(0f, 1f)] private float dangerSizePulseIntensity = 0.5f;

    private const int MaxTextureDimension = 2048;

    private Texture2D _texture;
    private Color32[] _basePixels;   // baked layout, never changes
    private Color32[] _framePixels;  // base + live markers, rebuilt every frame
    private int _texWidth;
    private int _texHeight;
    private Vector2 _worldMin;       // world position of the texture's bottom-left corner
    private float _pixelsPerUnit;

    private Transform _player;
    private NpcCommandPlayback[] _npcs;

    private RectTransform _canvasRect;
    private RectTransform _frameRect;
    private float _expandBlend; // 0 = corner minimap, 1 = fullscreen

    private void Start()
    {
        ResolveSceneReferences();

        if (!BakeLayout())
        {
            Debug.LogWarning("[Minimap] No tilemaps found to draw - minimap disabled.", this);
            enabled = false;
            return;
        }

        BuildUi();
    }

    private void ResolveSceneReferences()
    {
        if (bugZone == null)
            bugZone = FindFirstObjectByType<BugZone>();

        if (hideableTilemap == null && bugZone != null)
            hideableTilemap = bugZone.TargetTilemap;

        if (layoutTilemaps == null || layoutTilemaps.Length == 0)
        {
            var colliders = FindObjectsByType<TilemapCollider2D>(FindObjectsSortMode.None);
            var maps = new List<Tilemap>();
            foreach (TilemapCollider2D collider in colliders)
            {
                Tilemap map = collider.GetComponent<Tilemap>();
                if (map != null && map != hideableTilemap)
                    maps.Add(map);
            }
            layoutTilemaps = maps.ToArray();
        }

        _npcs = FindObjectsByType<NpcCommandPlayback>(FindObjectsSortMode.None);
    }

    // ---------------------------------------------------------------- baking

    private bool BakeLayout()
    {
        List<Vector2> groundCenters = CollectCellCenters(layoutTilemaps);
        List<Vector2> hideCenters = CollectCellCenters(new[] { hideableTilemap });

        if (groundCenters.Count == 0 && hideCenters.Count == 0)
            return false;

        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        foreach (Vector2 c in groundCenters) { min = Vector2.Min(min, c); max = Vector2.Max(max, c); }
        foreach (Vector2 c in hideCenters) { min = Vector2.Min(min, c); max = Vector2.Max(max, c); }

        // Half a cell to reach the outer edge of border cells, plus one cell of breathing room.
        _worldMin = min - new Vector2(1.5f, 1.5f);
        Vector2 worldSize = (max + new Vector2(1.5f, 1.5f)) - _worldMin;

        int largestSide = Mathf.CeilToInt(Mathf.Max(worldSize.x, worldSize.y));
        _pixelsPerUnit = Mathf.Clamp(pixelsPerCell, 1, Mathf.Max(1, MaxTextureDimension / largestSide));

        _texWidth = Mathf.CeilToInt(worldSize.x * _pixelsPerUnit);
        _texHeight = Mathf.CeilToInt(worldSize.y * _pixelsPerUnit);

        _basePixels = new Color32[_texWidth * _texHeight];
        _framePixels = new Color32[_texWidth * _texHeight];

        Color32 empty = emptyColor;
        for (int i = 0; i < _basePixels.Length; i++)
            _basePixels[i] = empty;

        foreach (Vector2 center in groundCenters)
            FillCell(_basePixels, center, groundColor);
        foreach (Vector2 center in hideCenters)
            FillCell(_basePixels, center, hideableColor);

        _texture = new Texture2D(_texWidth, _texHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        _texture.SetPixels32(_basePixels);
        _texture.Apply(false);
        return true;
    }

    private static List<Vector2> CollectCellCenters(Tilemap[] maps)
    {
        var centers = new List<Vector2>();
        if (maps == null)
            return centers;

        foreach (Tilemap map in maps)
        {
            if (map == null)
                continue;

            map.CompressBounds();
            foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
            {
                if (map.HasTile(cell))
                    centers.Add(map.GetCellCenterWorld(cell));
            }
        }
        return centers;
    }

    // ------------------------------------------------------------- per frame

    private void LateUpdate()
    {
        if (_texture == null)
            return;

        UpdateExpansion();

        System.Array.Copy(_basePixels, _framePixels, _basePixels.Length);

        DrawInfectedCells();
        DrawCrashDangerCells();
        DrawNpcDots();
        DrawPlayerDot();

        _texture.SetPixels32(_framePixels);
        _texture.Apply(false);
    }

    private void DrawInfectedCells()
    {
        if (bugZone == null)
            return;

        IReadOnlyList<Vector3Int> cells = bugZone.InfectedCells;
        for (int i = 0; i < cells.Count; i++)
            FillCell(_framePixels, bugZone.GetCellWorldCenter(cells[i]), infectedColor);
    }

    /// <summary>
    /// Overlays infected cells that BugZone reports as currently contributing to the
    /// Crash Gauge, in a brighter/pulsing color layered on top of the normal infected
    /// fill. Reads BugZone.CrashContributingCells only - no age/proximity logic here.
    /// </summary>
    private void DrawCrashDangerCells()
    {
        if (bugZone == null)
            return;

        IReadOnlyCollection<Vector3Int> cells = bugZone.CrashContributingCells;
        if (cells.Count == 0)
            return;

        // 0 at the pulse peak (most saturated crashDangerColor), 1 at the trough
        // (faded back toward infectedColor) - the size pulse is derived from the same
        // phase so the edge expansion peaks together with the color.
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * dangerPulseSpeed * Mathf.PI * 2f);
        Color32 pulseColor = Color.Lerp(crashDangerColor, infectedColor, pulse * dangerPulseIntensity);
        float sizePulseStrength = (1f - pulse) * dangerSizePulseIntensity;

        foreach (Vector3Int cell in cells)
        {
            Vector2 worldCenter = bugZone.GetCellWorldCenter(cell);
            FillCell(_framePixels, worldCenter, pulseColor);
            DrawCellOutline(_framePixels, worldCenter, pulseColor, sizePulseStrength);
        }
    }

    /// <summary>
    /// Subtle size-pulse effect: blends a 1-pixel ring just outside the cell's normal
    /// footprint toward `color`, by `strength` (0 = invisible, 1 = fully color). Never
    /// resizes the cell itself - FillCell's block stays exactly as it always has been.
    /// </summary>
    private void DrawCellOutline(Color32[] pixels, Vector2 worldCenter, Color32 color, float strength)
    {
        if (strength <= 0f)
            return;

        Vector2Int origin = WorldToPixel(worldCenter - new Vector2(0.5f, 0.5f));
        int size = Mathf.Max(1, Mathf.RoundToInt(_pixelsPerUnit));

        for (int x = -1; x <= size; x++)
        {
            BlendPixel(pixels, origin.x + x, origin.y - 1, color, strength);
            BlendPixel(pixels, origin.x + x, origin.y + size, color, strength);
        }
        for (int y = 0; y < size; y++)
        {
            BlendPixel(pixels, origin.x - 1, origin.y + y, color, strength);
            BlendPixel(pixels, origin.x + size, origin.y + y, color, strength);
        }
    }

    private void DrawNpcDots()
    {
        if (_npcs == null)
            return;

        foreach (NpcCommandPlayback npc in _npcs)
        {
            if (npc == null)
                continue;

            Vector2 position = npc.Body != null ? npc.Body.position : (Vector2)npc.transform.position;
            DrawDot(_framePixels, position, npcColor);
        }
    }

    private void DrawPlayerDot()
    {
        if (_player == null)
        {
            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo == null)
                return;
            _player = playerGo.transform;
        }

        DrawDot(_framePixels, _player.position, playerColor);
    }

    // ------------------------------------------------------------ rasterizing

    private Vector2Int WorldToPixel(Vector2 world)
    {
        return new Vector2Int(
            Mathf.FloorToInt((world.x - _worldMin.x) * _pixelsPerUnit),
            Mathf.FloorToInt((world.y - _worldMin.y) * _pixelsPerUnit));
    }

    private void FillCell(Color32[] pixels, Vector2 worldCenter, Color32 color)
    {
        Vector2Int origin = WorldToPixel(worldCenter - new Vector2(0.5f, 0.5f));
        int size = Mathf.Max(1, Mathf.RoundToInt(_pixelsPerUnit));
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                SetPixel(pixels, origin.x + x, origin.y + y, color);
    }

    private void DrawDot(Color32[] pixels, Vector2 world, Color32 color)
    {
        Vector2Int center = WorldToPixel(world);
        int sqrRadius = dotRadius * dotRadius;
        for (int dy = -dotRadius; dy <= dotRadius; dy++)
            for (int dx = -dotRadius; dx <= dotRadius; dx++)
                if (dx * dx + dy * dy <= sqrRadius)
                    SetPixel(pixels, center.x + dx, center.y + dy, color);
    }

    private void SetPixel(Color32[] pixels, int x, int y, Color32 color)
    {
        if (x < 0 || x >= _texWidth || y < 0 || y >= _texHeight)
            return;
        pixels[y * _texWidth + x] = color;
    }

    /// <summary>Bounds-safe: blends `color` into the existing pixel by `t` instead of
    /// overwriting it, so an outline pixel softens against whatever is already there
    /// (background, ground, an adjacent cell) rather than hard-cutting over it.</summary>
    private void BlendPixel(Color32[] pixels, int x, int y, Color32 color, float t)
    {
        if (x < 0 || x >= _texWidth || y < 0 || y >= _texHeight)
            return;
        int index = y * _texWidth + x;
        pixels[index] = Color32.Lerp(pixels[index], color, t);
    }

    // -------------------------------------------------------------------- UI

    private void BuildUi()
    {
        var canvasGo = new GameObject("MinimapCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _canvasRect = (RectTransform)canvasGo.transform;

        var frameGo = new GameObject("Frame", typeof(Image));
        frameGo.transform.SetParent(canvasGo.transform, false);
        _frameRect = (RectTransform)frameGo.transform;
        frameGo.GetComponent<Image>().color = frameColor;
        ApplyLayout(0f);

        var imageGo = new GameObject("MapImage", typeof(RawImage));
        imageGo.transform.SetParent(frameGo.transform, false);
        var imageRect = (RectTransform)imageGo.transform;
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = new Vector2(framePadding, framePadding);
        imageRect.offsetMax = new Vector2(-framePadding, -framePadding);
        imageGo.GetComponent<RawImage>().texture = _texture;
    }

    private void UpdateExpansion()
    {
        if (_frameRect == null)
            return;

        bool held = Keyboard.current != null && Keyboard.current[expandKey].isPressed;
        _expandBlend = Mathf.MoveTowards(_expandBlend, held ? 1f : 0f, expandSpeed * Time.unscaledDeltaTime);
        ApplyLayout(Mathf.SmoothStep(0f, 1f, _expandBlend));
    }

    /// <summary>Positions the frame between its two layouts: t = 0 is the small
    /// top-right corner map, t = 1 fills the screen (aspect ratio preserved).</summary>
    private void ApplyLayout(float t)
    {
        Vector2 padding = new Vector2(framePadding * 2f, framePadding * 2f);
        Vector2 texSize = new Vector2(_texWidth, _texHeight);

        float cornerScale = Mathf.Min(maxDisplaySize.x / _texWidth, maxDisplaySize.y / _texHeight);
        Vector2 cornerSize = texSize * cornerScale + padding;

        Vector2 available = _canvasRect.rect.size - fullscreenMargin * 2f;
        float fullScale = Mathf.Min(available.x / _texWidth, available.y / _texHeight);
        Vector2 fullSize = texSize * fullScale + padding;

        Vector2 anchor = Vector2.Lerp(new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), t);
        _frameRect.anchorMin = _frameRect.anchorMax = _frameRect.pivot = anchor;
        _frameRect.anchoredPosition = Vector2.Lerp(-screenMargin, Vector2.zero, t);
        _frameRect.sizeDelta = Vector2.Lerp(cornerSize, fullSize, t);
    }

    private void OnDestroy()
    {
        if (_texture != null)
            Destroy(_texture);
    }
}

/// <summary>
/// Spawns a MinimapController in every scene that contains a BugZone (i.e. every
/// gameplay level) so no per-scene setup is required. Place a MinimapController
/// in a scene manually to override the auto-spawned one, or delete this class to
/// opt out of auto-spawning entirely.
/// </summary>
public static class MinimapBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded += (scene, mode) => TrySpawn();
        TrySpawn();
    }

    private static void TrySpawn()
    {
        if (Object.FindFirstObjectByType<MinimapController>() != null)
            return;
        if (Object.FindFirstObjectByType<BugZone>() == null)
            return;

        new GameObject("Minimap (auto)").AddComponent<MinimapController>();
    }
}
