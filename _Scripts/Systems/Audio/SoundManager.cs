using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

[Serializable]
public class SoundEntry
{
    public string soundName;
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0.5f, 2f)] public float pitch = 1f;
}

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    [Header("Audio Sources")]
    public AudioSource musicSource;
    public AudioSource sfxSource;
    public AudioSource instructionSource;

    [Header("Listas de Sonidos")]
    public List<SoundEntry> soundLibrary = new List<SoundEntry>();
    public List<AudioClip> successSounds;

    private Dictionary<string, SoundEntry> soundDictionary = new Dictionary<string, SoundEntry>();
    private Coroutine currentInstructionCoroutine;
    private bool wasMusicPlaying = false;
    private bool wasSFXPlaying = false;

    private float instructionTimePosition = 0f;

    private AudioClip defaultMusicClip;
    private float defaultMusicVolume = 0.5f;

    private AudioClip previousMusicClip;
    private float previousMusicVolume;
    private bool hasStoredPreviousMusic = false;

    public event Action OnInstructionStart;
    public event Action OnInstructionEnd;
    private Dictionary<string, AudioSource> activeLoops = new();

    private float savedMusicVolume = -1f;

    // Volúmenes globales
    const string PP_MASTER = "pp_masterVol";
    const string PP_MUSIC = "pp_musicVol";
    const string PP_SFX = "pp_sfxVol";
    const string PP_INSTR = "pp_instrVol";

    [Range(0f, 1f)] float masterVol = 1f;
    [Range(0f, 1f)] float musicVol = 0.2f;
    [Range(0f, 1f)] float sfxVol = 1f;
    [Range(0f, 1f)] float instrVol = 1f;

    public enum AudioChannel { Master, Music, SFX, Instruction }

    [Header("UI Sample (preview)")]
    public AudioSource uiSampleSource;
    public float sampleMinInterval = 0.15f;

    private float lastSampleTime = -1f;
    private Coroutine uiSampleFade;

    private float currentMusicTrim = 1f;
    private Coroutine musicSmoothCR;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // persiste
            InitializeSoundLibrary();
            LoadVolumes();                 // aplica persistencia
            ApplyVolumes();
        }
        else { Destroy(gameObject); return; }

        if (musicSource != null && musicSource.clip != null)
        {
            defaultMusicClip = musicSource.clip;
            defaultMusicVolume = musicSource.volume;
        }

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

    void LoadVolumes()
    {
        masterVol = PlayerPrefs.GetFloat(PP_MASTER, 1f);
        musicVol = PlayerPrefs.GetFloat(PP_MUSIC, 0.2f);
        sfxVol = PlayerPrefs.GetFloat(PP_SFX, 1f);
        instrVol = PlayerPrefs.GetFloat(PP_INSTR, 1f);
    }

    void SaveVolumes()
    {
        PlayerPrefs.SetFloat(PP_MASTER, masterVol);
        PlayerPrefs.SetFloat(PP_MUSIC, musicVol);
        PlayerPrefs.SetFloat(PP_SFX, sfxVol);
        PlayerPrefs.SetFloat(PP_INSTR, instrVol);
        PlayerPrefs.Save();
    }

    public float GetMasterVolume() => masterVol;
    public float GetMusicVolume() => musicVol;
    public float GetSFXVolume() => sfxVol;
    public float GetInstrVolume() => instrVol;

    public void SetMasterVolume(float v, bool playSample = false)
    {
        masterVol = Mathf.Clamp01(v);
        SaveVolumes();
        ApplyVolumes(smoothMusic: true); 
    }
    public void SetMusicVolume(float v, bool playSample = false)
    {
        musicVol = Mathf.Clamp01(v);
        SaveVolumes();
        ApplyVolumes(smoothMusic: true);
    }
    public void SetSFXVolume(float v, bool playSample = false)
    {
        sfxVol = Mathf.Clamp01(v); ApplyVolumes(); SaveVolumes();
        if (playSample) PlayPreviewSample(AudioChannel.SFX);
    }
    public void SetInstrVolume(float v, bool playSample = false)
    {
        instrVol = Mathf.Clamp01(v); ApplyVolumes(); SaveVolumes();
        if (playSample) PlayPreviewSample(AudioChannel.Instruction);
    }

    // --------------------------
    // Samples para sliders
    // --------------------------
    [Header("Samples de UI")]
    public AudioClip sampleMusic;
    public AudioClip sampleSFX;
    public AudioClip sampleInstruction;

    void PlayUISampleMusic()
    {
        if (sampleMusic == null || musicSource == null) return;
        // reproducir sobre musicSource SIN cambiar loop actual (OneShot no mezcla con musicSource, así que:
        musicSource.PlayOneShot(sampleMusic, 1f);
    }
    void PlayUISampleSFX()
    {
        if (sampleSFX == null || sfxSource == null) return;
        sfxSource.PlayOneShot(sampleSFX, 1f);
    }
    void PlayUISampleInstruction()
    {
        if (sampleInstruction == null) return;
        if (instructionSource != null)
        {
            instructionSource.Stop();
            instructionSource.clip = sampleInstruction;
            instructionSource.time = 0;
            instructionSource.loop = false;
            instructionSource.Play();
        }
        else
        {
            // fallback si aún no creas instructionSource
            sfxSource?.PlayOneShot(sampleInstruction, 1f);
        }
    }

    void ApplyVolumes()
    {
        float safeMaster = Mathf.Clamp01(masterVol);
        if (musicSource) musicSource.volume = safeMaster * Mathf.Clamp01(musicVol);
        if (sfxSource) sfxSource.volume = safeMaster * Mathf.Clamp01(sfxVol);
        if (instructionSource) instructionSource.volume = safeMaster * Mathf.Clamp01(instrVol);
    }

    private void InitializeSoundLibrary()
    {
        soundDictionary.Clear();
        foreach (var sound in soundLibrary)
        {
            if (!soundDictionary.ContainsKey(sound.soundName))
            {
                soundDictionary.Add(sound.soundName, sound);
            }
        }
    }

    public void PlaySound(string soundName, Action onComplete = null)
    {
        if (soundDictionary.TryGetValue(soundName, out SoundEntry sound))
        {
            sfxSource.pitch = sound.pitch;
            sfxSource.PlayOneShot(sound.clip, sound.volume);

            if (onComplete != null)
            {
                StartCoroutine(ExecuteAfterSound(sound.clip.length, onComplete));
            }
        }
        else
        {
            Debug.LogWarning($"El sonido '{soundName}' no se encuentra en la lista.");
        }
    }

    public void PlaySound(AudioClip clip, float volume = 1f)
    {
        if (clip == null)
        {
            Debug.LogWarning("AudioClip nulo en PlaySound(AudioClip)");
            return;
        }

        sfxSource.pitch = UnityEngine.Random.Range(1f, 1.05f); // opcional
        sfxSource.PlayOneShot(clip, volume);
    }

    public void PlaySound(AudioClip clip, Action onComplete)
    {
        if (clip == null)
        {
            Debug.LogWarning("AudioClip nulo en PlaySound con callback");
            return;
        }

        sfxSource.PlayOneShot(clip);

        if (onComplete != null)
            StartCoroutine(ExecuteAfterSound(clip.length, onComplete));
    }

    private IEnumerator ExecuteAfterSound(float duration, Action onComplete)
    {
        yield return new WaitForSeconds(duration);
        onComplete?.Invoke();
    }

    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        sfxSource.PlayOneShot(clip, volume);
    }

    public void PlayInstructionSound(AudioClip clip, Action onComplete = null)
    {
        if (clip == null || instructionSource == null)
        {
            base_PlayInstructionWithSFX(clip, onComplete);
            return;
        }

        if (currentInstructionCoroutine != null) StopCoroutine(currentInstructionCoroutine);
        instructionTimePosition = 0f;
        currentInstructionCoroutine = StartCoroutine(PlayInstructionCoroutine_NEW(clip, onComplete));
        OnInstructionStart?.Invoke();
    }

    private IEnumerator PlayInstructionCoroutine_NEW(AudioClip clip, Action onComplete)
    {
        instructionSource.clip = clip;
        instructionSource.time = instructionTimePosition;
        instructionSource.Play();
        while (instructionSource.isPlaying) yield return null;
        instructionTimePosition = 0f;
        OnInstructionEnd?.Invoke();
        onComplete?.Invoke();
    }

    void base_PlayInstructionWithSFX(AudioClip clip, Action onComplete)
    {
        if (currentInstructionCoroutine != null) StopCoroutine(currentInstructionCoroutine);
        instructionTimePosition = 0f;
        currentInstructionCoroutine = StartCoroutine(PlayInstructionCoroutine(clip, onComplete));
        OnInstructionStart?.Invoke();
    }

    public void StopInstructionSound()
    {
        if (instructionSource != null && instructionSource.isPlaying) instructionSource.Stop();
        if (sfxSource != null && sfxSource.isPlaying) sfxSource.Stop();
        if (currentInstructionCoroutine != null)
        {
            StopCoroutine(currentInstructionCoroutine);
            currentInstructionCoroutine = null;
        }
    }

    private IEnumerator PlayInstructionCoroutine(AudioClip clip, Action onComplete)
    {
        sfxSource.clip = clip;
        sfxSource.time = instructionTimePosition;
        sfxSource.Play();

        while (sfxSource.isPlaying)
        {
            yield return null;
        }

        instructionTimePosition = 0f;
        OnInstructionEnd?.Invoke();
        onComplete?.Invoke();
    }

    public void PlaySuccessSound(AudioClip specificClip = null)
    {
        if (specificClip != null)
        {
            sfxSource.PlayOneShot(specificClip);
        }
        else if (successSounds.Count > 0)
        {
            AudioClip randomSuccessClip = successSounds[UnityEngine.Random.Range(0, successSounds.Count)];
            sfxSource.PlayOneShot(randomSuccessClip);
        }
        else
        {
            Debug.LogWarning("No hay sonidos de éxito asignados en la lista.");
        }
    }

    public void PauseInstructions()
    {
        if (sfxSource.isPlaying)
        {
            instructionTimePosition = sfxSource.time; 
            sfxSource.Pause();
        }
    }

    public void ResumeInstructions()
    {
        if (instructionTimePosition > 0f)
        {
            sfxSource.time = instructionTimePosition;
            sfxSource.Play();
        }
    }

    /// Pausa la música y el sonido cuando el juego está pausado.
    public void PauseAudio()
    {
        if (musicSource.isPlaying)
        {
            wasMusicPlaying = true;
            musicSource.Pause();
        }
        else
        {
            wasMusicPlaying = false;
        }

        if (sfxSource.isPlaying)
        {
            wasSFXPlaying = true;
            sfxSource.Pause();
        }
        else
        {
            wasSFXPlaying = false;
        }
    }

    /// Reanuda la música y el sonido desde donde se quedaron.
    public void ResumeAudio()
    {
        if (wasMusicPlaying)
        {
            musicSource.UnPause();
        }

        if (wasSFXPlaying)
        {
            sfxSource.UnPause();
        }
    }

    // Nueva función para restaurar la música anterior
    public void RestorePreviousMusic(bool fallbackToDefault = true)
    {
        if (hasStoredPreviousMusic && previousMusicClip != null)
        {
            PlayMusic(previousMusicClip, previousMusicVolume);
            previousMusicClip = null;
            hasStoredPreviousMusic = false;
            return;
        }

        if (fallbackToDefault && defaultMusicClip != null)
            PlayMusic(defaultMusicClip, defaultMusicVolume);
        else
            StopMusic();
    }

    public void PlayMusic(AudioClip clip, float vol = 1f, bool loop = true)
    {
        if (musicSource == null || clip == null) return;

        currentMusicTrim = Mathf.Clamp01(vol);
        musicSource.Stop();
        musicSource.loop = loop;
        musicSource.clip = clip;
        musicSource.volume = Mathf.Clamp01(GetMasterVolume() * GetMusicVolume() * currentMusicTrim);
        musicSource.Play();
    }

    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        PlayMusic(clip, 1f, loop);
    }

    public AudioClip CurrentMusicClip() => musicSource ? musicSource.clip : null;
    public bool IsMusicPlaying() => musicSource && musicSource.isPlaying;

    public void StopMusic()
    {
        if (musicSource == null) return;
        musicSource.Stop();
        musicSource.clip = null;
    }

    public void SetDefaultMusic(AudioClip clip, float volume = 1f)
    {
        defaultMusicClip = clip;
        defaultMusicVolume = volume;
    }
    public bool IsInstructionPlaying()
    {
        return sfxSource.isPlaying && currentInstructionCoroutine != null;
    }

    public void PlayLoop(string soundName, float volumeOverride = 1f)
    {
        // Si ya está sonando, lo detenemos primero
        StopLoop(soundName);

        if (!soundDictionary.TryGetValue(soundName, out SoundEntry sound))
        {
            Debug.LogWarning($"❌ El sonido '{soundName}' no se encuentra en la soundLibrary.");
            return;
        }

        GameObject loopObj = new GameObject("LoopSound_" + soundName);
        AudioSource loopSource = loopObj.AddComponent<AudioSource>();
        loopSource.clip = sound.clip;
        loopSource.volume = sound.volume * volumeOverride;
        loopSource.pitch = sound.pitch;
        loopSource.loop = true;
        loopSource.Play();

        activeLoops[soundName] = loopSource;
    }

    public void StopLoop(string soundName)
    {
        if (activeLoops.TryGetValue(soundName, out AudioSource source))
        {
            if (source != null)
                Destroy(source.gameObject);

            activeLoops.Remove(soundName);
        }
    }

    public void LowerMusicVolume(float newVolume = 0f)
    {
        if (musicSource == null) return;

        if (savedMusicVolume < 0f) // guardar solo una vez
            savedMusicVolume = musicSource.volume;

        musicSource.volume = newVolume;
    }

    public void RestoreMusicVolume()
    {
        if (musicSource == null || savedMusicVolume < 0f) return;

        musicSource.volume = savedMusicVolume;
        savedMusicVolume = -1f;
    }

    // PARA LLAMAR DESDE LOS SETTERS O EL SLIDER
    public void PlayPreviewSample(AudioChannel ch)
    {
        if (Time.unscaledTime - lastSampleTime < sampleMinInterval) return; // anti-doble tap
        lastSampleTime = Time.unscaledTime;

        AudioClip clip = null;
        float chVol = 1f;
        switch (ch)
        {
            case AudioChannel.Music: clip = sampleMusic; chVol = GetMusicVolume(); break;
            case AudioChannel.SFX: clip = sampleSFX; chVol = GetSFXVolume(); break;
            case AudioChannel.Instruction: clip = sampleInstruction; chVol = GetInstrVolume(); break;
            default: clip = sampleSFX; chVol = GetMasterVolume(); break;
        }
        if (clip == null || uiSampleSource == null) return;

        StopPreviewSamples(); // corta cualquier sample previo

        uiSampleSource.volume = Mathf.Clamp01(GetMasterVolume() * chVol);
        uiSampleSource.clip = clip;
        uiSampleSource.time = 0f;
        uiSampleSource.loop = false;
        uiSampleSource.Play();
    }

    public void StopPreviewSamples(bool hardStop = true)
    {
        if (uiSampleSource == null) return;
        if (uiSampleFade != null) { StopCoroutine(uiSampleFade); uiSampleFade = null; }
        if (hardStop) uiSampleSource.Stop();
        else uiSampleFade = StartCoroutine(FadeOutAndStop(uiSampleSource, 0.05f));
    }

    IEnumerator FadeOutAndStop(AudioSource src, float t)
    {
        if (!src.isPlaying) yield break;
        float start = src.volume, e = 0f;
        while (e < t) { e += Time.unscaledDeltaTime; src.volume = Mathf.Lerp(start, 0f, e / t); yield return null; }
        src.Stop(); src.volume = start;
    }


    public void ApplyVolumes(bool smoothMusic = false)
    {
        float master = Mathf.Clamp01(GetMasterVolume());

        if (musicSource)
        {
            float target = Mathf.Clamp01(master * GetMusicVolume() * currentMusicTrim);
            if (!smoothMusic) musicSource.volume = target;
            else
            {
                if (musicSmoothCR != null) StopCoroutine(musicSmoothCR);
                musicSmoothCR = StartCoroutine(SmoothVolume(musicSource, target, 0.08f));
            }
        }
        if (sfxSource) sfxSource.volume = Mathf.Clamp01(master * GetSFXVolume());
        if (instructionSource) instructionSource.volume = Mathf.Clamp01(master * GetInstrVolume());
    }

    public void SetActivityMusic(AudioClip newMusic, float volume = 0.2f, bool restartIfSame = false)
    {
        if (musicSource == null || newMusic == null) return;

        bool same = (musicSource.clip == newMusic);

        // Guarda "previous" solo una vez y solo si cambias a otra pista
        if (!hasStoredPreviousMusic && musicSource.clip != null && !same)
        {
            previousMusicClip = musicSource.clip;
            previousMusicVolume = currentMusicTrim; // o musicSource.volume si prefieres absoluto
            hasStoredPreviousMusic = true;
        }

        // Trim relativo para esta pista
        currentMusicTrim = Mathf.Clamp01(volume);

        // 🔸 Si es la MISMA pista y NO quieres reiniciar, solo ajusta volumen suavemente
        if (same && !restartIfSame)
        {
            ApplyVolumes(smoothMusic: true);
            return;
        }

        // 🔸 Si es distinta (o pediste reinicio), sí cambia el clip
        PlayMusic(newMusic, currentMusicTrim, loop: true);
    }

    [Obsolete("Usa SetActivityMusic(clip, volume, restartIfSame) si necesitas controlar el reinicio.")]
    public void SetActivityMusic(AudioClip newMusic)
    => SetActivityMusic(newMusic, 0.2f, false);

    [Obsolete("Usa SetActivityMusic(clip, volume, restartIfSame) si necesitas controlar el reinicio.")]
    public void SetActivityMusic(AudioClip newMusic, float volume)
        => SetActivityMusic(newMusic, volume, false);

    IEnumerator SmoothVolume(AudioSource src, float target, float dur)
    {
        float start = src.volume, t = 0f;
        while (t < dur) { t += Time.unscaledDeltaTime; src.volume = Mathf.Lerp(start, target, t / dur); yield return null; }
        src.volume = target; musicSmoothCR = null;
    }

    public void SetSceneBaseMusic(AudioClip clip, float volTrim = 1f, bool force = true)
    {
        if (clip == null || musicSource == null) return;

        hasStoredPreviousMusic = false;
        previousMusicClip = null;

        // Actualiza tu "default" (útil para RestorePreviousMusic sin fallback extraño)
        defaultMusicClip = clip;
        defaultMusicVolume = Mathf.Clamp01(volTrim);

        // Reproduce o reaplica volúmenes
        if (force || CurrentMusicClip() != clip)
            PlayMusic(clip, defaultMusicVolume, loop: true);
        else
            ApplyVolumes(smoothMusic: false); // recalcula Master*Music*trim
    }

    // Crossfade de música usando el MISMO musicSource (fade out → swap clip → fade in).
    public void CrossfadeMusic(AudioClip newClip, float fadeOut = 0.5f, float fadeIn = 0.6f, float volumeTrim = 0.3f, bool loop = true, bool restartIfSame = false)
    {
        if (musicSource == null || newClip == null) return;

        // Si es la misma pista y no quieres reiniciar, solo ajusta volumen suavemente.
        if (musicSource.clip == newClip && !restartIfSame)
        {
            currentMusicTrim = Mathf.Clamp01(volumeTrim);
            ApplyVolumes(smoothMusic: true); // respeta Master * Music * Trim y el slider del menú
            return;
        }

        // Guarda "previous" si estamos cambiando de pista
        if (!hasStoredPreviousMusic && musicSource.clip != null && musicSource.clip != newClip)
        {
            previousMusicClip = musicSource.clip;
            previousMusicVolume = currentMusicTrim;
            hasStoredPreviousMusic = true;
        }

        StartCoroutine(CrossfadeMusicCR(newClip, fadeOut, fadeIn, Mathf.Clamp01(volumeTrim), loop));
    }

    private IEnumerator CrossfadeMusicCR(AudioClip newClip, float fadeOut, float fadeIn, float volumeTrim, bool loop)
    {
        // 1) Fade out de la música actual (respetando Master/Music del slider)
        if (musicSmoothCR != null) StopCoroutine(musicSmoothCR);
        float targetOut = 0f;
        musicSmoothCR = StartCoroutine(SmoothVolume(musicSource, targetOut, Mathf.Max(0.01f, fadeOut)));
        yield return musicSmoothCR; // espera a que termine

        // 2) Swap de clip (con volumen 0), mantener integración con sliders
        musicSource.Stop();
        musicSource.clip = newClip;
        musicSource.loop = loop;

        currentMusicTrim = volumeTrim; // trim relativo de la pista nueva
        ApplyVolumes(smoothMusic: false); // calcula Master*Music*Trim → pone volumen "target"
        float targetIn = musicSource.volume; // este es el volumen correcto según el menú

        musicSource.volume = 0f; // arrancar desde 0 para el fade-in
        musicSource.Play();

        // 3) Fade in hasta el volumen objetivo calculado
        musicSmoothCR = StartCoroutine(SmoothVolume(musicSource, targetIn, Mathf.Max(0.01f, fadeIn)));
        yield return musicSmoothCR;
        musicSmoothCR = null;
    }

    // Atajo: crossfade a la música previa (si fue almacenada por SetActivityMusic/CrossfadeMusic)
    public void CrossfadeToPrevious(float fadeOut = 0.4f, float fadeIn = 0.6f)
    {
        if (!hasStoredPreviousMusic || previousMusicClip == null)
        {
            RestorePreviousMusic(fallbackToDefault: true); // usa el flujo existente si no hay previa
            return;
        }
        CrossfadeMusic(previousMusicClip, fadeOut, fadeIn, previousMusicVolume, loop: true, restartIfSame: false);
    }
}
