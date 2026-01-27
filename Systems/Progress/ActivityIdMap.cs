using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Training/ActivityIdMap", fileName = "ActivityIdMap")]
public class ActivityIdMap : ScriptableObject
{
    [Serializable]
    public class SceneMap
    {
        public string sceneName;
        public List<string> activityIdsByIndex = new();
    }

    [SerializeField] private List<SceneMap> scenes = new();

    public bool TryGetActivityId(string sceneName, int index, out string activityId)
    {
        activityId = null;

        var scene = scenes.Find(s => s.sceneName == sceneName);
        if (scene == null) return false;
        if (index < 0 || index >= scene.activityIdsByIndex.Count) return false;

        activityId = scene.activityIdsByIndex[index];
        return !string.IsNullOrEmpty(activityId);
    }
}
