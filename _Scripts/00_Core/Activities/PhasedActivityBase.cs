using UnityEngine;
using System.Collections;

/// <summary>
/// Base genérica de 3 fases: Tutorial → Practice → Summary.
/// ✔ Opcional: práctica con timer o sin timer
/// ✔ Crossfade de música usando SoundManager (sin mixers ni nuevas vars)
/// ✔ Hooks claros para que la actividad hija solo implemente lo propio
/// ✔ Transiciones manuales o automáticas (tú decides)
/// </summary>
public abstract class PhasedActivityBase : ActivityBase
{
    public enum Phase { None, Tutorial, Practice, Summary }
    [Header("Fases")]
    [SerializeField] protected bool autoStartOnEnable = true;
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

    // Estado
    protected Phase currentPhase = Phase.None;

    // Timer práctica (si se usa)
    protected Coroutine timerCo;
    protected int timeLeft; // segundos restantes

    protected virtual void OnEnable()
    {
        if (autoStartOnEnable)
        {
            // Arrancamos toda la actividad desde Tutorial
            StartPhasedActivity();
        }
    }

    /// <summary> Arranca el flujo por fases desde Tutorial. Llamable manualmente. </summary>
    public virtual void StartPhasedActivity()
    {
        StartTutorial(); // puedes sobreescribir si necesitas gating antes
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // TUTORIAL
    // ─────────────────────────────────────────────────────────────────────────────
    public virtual void StartTutorial()
    {
        currentPhase = Phase.Tutorial;

        // Música
        if (musicTutorial)
            SoundManager.Instance?.CrossfadeMusic(musicTutorial, fadeOut: musicFadeOut, fadeIn: musicFadeIn, volumeTrim: musicTutorialVol, loop: true);

        // Hook para la hija
        OnTutorialStart();
    }

    /// <summary> Llama esto cuando TU lógica de tutorial termine (botón, condición, etc.). </summary>
    protected void CompleteTutorial_StartPractice()
    {
        StartPractice();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // PRACTICE
    // ─────────────────────────────────────────────────────────────────────────────
    public virtual void StartPractice()
    {
        currentPhase = Phase.Practice;

        if (musicPractice)
            SoundManager.Instance?.CrossfadeMusic(musicPractice, fadeOut: musicFadeOut, fadeIn: musicFadeIn, volumeTrim: musicPracticeVol, loop: true);

        OnPracticeStart();

        if (usePracticeTimer && practiceDurationSeconds > 0)
        {
            // Timer automático
            if (timerCo != null) StopCoroutine(timerCo);
            timeLeft = practiceDurationSeconds;
            timerCo = StartCoroutine(CoPracticeTimer());
        }
        // Si NO usa timer: la hija debe llamar EndPractice() cuando lo decida.
    }

    IEnumerator CoPracticeTimer()
    {
        while (timeLeft >= 0)
        {
            OnPracticeTimerTick(timeLeft);
            yield return new WaitForSeconds(1f);
            timeLeft--;
        }
        EndPractice();
    }

    /// <summary> Llama esto para terminar práctica (sea por timer o manual). </summary>
    public virtual void EndPractice()
    {
        if (timerCo != null) { StopCoroutine(timerCo); timerCo = null; }

        currentPhase = Phase.Summary;

        if (musicSummary)
            SoundManager.Instance?.CrossfadeMusic(musicSummary, fadeOut: musicFadeOut, fadeIn: musicFadeIn, volumeTrim: musicSummaryVol, loop: true);
        else
            SoundManager.Instance?.CrossfadeToPrevious(0.5f, 0.6f);

        OnPracticeEnd();
        OnSummaryStart();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // HOOKS PARA LA ACTIVIDAD HIJA
    // ─────────────────────────────────────────────────────────────────────────────
    /// <summary> Configura cámaras/paneles/instrucciones del tutorial. </summary>
    protected abstract void OnTutorialStart();

    /// <summary> Configura la práctica. No asumas timer; revisa usePracticeTimer. </summary>
    protected abstract void OnPracticeStart();

    /// <summary> Tick del timer (solo si usePracticeTimer=true). </summary>
    protected virtual void OnPracticeTimerTick(int secondsLeft) { }

    /// <summary> Se llama al terminar práctica (por timer o manual). </summary>
    protected virtual void OnPracticeEnd() { }

    /// <summary> Configura el resumen final. Llama CompleteActivity() cuando quieras cerrar. </summary>
    protected abstract void OnSummaryStart();
}
