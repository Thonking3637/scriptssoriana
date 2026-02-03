using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using DG.Tweening;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════════════════
    // EVENTO GLOBAL DE PAUSA
    // Cualquier sistema puede suscribirse para reaccionar a la pausa
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Evento que se dispara cuando el juego se pausa o reanuda.
    /// true = pausado, false = reanudado
    /// </summary>
    public static event Action<bool> OnGamePaused;

    [Header("UI Elements")]
    public GameObject pausePanel;
    public CanvasGroup fadePanel;
    public float fadeDuration = 0.5f;

    [Header("Botón de Pausa")]
    public GameObject pauseButton;
    public GameObject resumeButton;

    [Header("Gestor de Introducción")]
    public IntroManager introManager;
    public string introMessage = "Iniciando Día 1";

    private bool isPaused = false;

    public static GameManager Instance;

    [Header("Configuración de Actividades")]
    [Tooltip("Indica en qué actividad comenzará el juego (se puede cambiar en el editor).")]
    public int startActivityIndex = 0;

    private int currentActivityIndex;
    public List<ActivityBase> activities = new List<ActivityBase>();

    [Serializable]
    public class ActivityConfig
    {
        public GameObject[] objectsToActivate;
        public GameObject[] objectsToDeactivate;
    }

    public List<ActivityConfig> activityConfigs;

    public event Action<int> OnActivityChange;
    public event Action OnTrainingComplete;

    private ActivityBase currentActivity;
    private SmoothCameraController cameraController;

    public static event Action<string> OnSceneProgressChanged;

    // ═══════════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        InitializeActivities();

        var scene = SceneManager.GetActiveScene().name;
        CompletionService.SetSceneActivityCount(scene, activities.Count);

        OnSceneProgressChanged?.Invoke(scene);

        DeactivateAllActivities();

        startActivityIndex = ActivityLauncher.Instance != null ?
                             ActivityLauncher.Instance.activityIndexToStart :
                             startActivityIndex;

        currentActivityIndex = startActivityIndex;

        if (fadePanel == null)
            fadePanel = FindObjectOfType<CanvasGroup>(true);

        pausePanel.SetActive(false);

        if (introManager != null)
        {
            if (fadePanel)
            {
                fadePanel.DOKill();
                fadePanel.gameObject.SetActive(true);
                fadePanel.alpha = 1f;
                introManager.fadeCanvas = fadePanel;
            }

            introManager.ShowIntro(introMessage, StartNextActivity);
            return;
        }

        if (fadePanel)
        {
            fadePanel.alpha = 1f;
            fadePanel.DOFade(0, fadeDuration).SetUpdate(true);
        }

        StartNextActivity();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SISTEMA DE PAUSA (Event-Driven)
    // ═══════════════════════════════════════════════════════════════════════════
  

    /// <summary>
    /// Establece el estado de pausa directamente.
    /// </summary>
    public void SetPaused(bool paused)
    {
        if (isPaused == paused) return;

        isPaused = paused;

        // 1. Time scale
        Time.timeScale = isPaused ? 0f : 1f;

        // 2. UI de pausa
        UpdatePauseUI();

        // 3. Notificar a TODOS los sistemas suscritos
        OnGamePaused?.Invoke(isPaused);

        Debug.Log($"[GameManager] Juego {(isPaused ? "PAUSADO" : "REANUDADO")}");
    }

    /// <summary>
    /// Reanuda el juego (llamado desde botón Resume).
    /// </summary>
    public void ResumeGame()
    {
        SetPaused(false);
    }

    /// <summary>
    /// Actualiza la UI de pausa.
    /// </summary>
    private void UpdatePauseUI()
    {
        if (pausePanel != null)
            pausePanel.SetActive(isPaused);

        if (pauseButton != null)
            pauseButton.SetActive(!isPaused);

        if (resumeButton != null)
            resumeButton.SetActive(isPaused);
    }

    /// <summary>
    /// Propiedad para verificar si el juego está pausado.
    /// </summary>
    public bool IsPaused => isPaused;

    // ═══════════════════════════════════════════════════════════════════════════
    // ACTIVIDADES
    // ═══════════════════════════════════════════════════════════════════════════

    private void InitializeActivities()
    {
        if (activities.Count == 0)
        {
            Debug.LogError("No hay actividades asignadas en el Inspector. Asegúrate de agregarlas en el GameManager.");
            return;
        }

        cameraController = FindObjectOfType<SmoothCameraController>();

        Debug.Log($"Total de actividades registradas: {activities.Count}");
    }

    private void StartNextActivity()
    {
        if (activities.Count == 0)
        {
            Debug.LogError("No hay actividades registradas.");
            return;
        }

        if (currentActivityIndex >= activities.Count)
        {
            Debug.Log("Entrenamiento completado. Volviendo al menú...");
            OnTrainingComplete?.Invoke();
            ReturnToMenu();
            return;
        }

        DeactivateAllActivities();

        ApplyActivityConfig(currentActivityIndex);

        currentActivity = activities[currentActivityIndex];

        if (currentActivity == null)
        {
            Debug.LogError($"[GameManager] La actividad en el índice {currentActivityIndex} es NULL.");
            return;
        }

        currentActivity.gameObject.SetActive(true);
        currentActivity.OnActivityComplete += HandleActivityComplete;

        if (cameraController != null && !string.IsNullOrEmpty(currentActivity.startCameraPosition))
        {
            cameraController.InitializeCameraPosition(currentActivity.startCameraPosition);
        }

        OnActivityChange?.Invoke(currentActivityIndex);
        currentActivity.StartActivity();
    }

    private void ApplyActivityConfig(int index)
    {
        if (activityConfigs == null || index < 0 || index >= activityConfigs.Count) return;

        var config = activityConfigs[index];
        if (config == null) return;

        if (config.objectsToActivate != null)
        {
            foreach (var obj in config.objectsToActivate)
            {
                if (obj != null) obj.SetActive(true);
            }
        }

        if (config.objectsToDeactivate != null)
        {
            foreach (var obj in config.objectsToDeactivate)
            {
                if (obj != null) obj.SetActive(false);
            }
        }
    }

    private void DeactivateAllActivities()
    {
        foreach (var activity in activities)
        {
            if (activity != null)
            {
                activity.gameObject.SetActive(false);
            }
        }
    }

    private void HandleActivityComplete()
    {
        if (currentActivity != null)
        {
            currentActivity.OnActivityComplete -= HandleActivityComplete;
        }
        else
        {
            Debug.LogWarning("[GameManager] HandleActivityComplete llamado pero currentActivity es NULL. " +
                           "Esto puede ocurrir si la actividad no fue iniciada por GameManager.");
        }

        string scene = SceneManager.GetActiveScene().name;

        // 1) Local (PlayerPrefs)
        CompletionService.MarkActivity(scene, currentActivityIndex);

        // 2) REMOTO (Firestore)
        if (ProgressService.Instance != null)
        {
            ProgressService.Instance.CommitMedal(scene, currentActivityIndex);
        }

        // Notificar progreso
        OnSceneProgressChanged?.Invoke(scene);

        var (done, total) = CompletionService.GetSceneProgress(scene);
        bool sceneNowComplete = (total > 0 && done >= total);

        if (sceneNowComplete)
        {
            CompletionService.MarkSceneCompleted(scene, true);
            OnSceneProgressChanged?.Invoke(scene);
        }

        bool isSingleActivityMode = ActivityLauncher.Instance != null;
        bool isLastInSequence = currentActivityIndex >= activities.Count - 1;

        if (isSingleActivityMode || isLastInSequence)
        {
            OnTrainingComplete?.Invoke();
            ReturnToMenu();
            return;
        }

        currentActivityIndex++;
        StartNextActivity();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // NAVEGACIÓN
    // ═══════════════════════════════════════════════════════════════════════════

    public void ReturnToMenu()
    {
        // Limpiar el ActivityLauncher si existe
        if (ActivityLauncher.Instance != null)
        {
            Destroy(ActivityLauncher.Instance.gameObject);
        }

        if (fadePanel != null)
        {
            fadePanel.DOFade(1, fadeDuration).OnComplete(() =>
            {
                Time.timeScale = 1;
                DOTween.KillAll();
                Destroy(gameObject);
                LoadingScreen.LoadScene("Menu");
            });
        }
        else
        {
            Time.timeScale = 1;
            DOTween.KillAll();
            Destroy(gameObject);
            LoadingScreen.LoadScene("Menu");
        }
    }
    // ═══════════════════════════════════════════════════════════════════════════
    // MENU PAUSE
    // ═══════════════════════════════════════════════════════════════════════════

    public void TogglePause()
    {
        SetPaused(!isPaused);
    }

    public void ReturnToMenuImmediate()
    {
        Time.timeScale = 1;

        if (ActivityLauncher.Instance != null)
        {
            Destroy(ActivityLauncher.Instance.gameObject);
        }

        if (fadePanel != null)
        {
            fadePanel.DOKill();
            fadePanel.DOFade(1, fadeDuration).OnComplete(() =>
            {
                DOTween.KillAll();
                Destroy(gameObject);
                LoadingScreen.LoadScene("Menu");
            });
        }
        else
        {
            DOTween.KillAll();
            Destroy(gameObject);
            LoadingScreen.LoadScene("Menu");
        }
    }

    public void RestartCurrentActivity()
    {
        SetPaused(false);

        // 1. Detener TODO el audio inmediatamente
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.StopInstructionSound();
            // Opcional: también bajar música
            SoundManager.Instance.LowerMusicVolume(0f);
        }

        // 2. Configurar ActivityLauncher
        if (ActivityLauncher.Instance != null)
        {
            ActivityLauncher.Instance.activityIndexToStart = currentActivityIndex;
        }
        else
        {
            var launcher = new GameObject("ActivityLauncher").AddComponent<ActivityLauncher>();
            launcher.activityIndexToStart = currentActivityIndex;
            DontDestroyOnLoad(launcher.gameObject);
        }

        // 3. Fade out y recargar
        string currentScene = SceneManager.GetActiveScene().name;

        if (fadePanel != null)
        {
            fadePanel.DOKill();
            fadePanel.DOFade(1, fadeDuration).OnComplete(() =>
            {
                DOTween.KillAll();
                Destroy(gameObject);
                LoadingScreen.LoadScene(currentScene);
            });
        }
        else
        {
            DOTween.KillAll();
            Destroy(gameObject);
            LoadingScreen.LoadScene(currentScene);
        }
    }

    public string GetCurrentActivityName()
    {
        if (currentActivityIndex >= 0 && currentActivityIndex < activities.Count && activities[currentActivityIndex] != null)
        {
            return activities[currentActivityIndex].name;
        }
        return string.Empty;
    }


    // ═══════════════════════════════════════════════════════════════════════════
    // CLEANUP
    // ═══════════════════════════════════════════════════════════════════════════

    private void OnDestroy()
    {
        // Desuscribirse de la actividad actual si existe
        if (currentActivity != null)
        {
            currentActivity.OnActivityComplete -= HandleActivityComplete;
        }

        // Asegurar que el juego no quede pausado
        Time.timeScale = 1;
    }
}