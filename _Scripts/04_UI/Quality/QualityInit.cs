// QualityInit.cs
using UnityEngine;
using System;

public static class QualityInit
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplySavedOrLow()
    {
        const string PREF = "gfx_qidx";

        // �
        // Indice de "Low" como fallback
        int low = Array.FindIndex(QualitySettings.names,
            n => string.Equals(n, "Low", StringComparison.OrdinalIgnoreCase));
        if (low < 0) low = 0;

        // si no hay preferencia guardada, arranca en Low
        int idx = PlayerPrefs.GetInt(PREF, low);
        idx = Mathf.Clamp(idx, 0, QualitySettings.names.Length - 1);

        QualitySettings.SetQualityLevel(idx, true);
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
    }
}