// ═══════════════════════════════════════════════════════════════════════════════
// UnifiedSummaryPanel.cs
// Panel de resumen unificado con sistema de 3 estrellas
// Se muestra al final de TODAS las actividades
// ═══════════════════════════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class UnifiedSummaryPanel : MonoBehaviour
{
    public static UnifiedSummaryPanel Instance { get; private set; }

    [Header("Referencias de UI")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Estrellas")]
    [SerializeField] private Image[] starImages = new Image[3];
    [SerializeField] private Sprite starFilledSprite;
    [SerializeField] private Sprite starEmptySprite;

    [Header("Textos")]
    [SerializeField] private TextMeshProUGUI txtQuality;      // "EXCELENTE", "MUY BIEN", etc.
    [SerializeField] private TextMeshProUGUI txtScore;        // "95/100"
    [SerializeField] private TextMeshProUGUI txtAccuracy;     // "Precisión: 100%"
    [SerializeField] private TextMeshProUGUI txtTime;         // "Tiempo: 45s"
    [SerializeField] private TextMeshProUGUI txtErrors;       // "Errores: 0"
    [SerializeField] private TextMeshProUGUI txtCustomMessage; // Mensaje personalizado

    [Header("Badge de Récord")]
    [SerializeField] private GameObject newRecordBadge;
    [SerializeField] private TextMeshProUGUI txtRecordMessage;

    [Header("Botones")]
    [SerializeField] private Button btnRetry;
    [SerializeField] private Button btnContinue;
    [SerializeField] private Button btnMenu;

    [Header("Animación")]
    [SerializeField] private float fadeInDuration = 0.5f;
    [SerializeField] private float starAnimDelay = 0.2f;
    [SerializeField] private float starAnimDuration = 0.3f;

    [Header("Audio")]
    [SerializeField] private string sfxShow = "win";
    [SerializeField] private string sfxStar = "star";

    // Estado
    private ActivityScoreData _currentData;
    private string _currentActivityId;
    private ActivityBase _targetActivity; // ← NUEVO: Referencia a la actividad para llamar CompleteActivity()

    // ═════════════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═════════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Setup inicial
        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        // Setup botones
        if (btnRetry != null)
            btnRetry.onClick.AddListener(OnRetryClicked);

        if (btnContinue != null)
            btnContinue.onClick.AddListener(OnContinueClicked);

        if (btnMenu != null)
            btnMenu.onClick.AddListener(OnMenuClicked);

        // Ocultar badge de récord por defecto
        if (newRecordBadge != null)
            newRecordBadge.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // API PÚBLICA
    // ═════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Muestra el panel con los datos de la actividad completada
    /// </summary>
    public void Show(ActivityMetrics metrics, string activityId, string customMessage, ActivityBase activity)
    {
        _currentActivityId = activityId;
        _targetActivity = activity; // ← Guardar referencia para llamar CompleteActivity() después

        // Crear datos
        _currentData = new ActivityScoreData
        {
            activityId = activityId,
            score = metrics.score,
            stars = metrics.stars,
            accuracy = metrics.GetAccuracyPercent(),
            timeSeconds = metrics.timeSeconds,
            successes = metrics.successes,
            errors = metrics.errors,
            dispatches = metrics.dispatches,
            customMessage = customMessage,
            timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        // Verificar si es récord personal
        bool isNewRecord = CheckIfNewRecord(_currentData);

        // Mostrar panel
        ShowPanel(_currentData, isNewRecord);
    }

    /// <summary>
    /// Oculta el panel
    /// </summary>
    public void Hide()
    {
        if (canvasGroup != null)
        {
            canvasGroup.DOFade(0f, 0.3f).OnComplete(() =>
            {
                if (panelRoot != null)
                    panelRoot.SetActive(false);
            });
        }
        else if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // CORE: MOSTRAR PANEL
    // ═════════════════════════════════════════════════════════════════════════════

    private void ShowPanel(ActivityScoreData data, bool isNewRecord)
    {
        // Activar panel
        if (panelRoot != null)
            panelRoot.SetActive(true);

        // Llenar textos
        FillTexts(data);

        // Configurar badge de récord
        if (newRecordBadge != null)
        {
            newRecordBadge.SetActive(isNewRecord);

            if (isNewRecord && txtRecordMessage != null)
            {
                txtRecordMessage.text = "¡NUEVO RÉCORD PERSONAL!";
            }
        }

        // Animación de entrada
        AnimateIn(data.stars);

        // Audio
        PlaySound(sfxShow);
    }

    private void FillTexts(ActivityScoreData data)
    {
        // Calidad
        if (txtQuality != null)
        {
            txtQuality.text = data.GetQualityText();
            txtQuality.color = data.GetQualityColor();
        }

        // Score - SIEMPRE MOSTRAR
        if (txtScore != null)
        {
            txtScore.text = $"PUNTUACIÓN: {data.score}/100";
        }

        // Precisión - Solo si hay datos
        if (txtAccuracy != null)
        {
            bool hasAccuracyData = data.successes > 0 || data.errors > 0;
            txtAccuracy.gameObject.SetActive(hasAccuracyData);
            if (hasAccuracyData)
            {
                txtAccuracy.text = $"Precisión: {data.accuracy:F1}%";
            }
        }

        // Tiempo - Solo si > 0
        if (txtTime != null)
        {
            bool hasTimeData = data.timeSeconds > 0;
            txtTime.gameObject.SetActive(hasTimeData);
            if (hasTimeData)
            {
                int minutes = Mathf.FloorToInt(data.timeSeconds / 60f);
                int seconds = Mathf.FloorToInt(data.timeSeconds % 60f);

                if (minutes > 0)
                    txtTime.text = $"Tiempo: {minutes}:{seconds:D2}";
                else
                    txtTime.text = $"Tiempo: {seconds}s";
            }
        }

        // Errores - Solo si hay sistema de errores
        if (txtErrors != null)
        {
            bool hasErrorTracking = data.successes > 0 || data.errors > 0;
            txtErrors.gameObject.SetActive(hasErrorTracking);
            if (hasErrorTracking)
            {
                txtErrors.text = $"Errores: {data.errors}";
            }
        }

        // Mensaje - SIEMPRE MOSTRAR
        if (txtCustomMessage != null)
        {
            txtCustomMessage.text = data.customMessage;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // ANIMACIONES
    // ═════════════════════════════════════════════════════════════════════════════

    private void AnimateIn(int stars)
    {
        // Fade in del panel
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.DOFade(1f, fadeInDuration);
        }

        // Animar estrellas secuencialmente
        AnimateStars(stars);
    }

    private void AnimateStars(int earnedStars)
    {
        // Resetear estrellas a vacías
        for (int i = 0; i < starImages.Length; i++)
        {
            if (starImages[i] != null)
            {
                starImages[i].sprite = starEmptySprite;
                starImages[i].transform.localScale = Vector3.zero;
            }
        }

        // Animar estrellas ganadas
        for (int i = 0; i < earnedStars && i < starImages.Length; i++)
        {
            int index = i;
            float delay = fadeInDuration + (i * starAnimDelay);

            DOVirtual.DelayedCall(delay, () =>
            {
                if (starImages[index] != null)
                {
                    starImages[index].sprite = starFilledSprite;

                    starImages[index].transform
                        .DOScale(1f, starAnimDuration)
                        .SetEase(Ease.OutBack);

                    PlaySound(sfxStar);
                }
            });
        }

        // Animar estrellas no ganadas (vacías)
        for (int i = earnedStars; i < starImages.Length; i++)
        {
            int index = i;
            float delay = fadeInDuration + (i * starAnimDelay);

            DOVirtual.DelayedCall(delay, () =>
            {
                if (starImages[index] != null)
                {
                    starImages[index].transform
                        .DOScale(1f, starAnimDuration)
                        .SetEase(Ease.OutQuad);
                }
            });
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═════════════════════════════════════════════════════════════════════════════

    private bool CheckIfNewRecord(ActivityScoreData newData)
    {
        if (ActivityScoringService.Instance == null)
            return false;

        var bestScore = ActivityScoringService.Instance.GetBestScore(newData.activityId);

        if (bestScore == null)
            return true; // Primera vez

        // Comparar: primero estrellas, luego score
        return (newData.stars > bestScore.stars) ||
               (newData.stars == bestScore.stars && newData.score > bestScore.score);
    }

    private void PlaySound(string soundKey)
    {
        if (string.IsNullOrEmpty(soundKey))
            return;

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySound(soundKey);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // BOTONES
    // ═════════════════════════════════════════════════════════════════════════════

    private void OnRetryClicked()
    {
        Hide();

        // Reiniciar la actividad actual (recargar la escena)
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
        );
    }

    private void OnContinueClicked()
    {
        Hide();

        // ✅ CRÍTICO: Llamar a CompleteActivity() AQUÍ
        // Esto permite que el usuario vea el panel ANTES de que avance la actividad
        if (_targetActivity != null)
        {
            _targetActivity.CompleteActivity();
        }
        else
        {
            Debug.LogWarning("[UnifiedSummaryPanel] No hay referencia a la actividad. Volviendo al menú.");

            // Fallback: volver al menú
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ReturnToMenu();
            }
            else
            {
                LoadingScreen.LoadScene("Menu");
            }
        }
    }

    private void OnMenuClicked()
    {
        Hide();

        // Volver al menú usando el GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReturnToMenu();
        }
        else
        {
            // Fallback: cargar escena de menú directamente
            LoadingScreen.LoadScene("Menu");
        }
    }
}