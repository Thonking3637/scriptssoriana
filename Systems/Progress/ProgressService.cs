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

    private FirebaseFirestore _db;
    private bool _remoteBusy;

    public event Action OnRemoteProgressUpdated;

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

    // =========================
    // ATTEMPTS (Activity timing)
    // =========================

    public void StartAttempt(string moduleId, string activityId, Action<string> onAttemptCreated)
    {
        string attemptId = Guid.NewGuid().ToString("N");
        onAttemptCreated?.Invoke(attemptId);

        Debug.Log($"[ProgressService] START Attempt {attemptId} {moduleId}/{activityId}");
    }

    public void EndAttempt(
        string attemptId,
        string moduleId,
        string activityId,
        int durationSec,
        bool completed,
        int? score,
        int? mistakes
    )
    {
        Debug.Log(
            $"[ProgressService] END Attempt {attemptId} | " +
            $"dur={durationSec}s completed={completed} score={score} mistakes={mistakes}"
        );
    }

    // =========================
    // MEDALS (Local Earn hook)
    // =========================

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

        Debug.Log($"[ProgressService] MEDAL Earned -> {activityId} | uid={uid} emp={emp} store={store} role={role}");

        // (Tu escritura a Firestore de medals ya existe en tu flujo actual)
        // Esto solo es el hook.
    }

    // =========================
    // REMOTE MEDALS (Menu reads)
    // =========================

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
            Debug.Log($"[ProgressService] Medal written -> {activityId}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ProgressService] CommitMedal failed: {ex.GetType().Name} {ex.Message}");
        }
    }

}
