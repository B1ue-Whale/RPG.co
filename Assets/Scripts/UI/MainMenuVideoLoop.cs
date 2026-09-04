using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Plays a main-menu video behind the UI and restarts it the instant it ends.
/// </summary>
public class MainMenuVideoLoop : MonoBehaviour
{
    [SerializeField] private VideoClip videoClip;
    [SerializeField] private bool muteAudio;

    private VideoPlayer videoPlayer;
    private RawImage rawImage;
    private AspectRatioFitter aspectFitter;
    private GameObject canvasRoot;

    private void Awake()
    {
        if (videoClip == null)
        {
            Debug.LogWarning("[MainMenuVideoLoop] Assign a VideoClip for the main menu.");
            return;
        }

        BuildBackground();

        videoPlayer = gameObject.GetComponent<VideoPlayer>();
        if (videoPlayer == null)
            videoPlayer = gameObject.AddComponent<VideoPlayer>();

        videoPlayer.playOnAwake = false;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.skipOnDrop = true;
        videoPlayer.isLooping = true;
        videoPlayer.renderMode = VideoRenderMode.APIOnly;
        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.clip = videoClip;
        videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        videoPlayer.audioOutputMode = muteAudio
            ? VideoAudioOutputMode.None
            : VideoAudioOutputMode.Direct;
    }

    private void OnEnable()
    {
        if (videoPlayer == null)
            return;

        videoPlayer.loopPointReached += OnLoopPointReached;
        videoPlayer.prepareCompleted += OnPrepareCompleted;
        videoPlayer.errorReceived += OnErrorReceived;
        videoPlayer.Prepare();
    }

    private void OnDisable()
    {
        if (videoPlayer == null)
            return;

        videoPlayer.loopPointReached -= OnLoopPointReached;
        videoPlayer.prepareCompleted -= OnPrepareCompleted;
        videoPlayer.errorReceived -= OnErrorReceived;
        videoPlayer.Stop();
    }

    private void OnDestroy()
    {
        if (canvasRoot != null)
            Destroy(canvasRoot);
    }

    private void LateUpdate()
    {
        if (videoPlayer == null || !videoPlayer.isPrepared || videoPlayer.isPlaying)
            return;

        RestartFromStart(videoPlayer);
    }

    private void OnLoopPointReached(VideoPlayer source)
    {
        if (!source.isPlaying)
            RestartFromStart(source);
    }

    private static void RestartFromStart(VideoPlayer source)
    {
        source.time = 0d;
        source.Play();
    }

    private void OnPrepareCompleted(VideoPlayer source)
    {
        if (rawImage != null)
        {
            rawImage.texture = source.texture;
            rawImage.enabled = true;
        }

        if (aspectFitter != null && source.width > 0 && source.height > 0)
            aspectFitter.aspectRatio = (float)source.width / source.height;

        source.Play();
    }

    private void OnErrorReceived(VideoPlayer source, string message)
    {
        Debug.LogError($"[MainMenuVideoLoop] {message}");
    }

    private void BuildBackground()
    {
        canvasRoot = new GameObject("MainMenuVideoBackground");

        var canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -10;

        var scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var fill = new GameObject("VideoFill", typeof(RectTransform));
        fill.transform.SetParent(canvasRoot.transform, false);
        var fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        var imageGo = new GameObject(
            "VideoImage",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage),
            typeof(AspectRatioFitter));
        imageGo.transform.SetParent(fill.transform, false);

        rawImage = imageGo.GetComponent<RawImage>();
        rawImage.color = Color.white;
        rawImage.raycastTarget = false;
        rawImage.enabled = false;

        aspectFitter = imageGo.GetComponent<AspectRatioFitter>();
        aspectFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        aspectFitter.aspectRatio = videoClip.width > 0 && videoClip.height > 0
            ? (float)videoClip.width / videoClip.height
            : 16f / 9f;
    }
}
