using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using DG.Tweening;
using System;

public class LoadingScreen : MonoBehaviour
{
    public static LoadingScreen Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Configuración")]
    [SerializeField] private float minLoadingTime = 1.5f;
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float fadeOutDuration = 0.4f;
    [SerializeField] private bool cleanupMemory = true;

    [Header("Textos localizables")]
    [SerializeField] private string loadingFormat = "Cargando... {0}%";
    [SerializeField] private string readyText = "¡Listo!";
    [SerializeField] private string errorText = "Error al cargar";

    // Estado
    private static string _nextScene;
    private static Action _onLoadComplete;
    private bool _isLoading;
    private Coroutine _loadCoroutine;

    // Cache
    private const string LOADING_SCENE_NAME = "LoadingScreen";
    private static readonly WaitForEndOfFrame WaitFrame = new WaitForEndOfFrame();

    #region API Pública

    /// <summary>
    /// Carga una escena mostrando la pantalla de carga.
    /// </summary>
    /// <param name="sceneName">Nombre exacto de la escena en Build Settings</param>
    /// <param name="onComplete">Callback opcional al completar (antes del fade out)</param>
    public static void LoadScene(string sceneName, Action onComplete = null)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[LoadingScreen] Nombre de escena vacío o nulo.");
            return;
        }

        // Validar que la escena existe en Build Settings
        if (!IsSceneInBuildSettings(sceneName))
        {
            Debug.LogError($"[LoadingScreen] La escena '{sceneName}' no está en Build Settings.");
            return;
        }

        _nextScene = sceneName;
        _onLoadComplete = onComplete;

        // Si ya estamos en LoadingScreen, iniciar carga directamente
        if (Instance != null && Instance.gameObject.activeInHierarchy)
        {
            Instance.StartLoadingProcess();
            return;
        }

        SceneManager.LoadScene(LOADING_SCENE_NAME);
    }

    /// <summary>
    /// Recarga la escena actual.
    /// </summary>
    public static void ReloadCurrentScene(Action onComplete = null)
    {
        LoadScene(SceneManager.GetActiveScene().name, onComplete);
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        // Singleton simple (no persiste entre escenas, es intencional)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        ValidateReferences();
        InitializeUI();
    }

    private void Start()
    {
        StartLoadingProcess();
    }

    private void OnDestroy()
    {
        // Limpiar tweens pendientes
        if (canvasGroup != null)
            DOTween.Kill(canvasGroup);

        if (Instance == this)
            Instance = null;
    }

    #endregion

    #region Proceso de Carga

    private void StartLoadingProcess()
    {
        if (_isLoading) return;

        if (string.IsNullOrWhiteSpace(_nextScene))
        {
            Debug.LogWarning("[LoadingScreen] No hay escena destino definida.");
            return;
        }

        if (_loadCoroutine != null)
            StopCoroutine(_loadCoroutine);

        _loadCoroutine = StartCoroutine(LoadSceneRoutine());
    }

    private IEnumerator LoadSceneRoutine()
    {
        _isLoading = true;

        // 1) Fade In de la UI
        yield return FadeIn();

        // 2) Limpieza de memoria (importante en móviles)
        if (cleanupMemory)
            yield return CleanupMemoryRoutine();

        // 3) Iniciar carga asíncrona
        AsyncOperation asyncOp = null;
        bool loadError = false;

        try
        {
            asyncOp = SceneManager.LoadSceneAsync(_nextScene);
            asyncOp.allowSceneActivation = false;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LoadingScreen] Error iniciando carga: {ex.Message}");
            loadError = true;
        }

        if (loadError || asyncOp == null)
        {
            ShowError();
            yield return new WaitForSeconds(2f);
            _isLoading = false;
            yield break;
        }

        // 4) Loop de progreso con tiempo mínimo
        float elapsedTime = 0f;
        float displayedProgress = 0f;

        while (!asyncOp.isDone)
        {
            elapsedTime += Time.unscaledDeltaTime;

            // Progreso real de Unity (0 a 0.9 = cargando, 0.9 = listo para activar)
            float realProgress = Mathf.Clamp01(asyncOp.progress / 0.9f);

            // Progreso basado en tiempo mínimo
            float timeProgress = Mathf.Clamp01(elapsedTime / minLoadingTime);

            // Usamos el menor de los dos para que la barra no llegue al 100%
            // hasta que AMBOS estén completos
            float targetProgress = Mathf.Min(realProgress, timeProgress);

            // Suavizar el progreso visual (evita saltos)
            displayedProgress = Mathf.MoveTowards(displayedProgress, targetProgress, Time.unscaledDeltaTime * 2f);

            UpdateProgressUI(displayedProgress);

            // Verificar si podemos activar la escena
            bool realLoadComplete = asyncOp.progress >= 0.9f;
            bool minTimeComplete = elapsedTime >= minLoadingTime;

            if (realLoadComplete && minTimeComplete)
            {
                // Asegurar que la UI muestre 100%
                UpdateProgressUI(1f);
                ShowReady();

                // Pequeña pausa para que el usuario vea "¡Listo!"
                yield return new WaitForSecondsRealtime(0.3f);

                // Callback antes de activar
                _onLoadComplete?.Invoke();
                _onLoadComplete = null;

                // 5) Fade Out y activar escena
                yield return FadeOut();

                asyncOp.allowSceneActivation = true;
            }

            yield return WaitFrame;
        }

        _isLoading = false;
    }

    private IEnumerator CleanupMemoryRoutine()
    {
        // Forzar limpieza de recursos no usados
        var unloadOp = Resources.UnloadUnusedAssets();

        while (!unloadOp.isDone)
            yield return null;

        // GC solo en plataformas donde tiene sentido
#if !UNITY_WEBGL
        GC.Collect();
#endif

        // Un frame extra para que Unity procese
        yield return null;
    }

    #endregion

    #region UI Updates

    private void InitializeUI()
    {
        if (progressBar != null)
            progressBar.value = 0f;

        if (progressText != null)
            progressText.text = string.Format(loadingFormat, 0);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = true;
        }
    }

    private void UpdateProgressUI(float progress)
    {
        int percentage = Mathf.RoundToInt(progress * 100f);

        if (progressBar != null)
            progressBar.value = progress;

        if (progressText != null)
            progressText.text = string.Format(loadingFormat, percentage);
    }

    private void ShowReady()
    {
        if (progressText != null)
            progressText.text = readyText;
    }

    private void ShowError()
    {
        if (progressText != null)
            progressText.text = errorText;

        if (progressBar != null)
            progressBar.value = 0f;
    }

    private IEnumerator FadeIn()
    {
        if (canvasGroup == null)
        {
            yield break;
        }

        canvasGroup.alpha = 0f;
        yield return canvasGroup
            .DOFade(1f, fadeInDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true) // Ignora Time.timeScale
            .WaitForCompletion();
    }

    private IEnumerator FadeOut()
    {
        if (canvasGroup == null)
        {
            yield break;
        }

        yield return canvasGroup
            .DOFade(0f, fadeOutDuration)
            .SetEase(Ease.InQuad)
            .SetUpdate(true)
            .WaitForCompletion();
    }

    #endregion

    #region Validaciones

    private void ValidateReferences()
    {
        if (progressBar == null)
            Debug.LogWarning("[LoadingScreen] ProgressBar no asignado.");

        if (progressText == null)
            Debug.LogWarning("[LoadingScreen] ProgressText no asignado.");

        if (canvasGroup == null)
        {
            // Intentar obtener o crear CanvasGroup
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private static bool IsSceneInBuildSettings(string sceneName)
    {
        int sceneCount = SceneManager.sceneCountInBuildSettings;

        for (int i = 0; i < sceneCount; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(scenePath);

            if (string.Equals(name, sceneName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    #endregion

    #region Editor Helpers

#if UNITY_EDITOR
    [ContextMenu("Test Load Menu")]
    private void TestLoadMenu() => LoadScene("Menu");
#endif

    #endregion
}