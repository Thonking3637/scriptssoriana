using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// Base PRO de 3 fases para actividades: Tutorial → Practice → Summary
/// ✔ Sub-pasos de tutorial (tutorialStep + NextTutorialStep())
/// ✔ Timer opcional de práctica
/// ✔ Música por fase (crossfade vía SoundManager, respeta sliders)
/// ✔ SFX de inicio/fin de fase
/// ✔ Métricas comunes (aciertos/errores/despachos/tiempo)
/// ✔ Lock/Unlock de input (global y por instancia)
/// ✔ Eventos globales OnPhaseChanged
/// ✔ Helpers de cámara por fase
/// </summary>
public abstract class PhasedActivityBasePro : ActivityBase
{
    // ─────────────────────────────────────────────────────────────────────────────
    // FASES
    // ─────────────────────────────────────────────────────────────────────────────
    public enum Phase { None, Tutorial, Practice, Summary }
    public static event Action<Phase> OnPhaseChangedGlobal;

    [Header("Fases")]
    [SerializeField] protected bool autoStartOnEnable = false;
    [SerializeField] protected bool usePracticeTimer = false;
    [SerializeField] protected int practiceDurationSeconds = 60;

    [Header("Música por fase (opcional)")]
    public AudioClip musicTutorial;
    public AudioClip musicPractice;
    public AudioClip musicSummary;
    [Range(0f, 1f)] public float musicTutorialVol = 0.25f;
    [Range(0f, 1f)] public float musicPracticeVol = 0.35f;
    [Range(0f, 1f)] public float musicSummaryVol = 0.30f;
    public float musicFadeOut = 0.5f;
    public float musicFadeIn = 0.8f;

    [Header("SFX de fase (keys en SoundManager)")]
    public string sfxPhaseStart = "fase_inicio";
    public string sfxPhaseEnd = "fase_fin";

    [Header("Cámara por fase (opcional)")]
    public string camTutorialAnchor;
    public string camPracticeAnchor;
    public string camSummaryAnchor;

    // Estado
    protected Phase currentPhase = Phase.None;
    private bool _phaseStartedOnce;

    // ─────────────────────────────────────────────────────────────────────────────
    // TUTORIAL: SUB-PASOS
    // ─────────────────────────────────────────────────────────────────────────────
    protected int tutorialStep = 0;
    /// <summary>
    /// Avanza al siguiente paso y espera el audio actual antes de pasar al siguiente.
    /// Usa el onComplete real de ActivityBase.UpdateInstructionOnce.
    /// </summary>
    public void NextTutorialStep()
    {
        // Si ya hay un audio de instrucción sonando, espera a que termine antes de pasar
        if (soundManager != null && soundManager.IsInstructionPlaying)
        {
            StartCoroutine(WaitForInstructionEnd(() => GoToTutorialStep(tutorialStep + 1)));
        }
        else
        {
            GoToTutorialStep(tutorialStep + 1);
        }
    }

    private IEnumerator WaitForInstructionEnd(Action next)
    {
        yield return new WaitUntil(() => soundManager == null || !soundManager.IsInstructionPlaying);
        yield return new WaitForSeconds(0.05f); // pequeño margen
        next?.Invoke();
    }
    /// <summary>Salta a un paso de tutorial (0..N) y llama OnTutorialStep(step).</summary>
    public void GoToTutorialStep(int step)
    {
        tutorialStep = Mathf.Max(0, step);
        OnTutorialStep(tutorialStep);
    }
    /// <summary>Implementa tu lógica por paso (switch/casos).</summary>
    protected virtual void OnTutorialStep(int step) { }

    // ─────────────────────────────────────────────────────────────────────────────
    // MÉTRICAS COMUNES
    // ─────────────────────────────────────────────────────────────────────────────
    //[Header("Métricas (lectura)")]
    public int aciertosTotales { get; protected set; }
    public int erroresTotales { get; protected set; }
    public int despachosTotales { get; protected set; }
    public float tiempoEmpleado { get; protected set; } // segundos (solo práctica)

    float _practiceStartTime;

    protected void RecordSuccess(int inc = 1) { aciertosTotales += Mathf.Max(0, inc); }
    protected void RecordError(int inc = 1) { erroresTotales += Mathf.Max(0, inc); }
    protected void RecordDespacho(int inc = 1) { despachosTotales += Mathf.Max(0, inc); }

    public void ReportSuccess(int inc = 1) { RecordSuccess(inc); }
    public void ReportError(int inc = 1) { RecordError(inc); }
    public void ReportDespacho(int inc = 1) { RecordDespacho(inc); }

    // ─────────────────────────────────────────────────────────────────────────────
    // INPUT LOCK
    // ─────────────────────────────────────────────────────────────────────────────
    public static bool InputLockedGlobal { get; private set; }
    public bool inputLocked { get; private set; }

    public void LockInput(float seconds = 0f)
    {
        inputLocked = true;
        InputLockedGlobal = true;
        if (seconds > 0f) StartCoroutine(CoUnlockAfter(seconds));
    }
    public void UnlockInput()
    {
        inputLocked = false;
        InputLockedGlobal = false;
    }
    IEnumerator CoUnlockAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        UnlockInput();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // TIMER PRÁCTICA
    // ─────────────────────────────────────────────────────────────────────────────
    protected Coroutine timerCo;
    protected int timeLeft; // segundos

    IEnumerator CoPracticeTimer()
    {
        while (timeLeft >= 0)
        {
            OnPracticeTimerTick(timeLeft);
            yield return new WaitForSeconds(1f);
            timeLeft--;
        }
        EndPractice(); // auto-fin
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // HOOKS DE VIDA
    // ─────────────────────────────────────────────────────────────────────────────
    protected virtual void OnEnable()
    {
        if (autoStartOnEnable)
            StartPhasedActivity();
    }

    /// <summary>Inicia el flujo desde Tutorial.</summary>
    public virtual void StartPhasedActivity()
    {
        if (_phaseStartedOnce) return;     // evita doble arranque
        _phaseStartedOnce = true;

        // Reset métricas y arranque normal
        aciertosTotales = erroresTotales = despachosTotales = 0;
        tiempoEmpleado = 0f;
        StartTutorial();
    }

    protected override void OnDisable()
    {
        base.OnDisable();  
        _phaseStartedOnce = false;         // para poder reiniciar al reactivar
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // TUTORIAL
    // ─────────────────────────────────────────────────────────────────────────────
    public virtual void StartTutorial()
    {
        ChangePhase(Phase.Tutorial);

        if (musicTutorial)
            SoundManager.Instance?.CrossfadeMusic(musicTutorial, musicFadeOut, musicFadeIn, musicTutorialVol, true);

        FocusCamera(camTutorialAnchor);
        PlaySfxSafe(sfxPhaseStart);

        OnTutorialStart();
    }

    /// <summary>Cuando tu tutorial termine (condición propia), llama esto para pasar a Práctica.</summary>
    protected void CompleteTutorial_StartPractice()
    {
        PlaySfxSafe(sfxPhaseEnd);
        StartPractice();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // PRÁCTICA
    // ─────────────────────────────────────────────────────────────────────────────
    public virtual void StartPractice()
    {
        ChangePhase(Phase.Practice);

        if (musicPractice)
            SoundManager.Instance?.CrossfadeMusic(musicPractice, musicFadeOut, musicFadeIn, musicPracticeVol, true);

        FocusCamera(camPracticeAnchor);
        PlaySfxSafe(sfxPhaseStart);

        _practiceStartTime = Time.time;

        OnPracticeStart();

        if (usePracticeTimer && practiceDurationSeconds > 0)
        {
            if (timerCo != null) StopCoroutine(timerCo);
            timeLeft = practiceDurationSeconds;
            timerCo = StartCoroutine(CoPracticeTimer());
        }
    }

    /// <summary>Finaliza la práctica (por timer o manual) y abre Summary.</summary>
    public virtual void EndPractice()
    {
        if (timerCo != null) { StopCoroutine(timerCo); timerCo = null; }

        tiempoEmpleado += Mathf.Max(0f, Time.time - _practiceStartTime);

        ChangePhase(Phase.Summary);

        if (musicSummary)
            SoundManager.Instance?.CrossfadeMusic(musicSummary, musicFadeOut, musicFadeIn, musicSummaryVol, true);
        else
            SoundManager.Instance?.CrossfadeToPrevious(0.5f, 0.6f);

        FocusCamera(camSummaryAnchor);
        PlaySfxSafe(sfxPhaseEnd);

        OnPracticeEnd();
        OnSummaryStart();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────────────────────
    protected void ChangePhase(Phase phase)
    {
        currentPhase = phase;
        try { OnPhaseChangedGlobal?.Invoke(phase); } catch { }
    }

    protected void FocusCamera(string anchorKey)
    {
        if (!cameraController) return;
        if (string.IsNullOrEmpty(anchorKey)) return;
        cameraController.MoveToPosition(anchorKey, null);
    }

    protected void PlaySfxSafe(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        try { SoundManager.Instance.PlaySound(key); } catch { }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // HOOKS PARA HIJAS
    // ─────────────────────────────────────────────────────────────────────────────
    /// <summary>Configura cámaras/paneles/instrucciones del tutorial.</summary>
    protected abstract void OnTutorialStart();

    /// <summary>Configura la práctica. No asumas timer; revisa usePracticeTimer.</summary>
    protected abstract void OnPracticeStart();

    /// <summary>Tick del timer (solo si usePracticeTimer=true).</summary>
    protected virtual void OnPracticeTimerTick(int secondsLeft) { }

    /// <summary>Se llama al terminar práctica (por timer o manual).</summary>
    protected virtual void OnPracticeEnd() { }

    /// <summary>Configura el resumen final. Llama CompleteActivity() cuando quieras cerrar.</summary>
    protected abstract void OnSummaryStart();
}
