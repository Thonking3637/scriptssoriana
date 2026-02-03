using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Gestor centralizado de audio para música, SFX e instrucciones.
/// Persiste entre escenas con DontDestroyOnLoad.
/// </summary>
public class SoundManager : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════════════════
    // SINGLETON
    // ═══════════════════════════════════════════════════════════════════════════

    public static SoundManager Instance { get; private set; }

    // ═══════════════════════════════════════════════════════════════════════════
    // SERIALIZED FIELDS - Audio Sources
    // ═══════════════════════════════════════════════════════════════════════════

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource instructionSource;

    [Header("Sound Library")]
    [SerializeField] private List<SoundEntry> soundLibrary = new List<SoundEntry>();
    [SerializeField] private List<AudioClip> successSounds = new List<AudioClip>();

    [Header("UI Samples (Preview)")]
    [SerializeField] private AudioSource uiSampleSource;
    [SerializeField] private AudioClip sampleMusic;
    [SerializeField] private AudioClip sampleSFX;
    [SerializeField] private AudioClip sampleInstruction;
    [SerializeField] private float sampleMinInterval = 0.15f;

    // ═══════════════════════════════════════════════════════════════════════════
    // CONSTANTS - PlayerPrefs Keys
    // ═══════════════════════════════════════════════════════════════════════════

    private const string PP_MASTER = "pp_masterVol";
    private const string PP_MUSIC = "pp_musicVol";
    private const string PP_SFX = "pp_sfxVol";
    private const string PP_INSTR = "pp_instrVol";

    // ═══════════════════════════════════════════════════════════════════════════
    // ENUMS
    // ═══════════════════════════════════════════════════════════════════════════

    public enum AudioChannel { Master, Music, SFX, Instruction }

    // ═══════════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════════

    public event Action OnInstructionStart;
    public event Action OnInstructionEnd;

    // ═══════════════════════════════════════════════════════════════════════════
    // PRIVATE STATE - Volume Settings
    // ═══════════════════════════════════════════════════════════════════════════

    private float _masterVol = 1f;
    private float _musicVol = 0.2f;
    private float _sfxVol = 1f;
    private float _instrVol = 1f;
    private float _currentMusicTrim = 1f;
    private float _savedMusicVolume = -1f;

    // ═══════════════════════════════════════════════════════════════════════════
    // PRIVATE STATE - Music
    // ═══════════════════════════════════════════════════════════════════════════

    private AudioClip _defaultMusicClip;
    private float _defaultMusicVolume = 0.5f;
    private AudioClip _previousMusicClip;
    private float _previousMusicVolume;
    private bool _hasStoredPreviousMusic = false;

    // ═══════════════════════════════════════════════════════════════════════════
    // PRIVATE STATE - Instructions
    // ═══════════════════════════════════════════════════════════════════════════

    private Coroutine _currentInstructionCoroutine;
    private float _instructionTimePosition = 0f;

    // ═══════════════════════════════════════════════════════════════════════════
    // PRIVATE STATE - Pause System
    // ═══════════════════════════════════════════════════════════════════════════

    private bool _isGamePaused = false;
    private float _pausedInstructionTime = 0f;
    private float _pausedMusicTime = 0f;
    private bool _wasInstructionPlaying = false;
    private bool _wasMusicPlayingOnPause = false;
    private bool _wasSFXPlayingOnPause = false;

    // ═══════════════════════════════════════════════════════════════════════════
    // PRIVATE STATE - Loops & Coroutines
    // ═══════════════════════════════════════════════════════════════════════════

    private Dictionary<string, SoundEntry> _soundDictionary = new Dictionary<string, SoundEntry>();
    private Dictionary<string, AudioSource> _activeLoops = new Dictionary<string, AudioSource>();
    private Coroutine _musicSmoothCR;
    private Coroutine _uiSampleFade;
    private float _lastSampleTime = -1f;

    // ═══════════════════════════════════════════════════════════════════════════
    // PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════════

    public bool IsGamePaused => _isGamePaused;
    public bool IsMusicPlaying => musicSource != null && musicSource.isPlaying;
    public AudioClip CurrentMusicClip => musicSource != null ? musicSource.clip : null;
    public bool IsInstructionPlaying => instructionSource != null && instructionSource.isPlaying;

    // ═══════════════════════════════════════════════════════════════════════════
    // LIFECYCLE - Unity Callbacks
    // ═══════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        // Singleton con persistencia
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SubscribeToGameManager();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        GameManager.OnGamePaused -= HandleGamePaused;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Re-suscribirse al GameManager cuando cambia la escena
        SubscribeToGameManager();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════════════════════════

    private void Initialize()
    {
        InitializeSoundLibrary();
        LoadVolumes();
        ApplyVolumes();
        SetupDefaultMusic();
        SetupUISampleSource();
    }

    private void InitializeSoundLibrary()
    {
        _soundDictionary.Clear();
        foreach (var sound in soundLibrary)
        {
            if (!string.IsNullOrEmpty(sound.soundName) && !_soundDictionary.ContainsKey(sound.soundName))
            {
                _soundDictionary.Add(sound.soundName, sound);
            }
        }
    }

    private void SetupDefaultMusic()
    {
        if (musicSource != null && musicSource.clip != null)
        {
            _defaultMusicClip = musicSource.clip;
            _defaultMusicVolume = musicSource.volume;
        }
    }

    private void SetupUISampleSource()
    {
        if (uiSampleSource == null)
        {
            var go = new GameObject("UI_SampleSource");
            go.transform.SetParent(transform);
            uiSampleSource = go.AddComponent<AudioSource>();
            uiSampleSource.playOnAwake = false;
            uiSampleSource.loop = false;
            uiSampleSource.spatialBlend = 0f;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // GAME PAUSE SYSTEM (Event-Driven)
    // ═══════════════════════════════════════════════════════════════════════════

    private void SubscribeToGameManager()
    {
        // Desuscribirse primero para evitar duplicados
        GameManager.OnGamePaused -= HandleGamePaused;
        GameManager.OnGamePaused += HandleGamePaused;
    }

    private void HandleGamePaused(bool paused)
    {
        if (paused)
            PauseAllAudio();
        else
            ResumeAllAudio();
    }

    private void PauseAllAudio()
    {
        if (_isGamePaused) return;
        _isGamePaused = true;

        // Pausar instrucciones
        if (instructionSource != null && instructionSource.isPlaying)
        {
            _pausedInstructionTime = instructionSource.time;
            _wasInstructionPlaying = true;
            instructionSource.Pause();
            Debug.Log($"[SoundManager] 🔇 Instrucción pausada en {_pausedInstructionTime:F2}s");
        }
        else
        {
            _wasInstructionPlaying = false;
        }

        // Pausar música
        if (musicSource != null && musicSource.isPlaying)
        {
            _pausedMusicTime = musicSource.time;
            _wasMusicPlayingOnPause = true;
            musicSource.Pause();
        }
        else
        {
            _wasMusicPlayingOnPause = false;
        }

        // Pausar SFX
        if (sfxSource != null && sfxSource.isPlaying)
        {
            _wasSFXPlayingOnPause = true;
            sfxSource.Pause();
        }
        else
        {
            _wasSFXPlayingOnPause = false;
        }

        // Pausar loops activos
        foreach (var loop in _activeLoops.Values)
        {
            if (loop != null && loop.isPlaying)
                loop.Pause();
        }
    }

    private void ResumeAllAudio()
    {
        if (!_isGamePaused) return;
        _isGamePaused = false;

        // Reanudar instrucciones
        if (_wasInstructionPlaying && instructionSource != null)
        {
            instructionSource.time = _pausedInstructionTime;
            instructionSource.UnPause();
            Debug.Log($"[SoundManager] 🔊 Instrucción reanudada desde {_pausedInstructionTime:F2}s");
        }

        // Reanudar música
        if (_wasMusicPlayingOnPause && musicSource != null)
        {
            musicSource.time = _pausedMusicTime;
            musicSource.UnPause();
        }

        // Reanudar SFX
        if (_wasSFXPlayingOnPause && sfxSource != null)
        {
            sfxSource.UnPause();
        }

        // Reanudar loops activos
        foreach (var loop in _activeLoops.Values)
        {
            if (loop != null)
                loop.UnPause();
        }

        // Reset flags
        _wasInstructionPlaying = false;
        _wasMusicPlayingOnPause = false;
        _wasSFXPlayingOnPause = false;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // VOLUME MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════════

    #region Volume Getters

    public float GetMasterVolume() => _masterVol;
    public float GetMusicVolume() => _musicVol;
    public float GetSFXVolume() => _sfxVol;
    public float GetInstrVolume() => _instrVol;

    #endregion

    #region Volume Setters

    public void SetMasterVolume(float value, bool playSample = false)
    {
        _masterVol = Mathf.Clamp01(value);
        SaveVolumes();
        ApplyVolumes(smoothMusic: true);
    }

    public void SetMusicVolume(float value, bool playSample = false)
    {
        _musicVol = Mathf.Clamp01(value);
        SaveVolumes();
        ApplyVolumes(smoothMusic: true);
    }

    public void SetSFXVolume(float value, bool playSample = false)
    {
        _sfxVol = Mathf.Clamp01(value);
        ApplyVolumes();
        SaveVolumes();
        if (playSample) PlayPreviewSample(AudioChannel.SFX);
    }

    public void SetInstrVolume(float value, bool playSample = false)
    {
        _instrVol = Mathf.Clamp01(value);
        ApplyVolumes();
        SaveVolumes();
        if (playSample) PlayPreviewSample(AudioChannel.Instruction);
    }

    #endregion

    #region Volume Persistence

    private void LoadVolumes()
    {
        _masterVol = PlayerPrefs.GetFloat(PP_MASTER, 1f);
        _musicVol = PlayerPrefs.GetFloat(PP_MUSIC, 0.2f);
        _sfxVol = PlayerPrefs.GetFloat(PP_SFX, 1f);
        _instrVol = PlayerPrefs.GetFloat(PP_INSTR, 1f);
    }

    private void SaveVolumes()
    {
        PlayerPrefs.SetFloat(PP_MASTER, _masterVol);
        PlayerPrefs.SetFloat(PP_MUSIC, _musicVol);
        PlayerPrefs.SetFloat(PP_SFX, _sfxVol);
        PlayerPrefs.SetFloat(PP_INSTR, _instrVol);
        PlayerPrefs.Save();
    }

    public void ApplyVolumes(bool smoothMusic = false)
    {
        float master = Mathf.Clamp01(_masterVol);

        // Música
        if (musicSource != null)
        {
            float target = Mathf.Clamp01(master * _musicVol * _currentMusicTrim);
            if (!smoothMusic)
            {
                musicSource.volume = target;
            }
            else
            {
                if (_musicSmoothCR != null) StopCoroutine(_musicSmoothCR);
                _musicSmoothCR = StartCoroutine(SmoothVolumeCoroutine(musicSource, target, 0.08f));
            }
        }

        // SFX
        if (sfxSource != null)
            sfxSource.volume = Mathf.Clamp01(master * _sfxVol);

        // Instrucciones
        if (instructionSource != null)
            instructionSource.volume = Mathf.Clamp01(master * _instrVol);
    }

    #endregion

    #region Temporary Volume Changes

    public void LowerMusicVolume(float newVolume = 0f)
    {
        if (musicSource == null) return;

        if (_savedMusicVolume < 0f)
            _savedMusicVolume = musicSource.volume;

        musicSource.volume = newVolume;
    }

    public void RestoreMusicVolume()
    {
        if (musicSource == null || _savedMusicVolume < 0f) return;

        musicSource.volume = _savedMusicVolume;
        _savedMusicVolume = -1f;
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    // SFX PLAYBACK
    // ═══════════════════════════════════════════════════════════════════════════

    public void PlaySound(string soundName, Action onComplete = null)
    {
        if (!_soundDictionary.TryGetValue(soundName, out SoundEntry sound))
        {
            Debug.LogWarning($"[SoundManager] El sonido '{soundName}' no se encuentra en la librería.");
            return;
        }

        sfxSource.pitch = sound.pitch;
        sfxSource.PlayOneShot(sound.clip, sound.volume);

        if (onComplete != null)
            StartCoroutine(ExecuteAfterDelayCoroutine(sound.clip.length, onComplete));
    }

    public void PlaySound(AudioClip clip, float volume = 1f)
    {
        if (clip == null)
        {
            Debug.LogWarning("[SoundManager] AudioClip nulo en PlaySound");
            return;
        }

        sfxSource.pitch = UnityEngine.Random.Range(1f, 1.05f);
        sfxSource.PlayOneShot(clip, volume);
    }

    public void PlaySound(AudioClip clip, Action onComplete)
    {
        if (clip == null)
        {
            Debug.LogWarning("[SoundManager] AudioClip nulo en PlaySound con callback");
            return;
        }

        sfxSource.PlayOneShot(clip);

        if (onComplete != null)
            StartCoroutine(ExecuteAfterDelayCoroutine(clip.length, onComplete));
    }

    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip != null)
            sfxSource.PlayOneShot(clip, volume);
    }

    public void PlaySuccessSound(AudioClip specificClip = null)
    {
        if (specificClip != null)
        {
            sfxSource.PlayOneShot(specificClip);
        }
        else if (successSounds.Count > 0)
        {
            AudioClip randomClip = successSounds[UnityEngine.Random.Range(0, successSounds.Count)];
            sfxSource.PlayOneShot(randomClip);
        }
        else
        {
            Debug.LogWarning("[SoundManager] No hay sonidos de éxito asignados.");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // INSTRUCTION AUDIO
    // ═══════════════════════════════════════════════════════════════════════════

    public void PlayInstructionSound(AudioClip clip, Action onComplete = null)
    {
        if (clip == null) return;

        // Usar instructionSource si está disponible, sino fallback a sfxSource
        if (instructionSource == null)
        {
            PlayInstructionWithSFXFallback(clip, onComplete);
            return;
        }

        if (_currentInstructionCoroutine != null)
            StopCoroutine(_currentInstructionCoroutine);

        _instructionTimePosition = 0f;
        _currentInstructionCoroutine = StartCoroutine(PlayInstructionCoroutine(clip, onComplete));
        OnInstructionStart?.Invoke();
    }

    public void StopInstructionSound()
    {
        if (instructionSource != null && instructionSource.isPlaying)
            instructionSource.Stop();

        if (sfxSource != null && sfxSource.isPlaying)
            sfxSource.Stop();

        if (_currentInstructionCoroutine != null)
        {
            StopCoroutine(_currentInstructionCoroutine);
            _currentInstructionCoroutine = null;
        }
    }

    public void PauseInstructions()
    {
        if (instructionSource != null && instructionSource.isPlaying)
        {
            _pausedInstructionTime = instructionSource.time;
            instructionSource.Pause();
        }
        else if (sfxSource != null && sfxSource.isPlaying)
        {
            _instructionTimePosition = sfxSource.time;
            sfxSource.Pause();
        }
    }

    public void ResumeInstructions()
    {
        if (instructionSource != null && _pausedInstructionTime > 0f)
        {
            instructionSource.time = _pausedInstructionTime;
            instructionSource.UnPause();
            _pausedInstructionTime = 0f;
        }
        else if (_instructionTimePosition > 0f && sfxSource != null)
        {
            sfxSource.time = _instructionTimePosition;
            sfxSource.Play();
        }
    }

    private IEnumerator PlayInstructionCoroutine(AudioClip clip, Action onComplete)
    {
        instructionSource.clip = clip;
        instructionSource.time = _instructionTimePosition;
        instructionSource.Play();

        while (instructionSource.isPlaying)
            yield return null;

        _instructionTimePosition = 0f;
        _currentInstructionCoroutine = null;
        OnInstructionEnd?.Invoke();
        onComplete?.Invoke();
    }

    private void PlayInstructionWithSFXFallback(AudioClip clip, Action onComplete)
    {
        if (_currentInstructionCoroutine != null)
            StopCoroutine(_currentInstructionCoroutine);

        _instructionTimePosition = 0f;
        _currentInstructionCoroutine = StartCoroutine(PlayInstructionWithSFXCoroutine(clip, onComplete));
        OnInstructionStart?.Invoke();
    }

    private IEnumerator PlayInstructionWithSFXCoroutine(AudioClip clip, Action onComplete)
    {
        sfxSource.clip = clip;
        sfxSource.time = _instructionTimePosition;
        sfxSource.Play();

        while (sfxSource.isPlaying)
            yield return null;

        _instructionTimePosition = 0f;
        _currentInstructionCoroutine = null;
        OnInstructionEnd?.Invoke();
        onComplete?.Invoke();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // MUSIC PLAYBACK
    // ═══════════════════════════════════════════════════════════════════════════

    public void PlayMusic(AudioClip clip, float volume = 1f, bool loop = true)
    {
        if (musicSource == null || clip == null) return;

        _currentMusicTrim = Mathf.Clamp01(volume);
        musicSource.Stop();
        musicSource.loop = loop;
        musicSource.clip = clip;
        musicSource.volume = Mathf.Clamp01(_masterVol * _musicVol * _currentMusicTrim);
        musicSource.Play();
    }

    public void PlayMusic(AudioClip clip, bool loop)
    {
        PlayMusic(clip, 1f, loop);
    }

    public void StopMusic()
    {
        if (musicSource == null) return;
        musicSource.Stop();
        musicSource.clip = null;
    }

    public void SetDefaultMusic(AudioClip clip, float volume = 1f)
    {
        _defaultMusicClip = clip;
        _defaultMusicVolume = volume;
    }

    public void SetActivityMusic(AudioClip newMusic, float volume = 0.2f, bool restartIfSame = false)
    {
        if (musicSource == null || newMusic == null) return;

        bool isSameClip = (musicSource.clip == newMusic);

        // Guardar música anterior si es diferente
        if (!_hasStoredPreviousMusic && musicSource.clip != null && !isSameClip)
        {
            _previousMusicClip = musicSource.clip;
            _previousMusicVolume = _currentMusicTrim;
            _hasStoredPreviousMusic = true;
        }

        _currentMusicTrim = Mathf.Clamp01(volume);

        // Si es la misma y no queremos reiniciar, solo ajustar volumen
        if (isSameClip && !restartIfSame)
        {
            ApplyVolumes(smoothMusic: true);
            return;
        }

        PlayMusic(newMusic, _currentMusicTrim, loop: true);
    }

    public void SetSceneBaseMusic(AudioClip clip, float volumeTrim = 1f, bool force = true)
    {
        if (clip == null || musicSource == null) return;

        _hasStoredPreviousMusic = false;
        _previousMusicClip = null;

        _defaultMusicClip = clip;
        _defaultMusicVolume = Mathf.Clamp01(volumeTrim);

        if (force || CurrentMusicClip != clip)
            PlayMusic(clip, _defaultMusicVolume, loop: true);
        else
            ApplyVolumes(smoothMusic: false);
    }

    public void RestorePreviousMusic(bool fallbackToDefault = true)
    {
        if (_hasStoredPreviousMusic && _previousMusicClip != null)
        {
            PlayMusic(_previousMusicClip, _previousMusicVolume);
            _previousMusicClip = null;
            _hasStoredPreviousMusic = false;
            return;
        }

        if (fallbackToDefault && _defaultMusicClip != null)
            PlayMusic(_defaultMusicClip, _defaultMusicVolume);
        else
            StopMusic();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // MUSIC CROSSFADE
    // ═══════════════════════════════════════════════════════════════════════════

    public void CrossfadeMusic(AudioClip newClip, float fadeOut = 0.5f, float fadeIn = 0.6f,
                               float volumeTrim = 0.3f, bool loop = true, bool restartIfSame = false)
    {
        if (musicSource == null || newClip == null) return;

        // Si es la misma y no queremos reiniciar, solo ajustar volumen
        if (musicSource.clip == newClip && !restartIfSame)
        {
            _currentMusicTrim = Mathf.Clamp01(volumeTrim);
            ApplyVolumes(smoothMusic: true);
            return;
        }

        // Guardar música anterior
        if (!_hasStoredPreviousMusic && musicSource.clip != null && musicSource.clip != newClip)
        {
            _previousMusicClip = musicSource.clip;
            _previousMusicVolume = _currentMusicTrim;
            _hasStoredPreviousMusic = true;
        }

        StartCoroutine(CrossfadeMusicCoroutine(newClip, fadeOut, fadeIn, Mathf.Clamp01(volumeTrim), loop));
    }

    public void CrossfadeToPrevious(float fadeOut = 0.4f, float fadeIn = 0.6f)
    {
        if (!_hasStoredPreviousMusic || _previousMusicClip == null)
        {
            RestorePreviousMusic(fallbackToDefault: true);
            return;
        }
        CrossfadeMusic(_previousMusicClip, fadeOut, fadeIn, _previousMusicVolume, loop: true, restartIfSame: false);
    }

    private IEnumerator CrossfadeMusicCoroutine(AudioClip newClip, float fadeOut, float fadeIn, float volumeTrim, bool loop)
    {
        // 1) Fade out
        if (_musicSmoothCR != null) StopCoroutine(_musicSmoothCR);
        _musicSmoothCR = StartCoroutine(SmoothVolumeCoroutine(musicSource, 0f, Mathf.Max(0.01f, fadeOut)));
        yield return _musicSmoothCR;

        // 2) Cambiar clip
        musicSource.Stop();
        musicSource.clip = newClip;
        musicSource.loop = loop;

        _currentMusicTrim = volumeTrim;
        ApplyVolumes(smoothMusic: false);
        float targetVolume = musicSource.volume;

        musicSource.volume = 0f;
        musicSource.Play();

        // 3) Fade in
        _musicSmoothCR = StartCoroutine(SmoothVolumeCoroutine(musicSource, targetVolume, Mathf.Max(0.01f, fadeIn)));
        yield return _musicSmoothCR;
        _musicSmoothCR = null;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // LOOP SOUNDS
    // ═══════════════════════════════════════════════════════════════════════════

    public void PlayLoop(string soundName, float volumeOverride = 1f)
    {
        StopLoop(soundName);

        if (!_soundDictionary.TryGetValue(soundName, out SoundEntry sound))
        {
            Debug.LogWarning($"[SoundManager] El sonido '{soundName}' no se encuentra en la librería.");
            return;
        }

        GameObject loopObj = new GameObject("LoopSound_" + soundName);
        AudioSource loopSource = loopObj.AddComponent<AudioSource>();
        loopSource.clip = sound.clip;
        loopSource.volume = sound.volume * volumeOverride;
        loopSource.pitch = sound.pitch;
        loopSource.loop = true;
        loopSource.Play();

        _activeLoops[soundName] = loopSource;
    }

    public void StopLoop(string soundName)
    {
        if (_activeLoops.TryGetValue(soundName, out AudioSource source))
        {
            if (source != null)
                Destroy(source.gameObject);

            _activeLoops.Remove(soundName);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // UI PREVIEW SAMPLES
    // ═══════════════════════════════════════════════════════════════════════════

    public void PlayPreviewSample(AudioChannel channel)
    {
        if (Time.unscaledTime - _lastSampleTime < sampleMinInterval) return;
        _lastSampleTime = Time.unscaledTime;

        AudioClip clip = null;
        float channelVol = 1f;

        switch (channel)
        {
            case AudioChannel.Music:
                clip = sampleMusic;
                channelVol = _musicVol;
                break;
            case AudioChannel.SFX:
                clip = sampleSFX;
                channelVol = _sfxVol;
                break;
            case AudioChannel.Instruction:
                clip = sampleInstruction;
                channelVol = _instrVol;
                break;
            default:
                clip = sampleSFX;
                channelVol = _masterVol;
                break;
        }

        if (clip == null || uiSampleSource == null) return;

        StopPreviewSamples();

        uiSampleSource.volume = Mathf.Clamp01(_masterVol * channelVol);
        uiSampleSource.clip = clip;
        uiSampleSource.time = 0f;
        uiSampleSource.loop = false;
        uiSampleSource.Play();
    }

    public void StopPreviewSamples(bool hardStop = true)
    {
        if (uiSampleSource == null) return;

        if (_uiSampleFade != null)
        {
            StopCoroutine(_uiSampleFade);
            _uiSampleFade = null;
        }

        if (hardStop)
            uiSampleSource.Stop();
        else
            _uiSampleFade = StartCoroutine(FadeOutAndStopCoroutine(uiSampleSource, 0.05f));
    }


    // ═══════════════════════════════════════════════════════════════════════════
    // UTILITY COROUTINES
    // ═══════════════════════════════════════════════════════════════════════════

    private IEnumerator ExecuteAfterDelayCoroutine(float delay, Action callback)
    {
        yield return new WaitForSeconds(delay);
        callback?.Invoke();
    }

    private IEnumerator SmoothVolumeCoroutine(AudioSource source, float targetVolume, float duration)
    {
        float startVolume = source.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(startVolume, targetVolume, elapsed / duration);
            yield return null;
        }

        source.volume = targetVolume;
        _musicSmoothCR = null;
    }

    private IEnumerator FadeOutAndStopCoroutine(AudioSource source, float duration)
    {
        if (!source.isPlaying) yield break;

        float startVolume = source.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            yield return null;
        }

        source.Stop();
        source.volume = startVolume;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// SOUND ENTRY (Data Class)
// ═══════════════════════════════════════════════════════════════════════════════

[Serializable]
public class SoundEntry
{
    public string soundName;
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0.5f, 2f)] public float pitch = 1f;
}