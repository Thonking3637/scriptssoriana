using System;
using System.Collections.Generic;
using UnityEngine;

public static class CompletionService
{
    // ====== LOCAL (PlayerPrefs) ======
    private const string SceneDonePrefix = "SCENE_DONE_";
    private const string SceneCountPrefix = "SCENE_COUNT_";
    private const string ActivityDonePrefix = "ACT_DONE_"; // ACT_DONE_{scene}_{index}

    // ====== REMOTE OVERLAY (activityId) ======
    private static readonly HashSet<string> _remoteActivityIds = new HashSet<string>();
    private static bool _remoteLoadedOnce = false;

    private static ActivityIdMap _activityIdMap;

    public static event Action OnProgressChanged;
    public static bool IsConfigured => _activityIdMap != null;
    // ------- Config -------
    public static void Configure(ActivityIdMap map)
    {
        if (map == null)
        {
            Debug.LogError("[CompletionService] Configure called with null ActivityIdMap!");
            return;
        }

        _activityIdMap = map;
        Debug.Log($"[CompletionService] Configured with ActivityIdMap");
    }

    // ------- Keys -------
    private static string ActKey(string scene, int index) => $"{ActivityDonePrefix}{scene}_{index}";

    // ------- Scene counts -------
    public static int GetSceneActivityCount(string scene)
    {
        if (string.IsNullOrWhiteSpace(scene)) return 0;
        return PlayerPrefs.GetInt(SceneCountPrefix + scene, 0);
    }

    public static void SetSceneActivityCount(string scene, int count)
    {
        if (string.IsNullOrWhiteSpace(scene)) return;
        PlayerPrefs.SetInt(SceneCountPrefix + scene, Mathf.Max(0, count));
        PlayerPrefs.Save();
        OnProgressChanged?.Invoke();
    }

    // ------- Progress -------
    public static (int done, int total) GetSceneProgress(string scene)
    {
        int total = GetSceneActivityCount(scene);
        if (total <= 0) return (0, 0);

        int done = 0;
        for (int i = 0; i < total; i++)
            if (IsActivityDone(scene, i)) done++;

        return (done, total);
    }

    public static (int done, int total) GetModuleProgress(List<string> sceneNames)
    {
        if (sceneNames == null || sceneNames.Count == 0) return (0, 0);

        int done = 0, total = 0;
        foreach (var scn in sceneNames)
        {
            var (d, t) = GetSceneProgress(scn);
            done += d;
            total += t;
        }
        return (done, total);
    }

    public static void MarkActivity(string scene, int index)
    {
        if (string.IsNullOrWhiteSpace(scene) || index < 0) return;

        PlayerPrefs.SetInt(ActKey(scene, index), 1);
        PlayerPrefs.Save();
        OnProgressChanged?.Invoke();
    }

    public static bool IsActivityDone(string scene, int index)
    {
        if (!IsConfigured)
        {
            Debug.LogWarning("[CompletionService] Not configured yet. Call Configure() first.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(scene) || index < 0)
            return false;

        // 1) LOCAL (PlayerPrefs)
        if (PlayerPrefs.GetInt(ActKey(scene, index), 0) == 1)
            return true;

        // 2) REMOTO (overlay desde Firebase)
        if (_activityIdMap != null &&
            _activityIdMap.TryGetActivityId(scene, index, out var activityId))
        {
            return _remoteActivityIds.Contains(activityId);
        }

        return false;
    }

    public static void MarkSceneCompleted(string scene, bool completed)
    {
        if (string.IsNullOrWhiteSpace(scene)) return;
        PlayerPrefs.SetInt(SceneDonePrefix + scene, completed ? 1 : 0);
        PlayerPrefs.Save();
        OnProgressChanged?.Invoke();
    }

    public static bool IsSceneCompleted(string scene)
    {
        if (string.IsNullOrWhiteSpace(scene)) return false;
        return PlayerPrefs.GetInt(SceneDonePrefix + scene, 0) == 1;
    }

    // ------- Remote overlay API -------
    public static void SetRemoteMedals(IEnumerable<string> medalIds)
    {
        _remoteActivityIds.Clear();

        if (medalIds != null)
        {
            foreach (var id in medalIds)
            {
                if (!string.IsNullOrWhiteSpace(id))
                    _remoteActivityIds.Add(id.Trim());
            }
        }

        _remoteLoadedOnce = true;
        OnProgressChanged?.Invoke();
    }

    public static bool HasRemoteSnapshot => _remoteLoadedOnce;

    public static void ClearRemoteSnapshot()
    {
        _remoteActivityIds.Clear();
        _remoteLoadedOnce = false;
        OnProgressChanged?.Invoke();
    }

    public static void NotifyChanged()
    {
        OnProgressChanged?.Invoke();
    }
}
