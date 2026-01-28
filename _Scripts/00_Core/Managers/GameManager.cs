using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using DG.Tweening;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject pausePanel;
    public CanvasGroup fadePanel;
    public float fadeDuration = 0.5f;

    [Header("Botón de Pausa")]
    public GameObject pauseButton;
    public GameObject resumeButton;
    public Image pauseButtonImage;
    public Sprite normalPauseSprite;
    public Sprite disabledPauseSprite;

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

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.OnInstructionStart += DisablePauseButton;
                SoundManager.Instance.OnInstructionEnd += EnablePauseButton;
            }
            DisablePauseButton();

            introManager.ShowIntro(introMessage, StartNextActivity);
            return;
        }

        if (fadePanel)
        {
            fadePanel.alpha = 1f;
            fadePanel.DOFade(0, fadeDuration).SetUpdate(true);
        }

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.OnInstructionStart += DisablePauseButton;
            SoundManager.Instance.OnInstructionEnd += EnablePauseButton;
        }
        DisablePauseButton();

        StartNextActivity();
    }

    private void DisablePauseButton()
    {
        if (pauseButtonImage != null)
        {
            pauseButtonImage.sprite = disabledPauseSprite;
            pauseButtonImage.color = new Color(1, 1, 1, 0.5f);
        }

        if (pauseButton != null)
        {
            var btn = pauseButton.GetComponent<Button>();
            if (btn != null) btn.interactable = false;
        }
    }

    private void EnablePauseButton()
    {
        if (pauseButtonImage != null)
        {
            pauseButtonImage.sprite = normalPauseSprite;
            pauseButtonImage.color = new Color(1, 1, 1, 1f);
        }

        if (pauseButton != null)
        {
            var btn = pauseButton.GetComponent<Button>();
            if (btn != null) btn.interactable = true;
        }
    }

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

        // ✅ FIX: Verificar que currentActivity no sea null
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

    // ✅ FIX PRINCIPAL: Verificación de null en HandleActivityComplete
    private void HandleActivityComplete()
    {
        // ✅ FIX: Verificar que currentActivity no sea null antes de usarlo
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

    public void ReturnToMenu()
    {
        // Limpiar el ActivityLauncher si existe
        if (ActivityLauncher.Instance != null)
        {
            Destroy(ActivityLauncher.Instance.gameObject);
        }

        // ✅ FIX: Verificar fadePanel antes de usar
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
            // Fallback si no hay fadePanel
            Time.timeScale = 1;
            DOTween.KillAll();
            Destroy(gameObject);
            LoadingScreen.LoadScene("Menu");
        }
    }

    public void ReturnToMenuImmediate()
    {
        // 1. Detener sonidos/tweens primero
        DOTween.KillAll();
        Time.timeScale = 1;

        // 2. Limpiar el ActivityLauncher si existe
        if (ActivityLauncher.Instance != null)
        {
            Destroy(ActivityLauncher.Instance.gameObject);
        }

        // 3. Cargar el menú con fade out
        if (fadePanel != null)
        {
            fadePanel.DOFade(1, fadeDuration).OnComplete(() =>
            {
                Destroy(gameObject);
                LoadingScreen.LoadScene("Menu");
            });
        }
        else
        {
            Destroy(gameObject);
            LoadingScreen.LoadScene("Menu");
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

    /// <summary>
    /// Activa o desactiva la pausa del juego.
    /// </summary>
    public void TogglePause()
    {
        isPaused = !isPaused;

        if (isPaused)
        {
            Time.timeScale = 0;
            if (pausePanel != null) pausePanel.SetActive(true);
            if (pauseButton != null) pauseButton.SetActive(false);
            if (resumeButton != null) resumeButton.SetActive(true);
        }
        else
        {
            Time.timeScale = 1;
            if (pausePanel != null) pausePanel.SetActive(false);
            if (pauseButton != null) pauseButton.SetActive(true);
            if (resumeButton != null) resumeButton.SetActive(false);
        }
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1;
        if (pausePanel != null) pausePanel.SetActive(false);
        if (pauseButton != null) pauseButton.SetActive(true);
        if (resumeButton != null) resumeButton.SetActive(false);
    }

    private void OnDestroy()
    {
        // Limpiar suscripciones
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.OnInstructionStart -= DisablePauseButton;
            SoundManager.Instance.OnInstructionEnd -= EnablePauseButton;
        }

        // Desuscribirse de la actividad actual si existe
        if (currentActivity != null)
        {
            currentActivity.OnActivityComplete -= HandleActivityComplete;
        }
    }
}