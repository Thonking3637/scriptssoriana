// LevelItemMeta.cs
using UnityEngine;

public enum LevelLaunchType { SceneByName, ActivityIndex }

public class LevelItemMeta : MonoBehaviour
{
    public Sprite preview;
    [TextArea] public string title;
    [TextArea(3, 10)] public string description;

    [Header("Lanzamiento")]
    public LevelLaunchType launchType = LevelLaunchType.ActivityIndex;
    public string sceneName;
    public int activityIndex;

    [Header("Disponibilidad")]
    public bool canLaunch = true;

    [Header("Cambio de panel (si no hay escena)")]
    public GameObject switchToPanel;
}
