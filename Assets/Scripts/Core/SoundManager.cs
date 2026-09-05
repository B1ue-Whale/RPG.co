using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 사운드 효과 및 배경 음악 관리
/// <para>게임 전체 전역 싱글톤</para>
/// </summary>
[DefaultExecutionOrder(-900)]
public class SoundManager : Singleton<SoundManager>
{
    public const string MasterVolumePrefsKey = "sound.masterVolume";
    public const string BgmVolumePrefsKey = "sound.bgmVolume";
    public const string SfxVolumePrefsKey = "sound.sfxVolume";
    public const string MasterMutedPrefsKey = "sound.masterMuted";
    public const string BgmMutedPrefsKey = "sound.bgmMuted";
    public const string SfxMutedPrefsKey = "sound.sfxMuted";

    private const int SfxVoiceCount = 8;
    private const float DefaultBgmFadeSeconds = 0.4f;

    public event Action SettingsChanged;

    [SerializeField] private float defaultBgmFadeSeconds = DefaultBgmFadeSeconds;

    private AudioSource _bgmSource;
    private AudioSource[] _sfxSources;
    private Coroutine _bgmFadeRoutine;
    private float _bgmFade = 1f;
    private bool _pausedByGame;
    private bool _gameManagerBound;

    private float _masterVolume = 1f;
    private float _bgmVolume = 1f;
    private float _sfxVolume = 1f;
    private bool _masterMuted;
    private bool _bgmMuted;
    private bool _sfxMuted;

    public float MasterVolume
    {
        get => _masterVolume;
        set => SetVolume(ref _masterVolume, value, MasterVolumePrefsKey);
    }

    public float BgmVolume
    {
        get => _bgmVolume;
        set => SetVolume(ref _bgmVolume, value, BgmVolumePrefsKey);
    }

    public float SfxVolume
    {
        get => _sfxVolume;
        set => SetVolume(ref _sfxVolume, value, SfxVolumePrefsKey);
    }

    public bool MasterMuted
    {
        get => _masterMuted;
        set => SetMuted(ref _masterMuted, value, MasterMutedPrefsKey);
    }

    public bool BgmMuted
    {
        get => _bgmMuted;
        set => SetMuted(ref _bgmMuted, value, BgmMutedPrefsKey);
    }

    public bool SfxMuted
    {
        get => _sfxMuted;
        set => SetMuted(ref _sfxMuted, value, SfxMutedPrefsKey);
    }

    public AudioClip CurrentBgm => _bgmSource != null ? _bgmSource.clip : null;
    public bool IsBgmPlaying => _bgmSource != null && _bgmSource.isPlaying;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        ClearInstance();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        var go = new GameObject(nameof(SoundManager));
        go.AddComponent<SoundManager>();
    }

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this)
            return;

        LoadSettings();
        CreateAudioSources();
        ApplyVolumes();

        SceneManager.sceneLoaded += OnSceneLoaded;
        TryBindGameManager();
    }

    private void Start()
    {
        if (Instance != this)
            return;

        TryBindGameManager();
    }

    protected override void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnbindGameManager();
        base.OnDestroy();
    }

    public void PlayBgm(AudioClip clip, bool loop = true, float fadeSeconds = -1f, bool restartIfSame = false)
    {
        if (_bgmSource == null)
            return;

        if (clip == null)
        {
            StopBgm(fadeSeconds);
            return;
        }

        if (!restartIfSame && _bgmSource.clip == clip && (_bgmSource.isPlaying || _pausedByGame))
            return;

        float fade = ResolveFadeSeconds(fadeSeconds);
        StopBgmFade();
        _bgmSource.loop = loop;
        _pausedByGame = false;

        if (fade <= 0f || !_bgmSource.isPlaying)
        {
            StartBgmImmediate(clip);
            PauseIfGamePaused();
            return;
        }

        _bgmFadeRoutine = StartCoroutine(CrossfadeBgmRoutine(clip, fade));
    }

    public void PlayBgm(string resourcePath, bool loop = true, float fadeSeconds = -1f, bool restartIfSame = false)
    {
        PlayBgm(LoadClip(resourcePath), loop, fadeSeconds, restartIfSame);
    }

    public void StopBgm(float fadeSeconds = -1f)
    {
        if (_bgmSource == null)
            return;

        _pausedByGame = false;
        float fade = ResolveFadeSeconds(fadeSeconds);
        StopBgmFade();

        if (fade <= 0f || !_bgmSource.isPlaying)
        {
            _bgmSource.Stop();
            _bgmSource.clip = null;
            _bgmFade = 1f;
            ApplyBgmVolume();
            return;
        }

        _bgmFadeRoutine = StartCoroutine(FadeOutBgmRoutine(fade));
    }

    public void PauseBgm()
    {
        PauseBgmInternal(false);
    }

    public void ResumeBgm()
    {
        ResumeBgmInternal();
    }

    public void PlaySfx(AudioClip clip, float volumeScale = 1f, float pitch = 1f)
    {
        if (clip == null || _sfxSources == null || IsSfxSilenced())
            return;

        AudioSource source = GetAvailableSfxSource();
        source.pitch = pitch <= 0f ? 1f : pitch;
        source.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    public void PlaySfx(string resourcePath, float volumeScale = 1f, float pitch = 1f)
    {
        PlaySfx(LoadClip(resourcePath), volumeScale, pitch);
    }

    public void PlaySfxAt(AudioClip clip, Vector3 worldPosition, float volumeScale = 1f)
    {
        if (clip == null || IsSfxSilenced())
            return;

        AudioSource.PlayClipAtPoint(
            clip,
            worldPosition,
            _masterVolume * _sfxVolume * Mathf.Clamp01(volumeScale));
    }

    public void PlaySfxAt(string resourcePath, Vector3 worldPosition, float volumeScale = 1f)
    {
        PlaySfxAt(LoadClip(resourcePath), worldPosition, volumeScale);
    }

    public void StopSfx()
    {
        if (_sfxSources == null)
            return;

        for (int i = 0; i < _sfxSources.Length; i++)
            _sfxSources[i].Stop();
    }

    private void CreateAudioSources()
    {
        _bgmSource = CreateSource("BGM", loop: true, priority: 0);
        _sfxSources = new AudioSource[SfxVoiceCount];
        for (int i = 0; i < SfxVoiceCount; i++)
            _sfxSources[i] = CreateSource($"SFX_{i}", loop: false, priority: 128);
    }

    private AudioSource CreateSource(string sourceName, bool loop, int priority)
    {
        var sourceObject = new GameObject(sourceName);
        sourceObject.transform.SetParent(transform, false);

        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.priority = priority;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        return source;
    }

    private void StartBgmImmediate(AudioClip clip)
    {
        _bgmSource.clip = clip;
        _bgmFade = 1f;
        ApplyBgmVolume();
        _bgmSource.Play();
    }

    private IEnumerator CrossfadeBgmRoutine(AudioClip nextClip, float fadeSeconds)
    {
        yield return FadeBgmVolume(0f, fadeSeconds);

        _bgmSource.clip = nextClip;
        _bgmSource.Play();
        PauseIfGamePaused();

        yield return FadeBgmVolume(1f, fadeSeconds);
        _bgmFadeRoutine = null;
    }

    private IEnumerator FadeOutBgmRoutine(float fadeSeconds)
    {
        yield return FadeBgmVolume(0f, fadeSeconds);

        _bgmSource.Stop();
        _bgmSource.clip = null;
        _bgmFade = 1f;
        ApplyBgmVolume();
        _bgmFadeRoutine = null;
    }

    private IEnumerator FadeBgmVolume(float target, float fadeSeconds)
    {
        float start = _bgmFade;
        float elapsed = 0f;

        while (elapsed < fadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            _bgmFade = Mathf.Lerp(start, target, elapsed / fadeSeconds);
            ApplyBgmVolume();
            yield return null;
        }

        _bgmFade = target;
        ApplyBgmVolume();
    }

    private void StopBgmFade()
    {
        if (_bgmFadeRoutine == null)
            return;

        StopCoroutine(_bgmFadeRoutine);
        _bgmFadeRoutine = null;
    }

    private void PauseBgmInternal(bool fromGameState)
    {
        if (_bgmSource == null || !_bgmSource.isPlaying)
            return;

        _bgmSource.Pause();
        if (fromGameState)
            _pausedByGame = true;
    }

    private void ResumeBgmInternal()
    {
        if (_bgmSource == null || _bgmSource.clip == null)
            return;

        _bgmSource.UnPause();
        _pausedByGame = false;
    }

    private void PauseIfGamePaused()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsState(Enums.GameState.Paused))
            PauseBgmInternal(true);
    }

    private void TryBindGameManager()
    {
        if (_gameManagerBound || GameManager.Instance == null)
            return;

        GameManager.Instance.StateChanged += OnGameStateChanged;
        _gameManagerBound = true;
    }

    private void UnbindGameManager()
    {
        if (!_gameManagerBound)
            return;

        if (GameManager.Instance != null)
            GameManager.Instance.StateChanged -= OnGameStateChanged;

        _gameManagerBound = false;
    }

    private void OnGameStateChanged(Enums.GameState newState)
    {
        if (newState == Enums.GameState.Paused)
        {
            PauseBgmInternal(true);
            return;
        }

        if (_pausedByGame)
            ResumeBgmInternal();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;

        // A fresh level/menu can leave a paused track stuck if the previous
        // scene ended while GameState.Paused and never resumed.
        if (_pausedByGame)
            ResumeBgmInternal();
    }

    private AudioSource GetAvailableSfxSource()
    {
        for (int i = 0; i < _sfxSources.Length; i++)
        {
            if (!_sfxSources[i].isPlaying)
                return _sfxSources[i];
        }

        return _sfxSources[0];
    }

    private bool IsSfxSilenced()
    {
        return _masterMuted || _sfxMuted || _masterVolume <= 0f || _sfxVolume <= 0f;
    }

    private float ResolveFadeSeconds(float fadeSeconds)
    {
        if (fadeSeconds >= 0f)
            return fadeSeconds;

        return defaultBgmFadeSeconds < 0f ? DefaultBgmFadeSeconds : defaultBgmFadeSeconds;
    }

    private static AudioClip LoadClip(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return null;

        AudioClip clip = Resources.Load<AudioClip>(resourcePath);
        if (clip == null)
            Debug.LogWarning($"[{nameof(SoundManager)}] AudioClip not found at Resources/{resourcePath}");

        return clip;
    }

    private void SetVolume(ref float field, float value, string prefsKey)
    {
        float clamped = Mathf.Clamp01(value);
        if (Mathf.Approximately(field, clamped))
            return;

        field = clamped;
        PlayerPrefs.SetFloat(prefsKey, field);
        PlayerPrefs.Save();
        ApplyVolumes();
        SettingsChanged?.Invoke();
    }

    private void SetMuted(ref bool field, bool value, string prefsKey)
    {
        if (field == value)
            return;

        field = value;
        PlayerPrefs.SetInt(prefsKey, field ? 1 : 0);
        PlayerPrefs.Save();
        ApplyVolumes();
        SettingsChanged?.Invoke();
    }

    private void LoadSettings()
    {
        _masterVolume = PlayerPrefs.GetFloat(MasterVolumePrefsKey, 1f);
        _bgmVolume = PlayerPrefs.GetFloat(BgmVolumePrefsKey, 1f);
        _sfxVolume = PlayerPrefs.GetFloat(SfxVolumePrefsKey, 1f);
        _masterMuted = PlayerPrefs.GetInt(MasterMutedPrefsKey, 0) != 0;
        _bgmMuted = PlayerPrefs.GetInt(BgmMutedPrefsKey, 0) != 0;
        _sfxMuted = PlayerPrefs.GetInt(SfxMutedPrefsKey, 0) != 0;
    }

    private void ApplyVolumes()
    {
        ApplyBgmVolume();
        ApplySfxVolume();
    }

    private void ApplyBgmVolume()
    {
        if (_bgmSource == null)
            return;

        _bgmSource.volume = _masterVolume * _bgmVolume * _bgmFade;
        _bgmSource.mute = _masterMuted || _bgmMuted;
    }

    private void ApplySfxVolume()
    {
        if (_sfxSources == null)
            return;

        float volume = _masterVolume * _sfxVolume;
        bool muted = _masterMuted || _sfxMuted;
        for (int i = 0; i < _sfxSources.Length; i++)
        {
            _sfxSources[i].volume = volume;
            _sfxSources[i].mute = muted;
        }
    }
}
