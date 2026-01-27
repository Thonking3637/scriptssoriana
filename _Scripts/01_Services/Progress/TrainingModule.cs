using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Training/Module Definition")]
public class TrainingModule : ScriptableObject
{
    public string moduleName;                 // "SETC" o "LÍNEA DE CAJAS"
    public List<SceneEntry> scenes;

    [System.Serializable]
    public class SceneEntry
    {
        public string sceneName;              // nombre EXACTO de la escena
        public int activityCount;             // 7 (SETC) | 5,7,10,3 (Línea de Cajas)
        public Sprite icon;                   // opcional
    }
}