// ═══════════════════════════════════════════════════════════════════════════════
// ProgressService.cs
// Servicio de progreso con integración completa a Firebase
// Maneja: attempts con score/mistakes, medals, y sincronización remota
// ═══════════════════════════════════════════════════════════════════════════════

using Firebase.Firestore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class ProgressService : MonoBehaviour
{
    public static ProgressService Instance { get; private set; }

    [Header("References")]
    [SerializeField] private ActivityIdMap activityIdMap;

    [Header("Remote Medals (Firestore)")]
    [SerializeField] private bool enableRemoteMedals = true;
    [SerializeField] private float remoteTimeoutSeconds = 6f;

    [Header("Attempt Recording")]
    [Tooltip("Si está activo, guarda cada attempt en Firebase con score y mistakes")]
    [SerializeField] private bool enableAttemptRecording = true;

    private FirebaseFirestore _db;
    private bool _remoteBusy;

    public event Action OnRemoteProgressUpdated;

    // ═════════════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═════════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Para que CompletionService pueda mapear scene/index => activityId
        CompletionService.Configure(activityIdMap);
    }

    private void Start()
    {
        if (enableRemoteMedals)
        {
            _ = InitializeFirestoreAsync();
        }
    }

    private async Task InitializeFirestoreAsync()
    {
        try
        {
            await FirebaseBootstrap.EnsureFirebaseReady();
            _db = FirebaseFirestore.DefaultInstance;
            TryRefreshRemoteMedals();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ProgressService] Firestore init failed (no bloquea): {ex.GetType().Name} {ex.Message}");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // ATTEMPTS (Activity timing + scoring)
    // ═════════════════════════════════════════════════════════════════════════════

    public void StartAttempt(string moduleId, string activityId, Action<string> onAttemptCreated)
    {
        string attemptId = Guid.NewGuid().ToString("N");
        onAttemptCreated?.Invoke(attemptId);

#if UNITY_EDITOR
        Debug.Log($"[ProgressService] START Attempt {attemptId} {moduleId}/{activityId}");
#endif
    }

    public void EndAttempt(
        string attemptId,
        string moduleId,
        string activityId,
        int durationSec,
        bool completed,
        int? score,
        int? mistakes)
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // PASO 1: Si score/mistakes son null, intentar obtenerlos del ActivityScoringService
        // ═══════════════════════════════════════════════════════════════════════════
        int? finalScore = score;
        int? finalMistakes = mistakes;

        if (completed && (!finalScore.HasValue || !finalMistakes.HasValue))
        {
            var scoreData = GetScoreDataFromScoringService(activityId);
            if (scoreData != null)
            {
                if (!finalScore.HasValue)
                    finalScore = scoreData.score;

                if (!finalMistakes.HasValue)
                    finalMistakes = scoreData.errors;

#if UNITY_EDITOR
                Debug.Log($"[ProgressService] ✅ Métricas obtenidas de ScoringService: score={finalScore}, mistakes={finalMistakes}");
#endif
            }
            else
            {
                Debug.LogWarning($"[ProgressService] ⚠️ No se encontraron métricas en ScoringService para {activityId}");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // PASO 2: Log con datos completos
        // ═══════════════════════════════════════════════════════════════════════════
#if UNITY_EDITOR
        Debug.Log(
            $"[ProgressService] END Attempt {attemptId} | " +
            $"dur={durationSec}s completed={completed} score={finalScore} mistakes={finalMistakes}"
        );
#endif

        // ═══════════════════════════════════════════════════════════════════════════
        // PASO 3: Guardar en Firebase si está habilitado
        // ═══════════════════════════════════════════════════════════════════════════
        if (enableAttemptRecording && completed)
        {
            _ = RecordAttemptToFirebaseAsync(attemptId, moduleId, activityId, durationSec, finalScore, finalMistakes);
        }
    }

    /// <summary>
    /// Obtiene los datos de score del ActivityScoringService
    /// </summary>
    private ActivityScoreData GetScoreDataFromScoringService(string activityId)
    {
        if (string.IsNullOrEmpty(activityId))
            return null;

        if (ActivityScoringService.Instance == null)
        {
            Debug.LogWarning("[ProgressService] ActivityScoringService.Instance es null");
            return null;
        }

        return ActivityScoringService.Instance.GetBestScore(activityId);
    }

    /// <summary>
    /// Guarda el attempt en Firebase usando el FirestoreProgressWriter existente
    /// </summary>
    private async Task RecordAttemptToFirebaseAsync(
        string attemptId,
        string moduleId,
        string activityId,
        int durationSec,
        int? score,
        int? mistakes)
    {
        try
        {
            var session = SessionContext.Instance;
            if (session == null || !session.IsLoggedIn)
            {
                Debug.LogWarning("[ProgressService] Usuario no logueado, skip attempt recording");
                return;
            }

            // Buscar el writer
            var writer = FindObjectOfType<FirestoreProgressWriter>();
            if (writer == null)
            {
                Debug.LogWarning("[ProgressService] FirestoreProgressWriter no encontrado");
                return;
            }

            // ═══════════════════════════════════════════════════════════════════════════
            // Usar el método RecordScoreAsync que ya tienes en tu extensión
            // Esto guarda en: users/{uid}/activities/{activityId}/attempts/{attemptId}
            // ═══════════════════════════════════════════════════════════════════════════

            // Crear ActivityScoreData con los datos del attempt
            var scoreData = ActivityScoringService.Instance?.GetBestScore(activityId);

            if (scoreData != null)
            {
                // Ya tenemos datos completos del ScoringService
                await writer.RecordScoreAsync(session.Uid, scoreData);
#if UNITY_EDITOR
                Debug.Log($"[ProgressService] ✅ Score completo guardado en Firebase: {activityId}");
#endif
            }
            else
            {
                // Crear datos mínimos si no hay ScoringService
                var minimalData = new ActivityScoreData
                {
                    activityId = activityId,
                    score = score ?? 0,
                    stars = CalculateStarsFromScore(score ?? 0),
                    errors = mistakes ?? 0,
                    timeSeconds = durationSec,
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                await writer.RecordScoreAsync(session.Uid, minimalData);
#if UNITY_EDITOR
                Debug.Log($"[ProgressService] ✅ Score mínimo guardado en Firebase: {activityId}");
#endif
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ProgressService] Error guardando attempt en Firebase: {ex.Message}");
        }
    }

    /// <summary>
    /// Calcula estrellas basado en score (fallback si no hay config)
    /// </summary>
    private int CalculateStarsFromScore(int score)
    {
        if (score >= 90) return 3;
        if (score >= 75) return 2;
        if (score >= 50) return 1;
        return 0;
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // MEDALS (Local Earn hook)
    // ═════════════════════════════════════════════════════════════════════════════

    public void OnLocalMedalEarned(string sceneName, int activityIndex)
    {
        if (activityIdMap == null)
        {
            Debug.LogWarning("[ProgressService] ActivityIdMap not assigned.");
            return;
        }

        if (!activityIdMap.TryGetActivityId(sceneName, activityIndex, out var activityId))
        {
            Debug.LogWarning($"[ProgressService] No mapping for {sceneName} index {activityIndex}");
            return;
        }

        var s = SessionContext.Instance;
        string uid = (s != null && s.IsLoggedIn) ? s.Uid : "NO_LOGIN";
        string emp = (s != null && s.IsLoggedIn) ? s.EmployeeCode : "-";
        string store = (s != null && s.IsLoggedIn) ? s.StoreId : "-";
        string role = (s != null && s.IsLoggedIn) ? s.RoleId : "";

#if UNITY_EDITOR
        Debug.Log($"[ProgressService] MEDAL Earned -> {activityId} | uid={uid} emp={emp} store={store} role={role}");
#endif
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // REMOTE MEDALS (Menu reads)
    // ═════════════════════════════════════════════════════════════════════════════

    public void TryRefreshRemoteMedals()
    {
        if (!enableRemoteMedals) return;
        if (_remoteBusy) return;

        var s = SessionContext.Instance;
        if (s == null || !s.IsLoggedIn) return;

        _ = RefreshRemoteMedalsAsync(s.Uid);
    }

    private async Task RefreshRemoteMedalsAsync(string uid)
    {
        _remoteBusy = true;

        try
        {
            await FirebaseBootstrap.EnsureFirebaseReady();
            if (_db == null) _db = FirebaseFirestore.DefaultInstance;

            var loadTask = LoadMedalIdsAsync(uid);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(remoteTimeoutSeconds));

            var finished = await Task.WhenAny(loadTask, timeoutTask);
            if (finished != loadTask)
            {
                Debug.LogWarning("[ProgressService] Remote medals load timeout (no bloquea).");
                return;
            }

            var medalIds = await loadTask;

            // Alimenta CompletionService (remote-first)
            CompletionService.SetRemoteMedals(medalIds);
            CompletionService.NotifyChanged();
            Debug.Log($"[ProgressService] Remote medals loaded: {medalIds.Count}");
            OnRemoteProgressUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ProgressService] Remote medals load failed (no bloquea): {ex.GetType().Name} {ex.Message}");
        }
        finally
        {
            _remoteBusy = false;
        }
    }

    private async Task<List<string>> LoadMedalIdsAsync(string uid)
    {
        var col = _db.Collection("users").Document(uid).Collection("medals");
        var snap = await col.GetSnapshotAsync();

        var list = new List<string>(snap.Count);
        foreach (var doc in snap.Documents)
            list.Add(doc.Id); // docId == activityId

        return list;
    }

    public IEnumerator LoadRemoteMedalsIntoCompletionService()
    {
        if (!enableRemoteMedals) yield break;
        if (_remoteBusy) yield break;

        var s = SessionContext.Instance;
        if (s == null || !s.IsLoggedIn) yield break;

        bool received = false;
        void Handler() => received = true;

        OnRemoteProgressUpdated += Handler;

        TryRefreshRemoteMedals();

        float t = 0f;
        float max = Mathf.Max(1f, remoteTimeoutSeconds + 0.5f);

        while (!received && t < max)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        OnRemoteProgressUpdated -= Handler;
    }

    public void CommitMedal(string sceneName, int activityIndex)
    {
        _ = CommitMedalAsync(sceneName, activityIndex);
    }

    private async Task CommitMedalAsync(string sceneName, int activityIndex)
    {
        try
        {
            if (activityIdMap == null) return;
            if (!activityIdMap.TryGetActivityId(sceneName, activityIndex, out var activityId)) return;

            var s = SessionContext.Instance;
            if (s == null || !s.IsLoggedIn) return;

            var writer = FindObjectOfType<FirestoreProgressWriter>();
            if (writer == null)
            {
                Debug.LogWarning("[ProgressService] FirestoreProgressWriter not found");
                return;
            }

            await writer.RecordMedalAsync(s.Uid, activityId);
#if UNITY_EDITOR
            Debug.Log($"[ProgressService] Medal written -> {activityId}");
#endif
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ProgressService] CommitMedal failed: {ex.GetType().Name} {ex.Message}");
        }
    }
}