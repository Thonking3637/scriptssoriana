using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(100)] // corre después de bootstraps comunes
public class SceneMusicAutoPlay : MonoBehaviour
{
    [Header("Música base de ESTA escena")]
    public AudioClip sceneMusicClip;

    [Tooltip("Trim opcional por escena (se multiplica a Master*Music)")]
    [Range(0f, 1f)] public float sceneTrim = 1f;
    public bool useSceneTrim = false;

    IEnumerator Start()
    {
        // Un frame de gracia para dejar terminar inits/restores de otros scripts
        yield return null;

        var sm = SoundManager.Instance;
        if (sm == null || sceneMusicClip == null) yield break;

        // 🚩 Aquí nos imponemos: fijamos la música base y limpiamos overrides
        sm.SetSceneBaseMusic(sceneMusicClip, useSceneTrim ? sceneTrim : 1f, force: true);
    }
}