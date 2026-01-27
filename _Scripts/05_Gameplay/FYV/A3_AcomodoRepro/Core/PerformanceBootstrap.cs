using UnityEngine;

public class PerformanceBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        QualitySettings.vSyncCount = 0;      // evita bloqueo a 30 por vSync
        Application.targetFrameRate = 60;    // fuerza 60
    }
}
