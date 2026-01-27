using System.Threading.Tasks;
using UnityEngine;
using Firebase;

public class FirebaseBootstrap : MonoBehaviour
{
    public static bool IsReady { get; private set; }

    private static Task<bool> _initTask;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        _ = EnsureFirebaseReady();
    }

    /// <summary>
    /// Inicializa Firebase una sola vez. Devuelve true si está listo.
    /// </summary>
    public static Task<bool> EnsureFirebaseReady()
    {
        if (IsReady) return Task.FromResult(true);
        if (_initTask != null) return _initTask;

        _initTask = InitInternal();
        return _initTask;
    }

    private static async Task<bool> InitInternal()
    {
        var status = await FirebaseApp.CheckAndFixDependenciesAsync();
        if (status != DependencyStatus.Available)
        {
            IsReady = false;
            Debug.LogError($"[FirebaseBootstrap] Firebase dependencies not available: {status}");
            return false;
        }

        IsReady = true;

        // Log útil para verificar proyecto real en device
        var app = FirebaseApp.DefaultInstance;
        Debug.Log($"[FirebaseBootstrap] Firebase READY | projectId={app.Options?.ProjectId} appId={app.Options?.AppId} apiKey={(string.IsNullOrEmpty(app.Options?.ApiKey) ? "EMPTY" : "OK")}");

        return true;
    }
}
