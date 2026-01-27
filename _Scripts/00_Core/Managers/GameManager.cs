using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using DG.Tweening;
using UnityEngine.UI;

public class GameManager: MonoBehaviour
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
        pauseButtonImage.sprite = disabledPauseSprite;
        pauseButtonImage.color = new Color(1, 1, 1, 0.5f);
        pauseButton.GetComponent<Button>().interactable = false;
    }

    private void EnablePauseButton()
    {
        pauseButtonImage.sprite = normalPauseSprite;
        pauseButtonImage.color = new Color(1, 1, 1, 1f);
        pauseButton.GetComponent<Button>().interactable = true;
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
        currentActivity.gameObject.SetActive(true);
        currentActivity.OnActivityComplete += HandleActivityComplete;

        cameraController.InitializeCameraPosition(currentActivity.startCameraPosition);

        OnActivityChange?.Invoke(currentActivityIndex);
        currentActivity.StartActivity();
    }

    private void ApplyActivityConfig(int index)
    {
        if (index < 0 || index >= activityConfigs.Count) return;

        var config = activityConfigs[index];

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

    /// Desactiva todas las actividades en la lista para que solo una se active al iniciar.
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
        currentActivity.OnActivityComplete -= HandleActivityComplete;

        string scene = SceneManager.GetActiveScene().name;

        // 1) Local (PlayerPrefs)
        CompletionService.MarkActivity(scene, currentActivityIndex);

        // 2) 🔥 REMOTO (Firestore)
        ProgressService.Instance.CommitMedal(scene, currentActivityIndex);

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

        // Ejecutar fade de salida
        fadePanel.DOFade(1, fadeDuration).OnComplete(() =>
        {
            Time.timeScale = 1;
            DOTween.KillAll();
            Destroy(gameObject);
            LoadingScreen.LoadScene("Menu");
        });
    }

    public void ReturnToMenuImmediate()
    {
        // 1. Detener sonidos/tweens primero
        DOTween.KillAll();
        Time.timeScale = 1; // Asegurar que el tiempo esté normalizado

        // 2. Limpiar el ActivityLauncher si existe (para evitar duplicados)
        if (ActivityLauncher.Instance != null)
        {
            Destroy(ActivityLauncher.Instance.gameObject);
        }

        // 3. Cargar el menú con fade out
        fadePanel.DOFade(1, fadeDuration).OnComplete(() =>
        {
            Destroy(gameObject); // Destruir GameManager
            LoadingScreen.LoadScene("Menu");
        });
    }

    public string GetCurrentActivityName()
    {
        if (currentActivityIndex < activities.Count)
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
        if (SoundManager.Instance.IsInstructionPlaying()) // 🔹 No se puede pausar si hay instrucciones activas
        {
            Debug.Log("No se puede pausar mientras hay instrucciones en reproducción.");
            return;
        }

        var grabador = FindObjectOfType<VoiceRecorder>();
        if (grabador != null && grabador.IsRecording)
        {
            Debug.Log("No se puede pausar mientras se graba audio.");
            return;
        }

        isPaused = !isPaused;
        pausePanel.SetActive(isPaused);
        Time.timeScale = isPaused ? 0 : 1;

        pauseButton.SetActive(!isPaused); // 🔹 Oculta el botón de pausa cuando se pausa el juego
        resumeButton.SetActive(isPaused); // 🔹 Muestra el botón de reanudar cuando se pausa
    }

    /// <summary>
    /// Reinicia el nivel con animación de fade in/out.
    /// </summary>
    public void RestartLevel()
    {
        Time.timeScale = 1;
        StartCoroutine(LoadSceneWithFade(SceneManager.GetActiveScene().name));
    }

    /// <summary>
    /// Carga una escena con efecto de fade in/out.
    /// </summary>
    private System.Collections.IEnumerator LoadSceneWithFade(string sceneName)
    {
        fadePanel.DOFade(1, fadeDuration).SetUpdate(true);
        yield return new WaitForSeconds(fadeDuration);
        LoadingScreen.LoadScene(sceneName);
    }

    public void StartActivityByIndex(int index)
    {
        if (index < 0 || index >= activities.Count)
        {
            Debug.LogError($"Índice {index} fuera de rango");
            return;
        }

        // Siempre usar el índice proporcionado
        currentActivityIndex = index;

        // Reiniciar la actividad
        DeactivateAllActivities();
        ApplyActivityConfig(index);

        currentActivity = activities[index];
        currentActivity.gameObject.SetActive(true);
        currentActivity.OnActivityComplete += HandleActivityComplete;

        cameraController?.InitializeCameraPosition(currentActivity.startCameraPosition);
        OnActivityChange?.Invoke(index);
        currentActivity.StartActivity();
    }

    private void OnDestroy()
    {
        // Limpieza preventiva de tweens
        if (fadePanel != null)
        {
            fadePanel.DOKill();
        }

        // Cancelar suscripciones a eventos
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.OnInstructionStart -= DisablePauseButton;
            SoundManager.Instance.OnInstructionEnd -= EnablePauseButton;
        }
    }
}
