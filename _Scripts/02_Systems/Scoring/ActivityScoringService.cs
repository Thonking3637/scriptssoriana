// ═══════════════════════════════════════════════════════════════════════════════
// ActivityScoringService.cs
// Servicio singleton para guardar y recuperar scores de actividades
// Integración con PlayerPrefs (local) y Firebase (remoto)
// ═══════════════════════════════════════════════════════════════════════════════

using UnityEngine;
using System.Collections.Generic;

public class ActivityScoringService : MonoBehaviour
{
    public static ActivityScoringService Instance { get; private set; }

    // Cache de mejores scores
    private Dictionary<string, ActivityScoreData> _bestScores = new Dictionary<string, ActivityScoreData>();

    private const string PREFS_PREFIX = "ACT_SCORE_";

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
        DontDestroyOnLoad(gameObject);

        Debug.Log("[ScoringService] Inicializado");
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // GUARDAR SCORE
    // ═════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Guarda el score de una actividad (local y remoto)
    /// </summary>
    public void SaveScore(string activityId, ActivityMetrics metrics, string customMessage)
    {
        if (string.IsNullOrEmpty(activityId))
        {
            Debug.LogWarning("[ScoringService] activityId vacío, no se puede guardar");
            return;
        }

        var scoreData = new ActivityScoreData
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

        // Guardar localmente
        SaveLocal(scoreData);

        // Actualizar mejor score si aplica
        UpdateBestScore(scoreData);

        // Guardar en Firebase (async)
        SaveRemoteAsync(scoreData);

        Debug.Log($"[ScoringService] Score guardado: {activityId} → {metrics.score}/100 ({metrics.stars}★)");
    }

    /// <summary>
    /// Guarda en PlayerPrefs
    /// </summary>
    private void SaveLocal(ActivityScoreData data)
    {
        string key = PREFS_PREFIX + data.activityId;

        PlayerPrefs.SetInt(key + "_score", data.score);
        PlayerPrefs.SetInt(key + "_stars", data.stars);
        PlayerPrefs.SetFloat(key + "_accuracy", data.accuracy);
        PlayerPrefs.SetFloat(key + "_time", data.timeSeconds);
        PlayerPrefs.SetInt(key + "_successes", data.successes);
        PlayerPrefs.SetInt(key + "_errors", data.errors);
        PlayerPrefs.SetString(key + "_timestamp", data.timestamp);
        PlayerPrefs.SetString(key + "_message", data.customMessage);

        PlayerPrefs.Save();
    }

    /// <summary>
    /// Actualiza el mejor score en cache
    /// </summary>
    private void UpdateBestScore(ActivityScoreData newData)
    {
        if (!_bestScores.ContainsKey(newData.activityId))
        {
            _bestScores[newData.activityId] = newData;
            return;
        }

        var current = _bestScores[newData.activityId];

        // Comparar: primero estrellas, luego score
        bool isNewBest = (newData.stars > current.stars) ||
                        (newData.stars == current.stars && newData.score > current.score);

        if (isNewBest)
        {
            _bestScores[newData.activityId] = newData;
            Debug.Log($"[ScoringService] ¡Nuevo récord personal! {newData.activityId}: {newData.score}/100 ({newData.stars}★)");
        }
    }

    /// <summary>
    /// Guarda en Firebase (async, no bloquea)
    /// </summary>
    private async void SaveRemoteAsync(ActivityScoreData data)
    {
        try
        {
            var writer = FindObjectOfType<FirestoreProgressWriter>();
            if (writer == null)
            {
                Debug.LogWarning("[ScoringService] FirestoreProgressWriter no encontrado, skip remote save");
                return;
            }

            var session = SessionContext.Instance;
            if (session == null || !session.IsLoggedIn)
            {
                Debug.LogWarning("[ScoringService] Usuario no logueado, skip remote save");
                return;
            }

            await writer.RecordScoreAsync(session.Uid, data);
            Debug.Log($"[ScoringService] Score sincronizado con Firebase: {data.activityId}");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[ScoringService] Error al guardar en Firebase: {ex.Message}");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // RECUPERAR SCORE
    // ═════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Obtiene el mejor score de una actividad
    /// </summary>
    public ActivityScoreData GetBestScore(string activityId)
    {
        if (string.IsNullOrEmpty(activityId)) return null;

        // Verificar cache
        if (_bestScores.TryGetValue(activityId, out var cached))
            return cached;

        // Cargar de PlayerPrefs
        var data = LoadLocal(activityId);
        if (data != null)
        {
            _bestScores[activityId] = data;
            return data;
        }

        return null;
    }

    /// <summary>
    /// Carga score de PlayerPrefs
    /// </summary>
    private ActivityScoreData LoadLocal(string activityId)
    {
        string key = PREFS_PREFIX + activityId;

        if (!PlayerPrefs.HasKey(key + "_score"))
            return null;

        return new ActivityScoreData
        {
            activityId = activityId,
            score = PlayerPrefs.GetInt(key + "_score"),
            stars = PlayerPrefs.GetInt(key + "_stars"),
            accuracy = PlayerPrefs.GetFloat(key + "_accuracy"),
            timeSeconds = PlayerPrefs.GetFloat(key + "_time"),
            successes = PlayerPrefs.GetInt(key + "_successes"),
            errors = PlayerPrefs.GetInt(key + "_errors"),
            timestamp = PlayerPrefs.GetString(key + "_timestamp"),
            customMessage = PlayerPrefs.GetString(key + "_message")
        };
    }

    /// <summary>
    /// Verifica si una actividad tiene score guardado
    /// </summary>
    public bool HasScore(string activityId)
    {
        return GetBestScore(activityId) != null;
    }

    /// <summary>
    /// Limpia todos los scores (útil para testing)
    /// </summary>
    [ContextMenu("Clear All Scores")]
    public void ClearAllScores()
    {
        _bestScores.Clear();

        // Limpiar PlayerPrefs (buscar todas las keys con el prefijo)
        var keysToDelete = new List<string>();

        // Nota: PlayerPrefs no tiene API para listar keys, así que usamos un approach aproximado
        // En producción, mantener una lista de activityIds conocidos

        Debug.Log("[ScoringService] Scores limpiados del cache");
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// DATOS
// ═══════════════════════════════════════════════════════════════════════════════

[System.Serializable]
public class ActivityScoreData
{
    public string activityId;
    public int score;           // 0-100
    public int stars;           // 0-3
    public float accuracy;      // %
    public float timeSeconds;
    public int successes;
    public int errors;
    public int dispatches;
    public string customMessage;
    public string timestamp;

    public string GetStarsText()
    {
        return new string('⭐', stars) + new string('☆', 3 - stars);
    }

    public string GetQualityText()
    {
        if (stars == 3) return "EXCELENTE";
        if (stars == 2) return "MUY BIEN";
        if (stars == 1) return "BIEN";
        return "INTENTA DE NUEVO";
    }

    public Color GetQualityColor()
    {
        if (stars == 3) return new Color(1f, 0.84f, 0f);      // Dorado
        if (stars == 2) return new Color(0.75f, 0.75f, 0.75f); // Plateado
        if (stars == 1) return new Color(0.8f, 0.5f, 0.2f);    // Bronce
        return Color.gray;
    }
}