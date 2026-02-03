// ═══════════════════════════════════════════════════════════════════════════════
// ActivityMetricsAdapter.cs
// Componente NO INVASIVO que se adjunta a cualquier ActivityBase
// Lee métricas por reflection y las normaliza para el Orquestador
// 
// TIPOS DE EVALUACIÓN:
// - AccuracyBased: Solo errores importan (ej: CashPayment)
// - TimeBased: Solo velocidad importa (ej: CardPayment)
// - ComboMetric: Combinación de accuracy + speed + efficiency
// - GuidedActivity: Sin métricas, siempre 100% si completa (ej: CashRegisterActivation)
// - CustomMetrics: La actividad pasa sus métricas directamente (ej: ScanActivity)
// ═══════════════════════════════════════════════════════════════════════════════

using UnityEngine;
using System.Reflection;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ActivityMetricsAdapter : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN BÁSICA
    // ═══════════════════════════════════════════════════════════════════════════

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

    // ═══════════════════════════════════════════════════════════════════════════
    // CUSTOM METRICS (para actividades con métricas propias como ScanActivity)
    // ═══════════════════════════════════════════════════════════════════════════

    [Header("═══ Custom Metrics (Solo para CustomMetrics) ═══")]
    [Tooltip("Texto personalizado para la línea 1 del resumen (ej: 'PRODUCTOS')")]
    [SerializeField] private string customLabel1 = "PRODUCTOS";

    [Tooltip("Texto personalizado para la línea 2 del resumen (ej: 'TIEMPO')")]
    [SerializeField] private string customLabel2 = "TIEMPO";

    // ═══════════════════════════════════════════════════════════════════════════
    // MODO CALIBRACIÓN
    // ═══════════════════════════════════════════════════════════════════════════

    [Header("═══ MODO CALIBRACIÓN (Solo Editor) ═══")]
    [Tooltip("Activa esto, juega la actividad perfectamente, y al terminar tu tiempo se guardará como idealTimeSeconds")]
    [SerializeField] private bool calibrationMode = false;

    [Tooltip("Multiplicador de margen. Ej: 1.2 = tu tiempo + 20% extra para el jugador")]
    [Range(1.0f, 2.0f)]
    [SerializeField] private float calibrationMargin = 1.2f;

    [Tooltip("Último tiempo calibrado (solo lectura)")]
    [SerializeField] private float lastCalibratedTime = 0f;

    [Tooltip("Fecha de última calibración")]
    [SerializeField] private string lastCalibrationDate = "";

    // ═══════════════════════════════════════════════════════════════════════════
    // ESTADO INTERNO
    // ═══════════════════════════════════════════════════════════════════════════

    private ActivityMetrics _metrics = new ActivityMetrics();
    private bool _hasCompleted = false;

    // Para CustomMetrics - valores seteados externamente
    private bool _useCustomMetrics = false;
    private int _customValue1;
    private int _customTotal1;
    private float _customTimeSeconds;
    private string _customDisplayLine1;
    private string _customDisplayLine2;

    // ═══════════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════════

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
        _hasCompleted = false;
        _useCustomMetrics = false;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // API PÚBLICA - NOTIFICACIÓN ESTÁNDAR
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Notificación estándar: extrae métricas automáticamente
    /// </summary>
    public void NotifyActivityCompleted()
    {
        if (_hasCompleted) return;
        _hasCompleted = true;

        Debug.Log("[Adapter] ✅ Actividad completada (notificación manual)");

        // Si es GuidedActivity, usar flujo simplificado
        if (scoringConfig.evaluationType == EvaluationType.GuidedActivity)
        {
            HandleGuidedActivityCompletion();
            return;
        }

        // Si hay custom metrics pendientes, usarlas
        if (_useCustomMetrics)
        {
            HandleCustomMetricsCompletion();
            return;
        }

        // Flujo estándar
        Debug.Log("[Adapter] Extrayendo métricas...");
        ExtractMetrics();

        if (calibrationMode)
            HandleCalibration();

        Debug.Log("[Adapter] Calculando score...");
        CalculateScore();

        Debug.Log("[Adapter] Guardando resultado...");
        SaveResult();

        Debug.Log("[Adapter] Mostrando panel...");
        ShowSummaryPanel();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // API PÚBLICA - CUSTOM METRICS (Para ScanActivity, etc.)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Permite a la actividad pasar sus propias métricas.
    /// Úsalo para actividades con sistemas de scoring personalizados.
    /// 
    /// Ejemplo ScanActivity:
    ///   adapter.SetCustomMetrics(
    ///     value1: scannedCount,      // 18
    ///     total1: minProductsToScan, // 18
    ///     timeSeconds: timeElapsed,  // 45.5f
    ///     displayLine1: "PRODUCTOS: 18/18",
    ///     displayLine2: "TIEMPO: 45s"
    ///   );
    ///   adapter.NotifyActivityCompleted();
    /// </summary>
    public void SetCustomMetrics(int value1, int total1, float timeSeconds,
                                  string displayLine1 = null, string displayLine2 = null)
    {
        _useCustomMetrics = true;
        _customValue1 = value1;
        _customTotal1 = total1;
        _customTimeSeconds = timeSeconds;
        _customDisplayLine1 = displayLine1;
        _customDisplayLine2 = displayLine2;

        Debug.Log($"[Adapter] Custom metrics set: {value1}/{total1}, time={timeSeconds:F1}s");
    }

    /// <summary>
    /// Versión simplificada para actividades que solo tienen completado/tiempo
    /// </summary>
    public void SetCustomMetrics(float timeSeconds, bool completed = true)
    {
        SetCustomMetrics(
            value1: completed ? 1 : 0,
            total1: 1,
            timeSeconds: timeSeconds
        );
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // GUIDED ACTIVITY (Sin métricas, siempre perfecto)
    // ═══════════════════════════════════════════════════════════════════════════

    private void HandleGuidedActivityCompletion()
    {
        Debug.Log("[Adapter] 🎯 GuidedActivity - Asignando score perfecto");

        // Score perfecto
        _metrics.score = 100;
        _metrics.stars = 3;
        _metrics.successes = 1;
        _metrics.errors = 0;
        _metrics.total = 1;
        _metrics.timeSeconds = 0; // No mostrar tiempo

        // Guardar y mostrar
        SaveResult();
        ShowSummaryPanel();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CUSTOM METRICS (Actividades con sistema propio)
    // ═══════════════════════════════════════════════════════════════════════════

    private void HandleCustomMetricsCompletion()
    {
        Debug.Log("[Adapter] 🎯 CustomMetrics - Usando métricas proporcionadas");

        // Asignar métricas custom
        _metrics.successes = _customValue1;
        _metrics.total = _customTotal1;
        _metrics.errors = Mathf.Max(0, _customTotal1 - _customValue1);
        _metrics.timeSeconds = _customTimeSeconds;

        // Calcular score basado en completion ratio
        float completionRatio = _customTotal1 > 0 ? (float)_customValue1 / _customTotal1 : 1f;

        // Si hay idealTime configurado, también considerar el tiempo
        if (scoringConfig.idealTimeSeconds > 0 && _customTimeSeconds > 0)
        {
            float timeRatio = Mathf.Clamp01(scoringConfig.idealTimeSeconds / _customTimeSeconds);
            // 70% completion + 30% tiempo
            _metrics.score = Mathf.RoundToInt((completionRatio * 70f) + (timeRatio * 30f));
        }
        else
        {
            // Solo completion
            _metrics.score = Mathf.RoundToInt(completionRatio * 100f);
        }

        _metrics.score = Mathf.Clamp(_metrics.score, 0, 100);
        _metrics.stars = CalculateStars(_metrics.score);

        // Guardar mensaje custom si se proporcionó
        if (!string.IsNullOrEmpty(_customDisplayLine1) || !string.IsNullOrEmpty(_customDisplayLine2))
        {
            // Construir mensaje custom
            string customMsg = "";
            if (!string.IsNullOrEmpty(_customDisplayLine1))
                customMsg += _customDisplayLine1;
            if (!string.IsNullOrEmpty(_customDisplayLine2))
                customMsg += (string.IsNullOrEmpty(customMsg) ? "" : "\n") + _customDisplayLine2;

            customMessage = customMsg;
        }

        Debug.Log($"[Adapter] CustomMetrics Score: {_metrics.score}/100, Stars: {_metrics.stars}");

        // Guardar y mostrar
        SaveResult();
        ShowSummaryPanel();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CALIBRACIÓN
    // ═══════════════════════════════════════════════════════════════════════════

    private void HandleCalibration()
    {
#if UNITY_EDITOR
        if (_metrics.timeSeconds <= 0)
        {
            Debug.LogWarning("[Adapter] ⚠️ CALIBRACIÓN: Tiempo es 0, no se puede calibrar");
            return;
        }

        lastCalibratedTime = _metrics.timeSeconds;
        lastCalibrationDate = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");

        float newIdealTime = _metrics.timeSeconds * calibrationMargin;

        Debug.Log($"[Adapter] ════════════════════════════════════════════════");
        Debug.Log($"[Adapter] 🎯 CALIBRACIÓN COMPLETADA");
        Debug.Log($"[Adapter] Tu tiempo: {_metrics.timeSeconds:F2}s");
        Debug.Log($"[Adapter] Margen: x{calibrationMargin}");
        Debug.Log($"[Adapter] Nuevo idealTimeSeconds: {newIdealTime:F2}s");
        Debug.Log($"[Adapter] ════════════════════════════════════════════════");

        float oldIdealTime = scoringConfig.idealTimeSeconds;
        scoringConfig.idealTimeSeconds = newIdealTime;

        EditorUtility.SetDirty(this);

        bool keepCalibrationMode = EditorUtility.DisplayDialog(
            "🎯 Calibración Completada",
            $"Tu tiempo: {_metrics.timeSeconds:F2}s\n" +
            $"Margen aplicado: x{calibrationMargin}\n\n" +
            $"idealTimeSeconds actualizado:\n" +
            $"  Antes: {oldIdealTime:F2}s\n" +
            $"  Ahora: {newIdealTime:F2}s\n\n" +
            $"¿Mantener modo calibración activo?",
            "Sí, seguir calibrando",
            "No, desactivar"
        );

        if (!keepCalibrationMode)
        {
            calibrationMode = false;
            EditorUtility.SetDirty(this);
        }
#else
        Debug.LogWarning("[Adapter] ⚠️ Modo calibración solo funciona en el Editor");
#endif
    }

    [ContextMenu("Reset Calibración")]
    private void ResetCalibration()
    {
#if UNITY_EDITOR
        lastCalibratedTime = 0f;
        lastCalibrationDate = "";
        scoringConfig.idealTimeSeconds = 60f;
        EditorUtility.SetDirty(this);
        Debug.Log("[Adapter] 🔄 Calibración reseteada");
#endif
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // EXTRACCIÓN DE MÉTRICAS
    // ═══════════════════════════════════════════════════════════════════════════

    private void ExtractMetrics()
    {
        // CASO 1: PhasedActivityBasePro
        if (targetActivity is PhasedActivityBasePro pro)
        {
            _metrics.successes = pro.aciertosTotales;
            _metrics.errors = pro.erroresTotales;
            _metrics.dispatches = pro.despachosTotales;
            _metrics.timeSeconds = pro.tiempoEmpleado;
            _metrics.total = metricsConfig.expectedTotal > 0
                ? metricsConfig.expectedTotal
                : pro.aciertosTotales + pro.erroresTotales;

            Debug.Log($"[Adapter] Métricas Pro: {_metrics.successes}/{_metrics.total}, errors={_metrics.errors}, time={_metrics.timeSeconds:F1}s");
            return;
        }

        // CASO 2: Reflection
        _metrics = ExtractUsingReflection(targetActivity, metricsConfig);
        Debug.Log($"[Adapter] Métricas Reflection: {_metrics.successes}/{_metrics.total}, errors={_metrics.errors}, time={_metrics.timeSeconds:F1}s");
    }

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

        // Extraer dispatches
        if (!string.IsNullOrEmpty(config.dispatchesFieldName))
        {
            var value = GetFieldValue(type, activity, config.dispatchesFieldName);
            metrics.dispatches = ConvertToInt(value);
        }

        // Tiempo
        metrics.timeSeconds = GetElapsedTimeFromActivity(activity);

        // Total
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

    private float GetElapsedTimeFromActivity(ActivityBase activity)
    {
        var baseType = typeof(ActivityBase);
        var elapsedField = baseType.GetField("elapsedTime", BindingFlags.NonPublic | BindingFlags.Instance);

        if (elapsedField != null)
        {
            var value = elapsedField.GetValue(activity);
            if (value != null)
            {
                float elapsed = (float)value;
                Debug.Log($"[Adapter] ✅ elapsedTime: {elapsed:F2}s");
                return elapsed;
            }
        }

        // Fallback: activityTimeText
        if (activity.activityTimeText != null && !string.IsNullOrEmpty(activity.activityTimeText.text))
        {
            float parsed = ParseTimeFromText(activity.activityTimeText.text);
            Debug.Log($"[Adapter] ⚠️ Fallback activityTimeText: {parsed:F2}s");
            return parsed;
        }

        // Fallback: calcular desde startTime
        var startTimeField = baseType.GetField("activityStartTime", BindingFlags.NonPublic | BindingFlags.Instance);
        if (startTimeField != null)
        {
            var startValue = startTimeField.GetValue(activity);
            if (startValue != null)
            {
                float calculated = Time.time - (float)startValue;
                Debug.Log($"[Adapter] ⚠️ Fallback calculado: {calculated:F2}s");
                return calculated;
            }
        }

        Debug.LogWarning("[Adapter] ❌ No se pudo obtener tiempo");
        return 0f;
    }

    private object GetFieldValue(System.Type type, object instance, string fieldName)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        var currentType = type;
        while (currentType != null)
        {
            var field = currentType.GetField(fieldName, flags);
            if (field != null)
                return field.GetValue(instance);

            var prop = currentType.GetProperty(fieldName, flags);
            if (prop != null)
                return prop.GetValue(instance);

            currentType = currentType.BaseType;
        }

        Debug.LogWarning($"[Adapter] Campo '{fieldName}' no encontrado en {type.Name}");
        return null;
    }

    private int ConvertToInt(object value)
    {
        if (value == null) return 0;
        try { return System.Convert.ToInt32(value); }
        catch { return 0; }
    }

    private float ParseTimeFromText(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0f;

        string cleaned = text.ToLower()
            .Replace("tiempo total:", "")
            .Replace("tiempo:", "")
            .Replace("s", "")
            .Replace(" ", "")
            .Trim();

        if (float.TryParse(cleaned, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float result))
        {
            return result;
        }

        return 0f;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CÁLCULO DE SCORE
    // ═══════════════════════════════════════════════════════════════════════════

    private void CalculateScore()
    {
        float rawScore = 0f;

        switch (scoringConfig.evaluationType)
        {
            case EvaluationType.AccuracyBased:
                if (_metrics.total > 0)
                    rawScore = ((float)_metrics.successes / _metrics.total) * 100f;
                else
                    rawScore = 100f;
                break;

            case EvaluationType.TimeBased:
                if (_metrics.timeSeconds > 0 && scoringConfig.idealTimeSeconds > 0)
                {
                    float timeRatio = scoringConfig.idealTimeSeconds / _metrics.timeSeconds;
                    rawScore = Mathf.Clamp(timeRatio * 100f, 0f, 100f);
                }
                else
                {
                    rawScore = 100f;
                }
                break;

            case EvaluationType.ComboMetric:
                float accuracyScore = 0f;
                if (_metrics.total > 0)
                    accuracyScore = ((float)_metrics.successes / _metrics.total) * 100f * scoringConfig.weightAccuracy;

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

            case EvaluationType.GuidedActivity:
                // Siempre perfecto
                rawScore = 100f;
                break;

            case EvaluationType.CustomMetrics:
                // Ya se calculó en HandleCustomMetricsCompletion
                rawScore = _metrics.score;
                break;
        }

        _metrics.score = Mathf.Clamp(Mathf.RoundToInt(rawScore), 0, 100);
        _metrics.stars = CalculateStars(_metrics.score);

        Debug.Log($"[Adapter] Score: {_metrics.score}/100, Stars: {_metrics.stars}");
    }

    private int CalculateStars(int score)
    {
        if (score >= scoringConfig.star3Threshold) return 3;
        if (score >= scoringConfig.star2Threshold) return 2;
        if (score >= scoringConfig.star1Threshold) return 1;
        return 0;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // GUARDADO
    // ═══════════════════════════════════════════════════════════════════════════

    private void SaveResult()
    {
        string message = GetDynamicMessage();

        if (ActivityScoringService.Instance != null)
            ActivityScoringService.Instance.SaveScore(activityId, _metrics, message);

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        int activityIndex = GetActivityIndex();

        if (activityIndex >= 0)
        {
            CompletionService.MarkActivity(scene, activityIndex);

            if (ProgressService.Instance != null)
                ProgressService.Instance.CommitMedal(scene, activityIndex);
        }
    }

    private string GetDynamicMessage()
    {
        if (summaryConfig != null)
            return summaryConfig.GetMessage(activityId, _metrics.stars, _metrics.errors);

        return customMessage;
    }

    private int GetActivityIndex()
    {
        if (GameManager.Instance == null) return -1;
        return GameManager.Instance.activities.IndexOf(targetActivity);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PANEL DE RESUMEN
    // ═══════════════════════════════════════════════════════════════════════════

    private void ShowSummaryPanel()
    {
        var panel = UnifiedSummaryPanel.Instance;

        if (panel == null)
        {
            Debug.LogError("[Adapter] ❌ UnifiedSummaryPanel no encontrado");
            if (targetActivity != null)
                targetActivity.CompleteActivity();
            return;
        }

        string message = GetDynamicMessage();

        Debug.Log($"[Adapter] Mostrando panel - Score: {_metrics.score}, Stars: {_metrics.stars}");
        panel.Show(_metrics, activityId, message, targetActivity);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // API PÚBLICA (Debug)
    // ═══════════════════════════════════════════════════════════════════════════

    public ActivityMetrics GetCurrentMetrics() => _metrics;
    public ScoringConfig GetScoringConfig() => scoringConfig;

    [ContextMenu("Test: Extract Metrics Now")]
    private void TestExtractMetrics()
    {
        ExtractMetrics();
        Debug.Log($"[Test] successes={_metrics.successes}, errors={_metrics.errors}, time={_metrics.timeSeconds:F2}s");
    }

    [ContextMenu("Test: Calculate Score Now")]
    private void TestCalculateScore()
    {
        ExtractMetrics();
        CalculateScore();
        Debug.Log($"[Test] Score: {_metrics.score}/100, Stars: {_metrics.stars}");
    }

    [ContextMenu("Activar Modo Calibración")]
    private void ActivateCalibrationMode()
    {
#if UNITY_EDITOR
        calibrationMode = true;
        EditorUtility.SetDirty(this);
        Debug.Log("[Adapter] 🎯 Modo calibración ACTIVADO");
#endif
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// CONFIGURACIONES
// ═══════════════════════════════════════════════════════════════════════════════

[System.Serializable]
public class MetricsSourceConfig
{
    [Header("Campos de la Actividad (Reflection)")]
    public string successesFieldName = "";
    public string errorsFieldName = "";
    public string dispatchesFieldName = "";

    [Header("Total Esperado")]
    [Tooltip("Si es 0, se calcula como successes + errors")]
    public int expectedTotal = 0;
}

[System.Serializable]
public class ScoringConfig
{
    [Header("Tipo de Evaluación")]
    public EvaluationType evaluationType = EvaluationType.AccuracyBased;

    [Header("Pesos (solo ComboMetric)")]
    [Range(0f, 1f)] public float weightAccuracy = 0.5f;
    [Range(0f, 1f)] public float weightSpeed = 0.3f;
    [Range(0f, 1f)] public float weightEfficiency = 0.2f;

    [Header("Umbrales de Estrellas")]
    [Range(0, 100)] public int star1Threshold = 50;
    [Range(0, 100)] public int star2Threshold = 75;
    [Range(0, 100)] public int star3Threshold = 90;

    [Header("Referencias")]
    [Tooltip("Tiempo ideal (para TimeBased/ComboMetric/CustomMetrics)")]
    public float idealTimeSeconds = 60f;

    [Tooltip("Máximo errores permitidos antes de penalización completa")]
    public int maxAllowedErrors = 3;
}

public enum EvaluationType
{
    AccuracyBased,    // Solo precisión (errores)
    TimeBased,        // Solo velocidad
    ComboMetric,      // Combinación
    GuidedActivity,   // Sin métricas, siempre 100%
    CustomMetrics     // La actividad pasa sus métricas
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