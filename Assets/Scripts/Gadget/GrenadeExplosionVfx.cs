using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(SpriteRenderer))]
public class GrenadeExplosionVfx : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float framesPerSecond = 48f;
    [SerializeField] private int sortingOrder = 50;

    [Header("Flash Light")]
    [SerializeField] private Color lightColor = new Color(1f, 0.72f, 0.28f, 1f);
    [SerializeField] private float lightIntensity = 2.5f;
    [Tooltip("Renderer2D blend style index. 1 is Additive in this project's 2D renderer.")]
    [SerializeField] private int additiveBlendStyleIndex = 1;

    [Header("Blackout")]
    [SerializeField] private Sprite blackoutSprite;
    [Tooltip("How much of the camera view the black circle covers, by area.")]
    [SerializeField] [Range(0.05f, 1f)] private float blackoutScreenFraction = 0.25f;
    [SerializeField] [Range(0f, 1f)] private float blackoutPeakAlpha = 1f;

    private static Light2D persistentGlobalLight;

    private Light2D flashLight;
    private SpriteRenderer blackoutRenderer;
    private float peakOuterRadius = 3f;

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        spriteRenderer.sortingOrder = sortingOrder;
        EnsureSceneStaysLit();
        EnsureFlashLight();
        EnsureBlackout();
    }

    public void Play(float range)
    {
        peakOuterRadius = Mathf.Max(0.1f, range);
        SizeBlackoutToScreen();
        StopAllCoroutines();
        StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        if (frames == null || frames.Length == 0)
        {
            Debug.LogWarning("GrenadeExplosionVfx has no frames assigned.", this);
            Destroy(gameObject);
            yield break;
        }

        float frameDuration = 1f / Mathf.Max(1f, framesPerSecond);
        int count = frames.Length;

        for (int i = 0; i < count; i++)
        {
            spriteRenderer.sprite = frames[i];
            ApplyEnvelope((float)i / Mathf.Max(1, count - 1));
            yield return new WaitForSeconds(frameDuration);
        }

        Destroy(gameObject);
    }

    private void ApplyEnvelope(float t01)
    {
        const float peak = 0.18f;
        float envelope = t01 <= peak
            ? Mathf.Lerp(0.7f, 1f, t01 / peak)
            : 1f - (t01 - peak) / (1f - peak);
        envelope = Mathf.Clamp01(envelope);

        if (flashLight != null)
        {
            flashLight.intensity = lightIntensity * envelope;
            flashLight.volumeIntensity = envelope;
            flashLight.pointLightOuterRadius = peakOuterRadius * Mathf.Lerp(0.55f, 1f, envelope);
            flashLight.pointLightInnerRadius = peakOuterRadius * 0.2f * envelope;
        }

        if (blackoutRenderer != null)
        {
            Color color = blackoutRenderer.color;
            color.a = blackoutPeakAlpha * envelope;
            blackoutRenderer.color = color;
        }
    }

    private void EnsureSceneStaysLit()
    {
        if (persistentGlobalLight != null)
        {
            return;
        }

        Light2D[] lights = FindObjectsByType<Light2D>(FindObjectsInactive.Exclude);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null && lights[i].lightType == Light2D.LightType.Global)
            {
                persistentGlobalLight = lights[i];
                return;
            }
        }

        var go = new GameObject("Global Light 2D");
        persistentGlobalLight = go.AddComponent<Light2D>();
        persistentGlobalLight.lightType = Light2D.LightType.Global;
        persistentGlobalLight.blendStyleIndex = 0;
        persistentGlobalLight.color = Color.white;
        persistentGlobalLight.intensity = 1f;
        persistentGlobalLight.shadowsEnabled = false;
        persistentGlobalLight.volumetricEnabled = false;
    }

    private void EnsureFlashLight()
    {
        flashLight = GetComponent<Light2D>();
        if (flashLight == null)
        {
            flashLight = gameObject.AddComponent<Light2D>();
        }

        flashLight.lightType = Light2D.LightType.Point;
        flashLight.blendStyleIndex = additiveBlendStyleIndex;
        flashLight.color = lightColor;
        flashLight.intensity = 0f;
        flashLight.overlapOperation = Light2D.OverlapOperation.Additive;
        flashLight.shadowsEnabled = false;
        flashLight.volumetricEnabled = true;
        flashLight.volumeIntensity = 0f;
        flashLight.falloffIntensity = 0.45f;
        flashLight.pointLightInnerAngle = 360f;
        flashLight.pointLightOuterAngle = 360f;
    }

    private void EnsureBlackout()
    {
        if (blackoutSprite == null)
        {
            return;
        }

        var go = new GameObject("Blackout");
        go.transform.SetParent(transform, false);

        blackoutRenderer = go.AddComponent<SpriteRenderer>();
        blackoutRenderer.sprite = blackoutSprite;
        blackoutRenderer.color = new Color(0f, 0f, 0f, 0f);
        blackoutRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        blackoutRenderer.sortingOrder = sortingOrder - 1;
        if (spriteRenderer.sharedMaterial != null)
        {
            blackoutRenderer.sharedMaterial = spriteRenderer.sharedMaterial;
        }
    }

    private void SizeBlackoutToScreen()
    {
        if (blackoutRenderer == null || blackoutRenderer.sprite == null)
        {
            return;
        }

        Camera cam = Camera.main;
        float height = 10f;
        float width = 16f;
        if (cam != null && cam.orthographic)
        {
            height = cam.orthographicSize * 2f;
            width = height * cam.aspect;
        }

        float targetArea = width * height * blackoutScreenFraction;
        float targetDiameter = 2f * Mathf.Sqrt(targetArea / Mathf.PI);
        Vector2 spriteSize = blackoutRenderer.sprite.bounds.size;
        float spriteDiameter = Mathf.Max(spriteSize.x, spriteSize.y);
        float scale = spriteDiameter > 0.001f ? targetDiameter / spriteDiameter : targetDiameter;
        blackoutRenderer.transform.localScale = new Vector3(scale, scale, 1f);
    }
}
