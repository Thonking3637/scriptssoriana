// ═══════════════════════════════════════════════════════════════════════════════
// ActivityMetricsAdapter.cs
// Componente NO INVASIVO que se adjunta a cualquier ActivityBase
// Lee métricas por reflection y las normaliza para el Orquestador
// 
// ACTUALIZACIÓN: Ahora soporta ActivitySummaryConfig para mensajes dinámicos
// ═══════════════════════════════════════════════════════════════════════════════

using UnityEngine;
using System.Reflection;

/// <summary>
/// Wrapper que lee métricas de cualquier actividad y las traduce a formato unificado.
/// Se AGREGA al GameObject, NO reemplaza la actividad existente.
/// </summary>
public class ActivityMetricsAdapter : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private ActivityBase targetActivity;
    [SerializeField] private string activityId = "FYV_A1";

    [Header("Mensajes")]
    [Tooltip("Config global de mensajes (opcional). Si está asignado, ignora customMessage.")]
    [SerializeField] private ActivitySummaryConfig summaryConfig;

    [Tooltip("Mensaje fijo (solo se usa si summaryConfig es null)")]
    [SerializeField] public string customMessage = "¡Excelente trabajo!";

    [Header("Extracción de Métricas")]
    [SerializeField] private MetricsSourceConfig metricsConfig;

    [Header("Configuración de Scoring")]
    [SerializeField] private ScoringConfig scoringConfig;

    // Estado interno
    private ActivityMetrics _metrics = new ActivityMetrics();
    private bool _hasCompleted = false;

    // ═════════════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═════════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (targetActivity == null)
            targetActivity = GetComponent<ActivityBase>();

        if (targetActivity == null)
        {
            Debug.LogError($"[ActivityMetricsAdapter] No ActivityBase encontrado en {gameObject.name}");
            enabled = false;
            return;
        }
    }

    private void OnEnable()
    {
        // ⚠️ IMPORTANTE: NO suscribirse al evento OnActivityComplete automáticamente
        // porque crea un race condition con el GameManager
        // 
        // La actividad debe llamar NotifyActivityCompleted() manualmente ANTES
        // de llamar CompleteActivity() para prevenir que GameManager avance

        _hasCompleted = false;
    }

    private void OnDisable()
    {
        // Ya no hay evento al que desuscribirse
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // API PÚBLICA - LLAMADA MANUAL DESDE LA ACTIVIDAD
    // ═════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Método público para que la actividad notifique manualmente que ha terminado.
    /// 
    /// IMPORTANTE: Llamar este método ANTES de llamar CompleteActivity() para prevenir
    /// el race condition con el GameManager.
    /// </summary>
    public void NotifyActivityCompleted()
    {
        if (_hasCompleted) return;
        _hasCompleted = true;

        Debug.Log("[Adapter] ✅ Actividad completada (notificación manual)");

        // ✅ LLAMAR DIRECTAMENTE a los métodos sin pasar por OnActivityCompleted()
        Debug.Log("[Adapter] Extrayendo métricas...");
        ExtractMetrics();

        Debug.Log("[Adapter] Calculando score...");
        CalculateScore();

        Debug.Log("[Adapter] Guardando resultado...");
        SaveResult();

        Debug.Log("[Adapter] Mostrando panel...");
        ShowSummaryPanel();
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // CORE: EXTRACCIÓN Y EVALUACIÓN
    // ═════════════════════════════════════════════════════════════════════════════

    private void OnActivityCompleted()
    {
        if (_hasCompleted) return;
        _hasCompleted = true;

        Debug.Log("[Adapter] ✅ Actividad completada detectada");

        // 1. Extraer métricas
        ExtractMetrics();

        // 2. Calcular score
        CalculateScore();

        // 3. Guardar resultado
        SaveResult();

        // 4. Mostrar panel de resumen
        ShowSummaryPanel();
    }

    /// <summary>
    /// Extrae métricas de la actividad usando reflection o acceso directo
    /// </summary>
    private void ExtractMetrics()
    {
        // CASO 1: PhasedActivityBasePro (tiene métricas built-in)
        if (targetActivity is PhasedActivityBasePro pro)
        {
            _metrics.successes = pro.aciertosTotales;
            _metrics.errors = pro.erroresTotales;
            _metrics.dispatches = pro.despachosTotales;
            _metrics.timeSeconds = pro.tiempoEmpleado;
            _metrics.total = metricsConfig.expectedTotal > 0 ? metricsConfig.expectedTotal : pro.aciertosTotales + pro.erroresTotales;

            Debug.Log($"[Adapter] Métricas Pro: {_metrics.successes}/{_metrics.total}, errors={_metrics.errors}, time={_metrics.timeSeconds:F1}s");
            return;
        }

        // CASO 2: Usar configuración manual con reflection
        _metrics = ExtractUsingReflection(targetActivity, metricsConfig);

        Debug.Log($"[Adapter] Métricas Reflection: {_metrics.successes}/{_metrics.total}, errors={_metrics.errors}, time={_metrics.timeSeconds:F1}s");
    }

    /// <summary>
    /// Extrae métricas usando reflection basado en configuración
    /// </summary>
    private ActivityMetrics ExtractUsingReflection(ActivityBase activity, MetricsSourceConfig config)
    {
        var metrics = new ActivityMetrics();
        var type = activity.GetType();

        // Extraer successes
        if (!string.IsNullOrEmpty(config.successesFieldName))
        {
            var value = GetFieldValue(type, activity, config.successesFieldName);
            metrics.successes = ConvertToInt(value);
        }

        // Extraer errors
        if (!string.IsNullOrEmpty(config.errorsFieldName))
        {
            var value = GetFieldValue(type, activity, config.errorsFieldName);
            metrics.errors = ConvertToInt(value);
        }

        // Extraer dispatches (opcional)
        if (!string.IsNullOrEmpty(config.dispatchesFieldName))
        {
            var value = GetFieldValue(type, activity, config.dispatchesFieldName);
            metrics.dispatches = ConvertToInt(value);
        }

        // Tiempo: usar el ActivityBase.activityTimeText si existe
        if (activity.activityTimeText != null)
        {
            metrics.timeSeconds = ParseTimeFromText(activity.activityTimeText.text);
        }

        // Total esperado
        if (metrics.errors > 0)
        {
            metrics.total = metrics.successes + metrics.errors;
        }
        else if (config.expectedTotal > 0)
        {
            metrics.total = config.expectedTotal;
        }
        else
        {
            metrics.total = Mathf.Max(1, metrics.successes);
        }

        return metrics;
    }

    /// <summary>
    /// Obtiene valor de campo usando reflection (soporta campos privados y propiedades)
    /// </summary>
    private object GetFieldValue(System.Type type, object instance, string fieldName)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        // Intentar campo
        var field = type.GetField(fieldName, flags);
        if (field != null)
            return field.GetValue(instance);

        // Intentar propiedad
        var prop = type.GetProperty(fieldName, flags);
        if (prop != null)
            return prop.GetValue(instance);

        Debug.LogWarning($"[Adapter] Campo/propiedad '{fieldName}' no encontrado en {type.Name}");
        return null;
    }

    private int ConvertToInt(object value)
    {
        if (value == null) return 0;

        try
        {
            return System.Convert.ToInt32(value);
        }
        catch
        {
            return 0;
        }
    }

    private float ParseTimeFromText(string timeText)
    {
        if (string.IsNullOrEmpty(timeText)) return 0f;

        // Formato esperado: "1:30" o "90" o "1m 30s"
        timeText = timeText.Trim();

        // Intentar formato "M:SS"
        if (timeText.Contains(":"))
        {
            var parts = timeText.Split(':');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out int min) &&
                int.TryParse(parts[1], out int sec))
            {
                return min * 60f + sec;
            }
        }

        // Intentar formato numérico simple (segundos)
        if (float.TryParse(timeText.Replace("s", "").Trim(), out float seconds))
        {
            return seconds;
        }

        return 0f;
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // SCORING
    // ═════════════════════════════════════════════════════════════════════════════

    private void CalculateScore()
    {
        float rawScore = 0f;

        switch (scoringConfig.evaluationType)
        {
            case EvaluationType.AccuracyBased:
                // Score basado en % de aciertos
                rawScore = _metrics.GetAccuracyPercent();
                break;

            case EvaluationType.TimeBased:
                // Score basado en completar en tiempo ideal
                if (_metrics.timeSeconds > 0 && scoringConfig.idealTimeSeconds > 0)
                {
                    float timeRatio = Mathf.Clamp01(scoringConfig.idealTimeSeconds / _metrics.timeSeconds);
                    rawScore = timeRatio * 100f;
                }
                else
                {
                    rawScore = 100f; // Si no hay tiempo, score máximo
                }
                break;

            case EvaluationType.ComboMetric:
                // Combo: accuracy + speed + efficiency
                float accuracyScore = _metrics.GetAccuracyPercent() * scoringConfig.weightAccuracy;

                float speedScore = 0f;
                if (_metrics.timeSeconds > 0 && scoringConfig.idealTimeSeconds > 0)
                {
                    float timeRatio = Mathf.Clamp01(scoringConfig.idealTimeSeconds / _metrics.timeSeconds);
                    speedScore = timeRatio * 100f * scoringConfig.weightSpeed;
                }

                float efficiencyScore = 100f * scoringConfig.weightEfficiency;
                if (_metrics.errors > 0 && scoringConfig.maxAllowedErrors > 0)
                {
                    float errorPenalty = Mathf.Clamp01((float)_metrics.errors / scoringConfig.maxAllowedErrors);
                    efficiencyScore *= (1f - errorPenalty);
                }

                rawScore = accuracyScore + speedScore + efficiencyScore;
                break;
        }

        _metrics.score = Mathf.Clamp(Mathf.RoundToInt(rawScore), 0, 100);
        _metrics.stars = CalculateStars(_metrics.score);

        Debug.Log($"[Adapter] Score calculado: {_metrics.score}/100, Stars: {_metrics.stars}");
    }

    private int CalculateStars(int score)
    {
        if (score >= scoringConfig.star3Threshold) return 3;
        if (score >= scoringConfig.star2Threshold) return 2;
        if (score >= scoringConfig.star1Threshold) return 1;
        return 0;
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // GUARDADO
    // ═════════════════════════════════════════════════════════════════════════════

    private void SaveResult()
    {
        // Obtener mensaje dinámico
        string message = GetDynamicMessage();

        // Guardar en el servicio de scoring
        if (ActivityScoringService.Instance != null)
        {
            ActivityScoringService.Instance.SaveScore(activityId, _metrics, message);
        }

        // Marcar como completada en el sistema existente
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        int activityIndex = GetActivityIndex();

        if (activityIndex >= 0)
        {
            CompletionService.MarkActivity(scene, activityIndex);

            if (ProgressService.Instance != null)
            {
                ProgressService.Instance.CommitMedal(scene, activityIndex);
            }
        }
    }

    /// <summary>
    /// Obtiene el mensaje dinámico basado en el config o el mensaje fijo.
    /// </summary>
    private string GetDynamicMessage()
    {
        // Si hay config de mensajes, usarlo
        if (summaryConfig != null)
        {
            return summaryConfig.GetMessage(activityId, _metrics.stars, _metrics.errors);
        }

        // Fallback al mensaje fijo
        return customMessage;
    }

    private int GetActivityIndex()
    {
        if (GameManager.Instance == null) return -1;
        return GameManager.Instance.activities.IndexOf(targetActivity);
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // PANEL DE RESUMEN
    // ═════════════════════════════════════════════════════════════════════════════

    private void ShowSummaryPanel()
    {
        Debug.Log("[Adapter] ════════════════════════════════════");
        Debug.Log("[Adapter] ShowSummaryPanel - INICIADO");
        Debug.Log("[Adapter] ════════════════════════════════════");

        // Buscar o crear el panel de resumen unificado
        var panel = UnifiedSummaryPanel.Instance;

        Debug.Log($"[Adapter] UnifiedSummaryPanel.Instance: {(panel != null ? "✅ ENCONTRADO" : "❌ NULL")}");

        if (panel == null)
        {
            Debug.LogError("[Adapter] ❌ UnifiedSummaryPanel NO ENCONTRADO en la escena");
            Debug.LogError("[Adapter] ¿Está el prefab instanciado en el Canvas?");

            // ⚠️ FALLBACK CRÍTICO
            if (targetActivity != null)
            {
                Debug.LogWarning("[Adapter] Ejecutando fallback: CompleteActivity()");
                targetActivity.CompleteActivity();
            }
            return;
        }

        // ✅ Obtener mensaje dinámico
        string message = GetDynamicMessage();

        Debug.Log("[Adapter] ✅ Panel encontrado, preparando datos...");
        Debug.Log($"[Adapter] Score: {_metrics.score}/100, Stars: {_metrics.stars}");
        Debug.Log($"[Adapter] Mensaje: {message}");

        // ✅ Pasar la referencia de la actividad al panel
        Debug.Log("[Adapter] Llamando panel.Show()...");
        panel.Show(_metrics, activityId, message, targetActivity);
        Debug.Log("[Adapter] panel.Show() ejecutado");

        Debug.Log("[Adapter] ════════════════════════════════════");
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // API PÚBLICA (para debugging)
    // ═════════════════════════════════════════════════════════════════════════════

    public ActivityMetrics GetCurrentMetrics() => _metrics;

    [ContextMenu("Test: Extract Metrics Now")]
    private void TestExtractMetrics()
    {
        ExtractMetrics();
        Debug.Log($"[Test] Métricas extraídas: {JsonUtility.ToJson(_metrics, true)}");
    }

    [ContextMenu("Test: Calculate Score Now")]
    private void TestCalculateScore()
    {
        ExtractMetrics();
        CalculateScore();
        Debug.Log($"[Test] Score: {_metrics.score}/100, Stars: {_metrics.stars}");
    }

    [ContextMenu("Test: Get Dynamic Message")]
    private void TestGetDynamicMessage()
    {
        ExtractMetrics();
        CalculateScore();
        string msg = GetDynamicMessage();
        Debug.Log($"[Test] Mensaje dinámico: {msg}");
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// CONFIGURACIONES (se muestran en Inspector)
// ═══════════════════════════════════════════════════════════════════════════════

[System.Serializable]
public class MetricsSourceConfig
{
    [Header("Campos de la Actividad (usar Reflection)")]
    [Tooltip("Nombre del campo que contiene aciertos (ej: 'ejerciciosCorrectos', 'scannedCount')")]
    public string successesFieldName = "";

    [Tooltip("Nombre del campo que contiene errores (ej: 'errorCount', 'mistakes')")]
    public string errorsFieldName = "";

    [Tooltip("Nombre del campo que contiene despachos (opcional)")]
    public string dispatchesFieldName = "";

    [Header("Total Esperado")]
    [Tooltip("Total de objetivos esperados (ej: 3 ejercicios, 15 productos). Si es 0, se calcula como successes+errors")]
    public int expectedTotal = 0;
}

[System.Serializable]
public class ScoringConfig
{
    [Header("Tipo de Evaluación")]
    public EvaluationType evaluationType = EvaluationType.AccuracyBased;

    [Header("Pesos (solo para ComboMetric)")]
    [Range(0f, 1f)] public float weightAccuracy = 0.5f;
    [Range(0f, 1f)] public float weightSpeed = 0.3f;
    [Range(0f, 1f)] public float weightEfficiency = 0.2f;

    [Header("Umbrales de Estrellas")]
    [Range(0, 100)] public int star1Threshold = 50;
    [Range(0, 100)] public int star2Threshold = 75;
    [Range(0, 100)] public int star3Threshold = 90;

    [Header("Referencias (para cálculos)")]
    [Tooltip("Tiempo ideal en segundos (para TimeBased o ComboMetric)")]
    public float idealTimeSeconds = 60f;

    [Tooltip("Máximo de errores permitidos antes de penalización completa")]
    public int maxAllowedErrors = 3;
}

public enum EvaluationType
{
    AccuracyBased,   // Solo % de aciertos
    TimeBased,       // Solo velocidad
    ComboMetric      // Combinación de accuracy + speed + efficiency
}

// ═══════════════════════════════════════════════════════════════════════════════
// DATOS
// ═══════════════════════════════════════════════════════════════════════════════

[System.Serializable]
public class ActivityMetrics
{
    public int successes;
    public int errors;
    public int total;
    public float timeSeconds;
    public int dispatches;

    // Calculados
    public int score;
    public int stars;

    public float GetAccuracyPercent()
    {
        if (total <= 0) return 100f;
        return (successes / (float)total) * 100f;
    }
}