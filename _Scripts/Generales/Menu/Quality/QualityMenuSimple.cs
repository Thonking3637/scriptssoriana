// QualityMenuSimple.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal; // <- importante

public class QualityMenuSimple : MonoBehaviour
{
    [Header("Botones")]
    public Button btnLow, btnMed, btnHigh;

    [Header("Indicadores (opcional)")]
    public GameObject markLow, markMed, markHigh;

    [Header("Nombres de niveles")]
    public string lowName = "Low";
    public string medName = "Medium";
    public string highName = "High";

    const string PREF = "gfx_qidx";
    int idxLow = 0, idxMed = 1, idxHigh = 2;

    QualityFeedback feedback;
    QualityPostSwitcher postSwitcher;

    void Awake()
    {
        feedback = FindObjectOfType<QualityFeedback>(true);
        postSwitcher = FindObjectOfType<QualityPostSwitcher>(true);

        var names = QualitySettings.names;
        idxLow = IndexOf(names, lowName, 0);
        idxMed = IndexOf(names, medName, Mathf.Min(1, names.Length - 1));
        idxHigh = IndexOf(names, highName, Mathf.Min(2, names.Length - 1));
    }

    void Start()
    {
        if (btnLow) btnLow.onClick.AddListener(() => SetQuality(idxLow));
        if (btnMed) btnMed.onClick.AddListener(() => SetQuality(idxMed));
        if (btnHigh) btnHigh.onClick.AddListener(() => SetQuality(idxHigh));

        int saved = PlayerPrefs.GetInt(PREF, QualitySettings.GetQualityLevel());
        saved = Mathf.Clamp(saved, 0, QualitySettings.names.Length - 1);
        SetQuality(saved);
    }

    void OnEnable() => RefreshMarks(QualitySettings.GetQualityLevel());

    void SetQuality(int index)
    {
        index = Mathf.Clamp(index, 0, QualitySettings.names.Length - 1);

        QualitySettings.SetQualityLevel(index, true);
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;

        PlayerPrefs.SetInt(PREF, index);
        PlayerPrefs.Save();

        RefreshMarks(index);

        // Mostrar overlay / cambiar post
        if (feedback) feedback.ShowNow();
        if (postSwitcher) postSwitcher.ApplyForCurrent();

        // ⚡ AA en cámara: FXAA en Low, None en Med/High (MSAA lo maneja el URP Asset)
        var cam = Camera.main;
        var data = cam ? cam.GetUniversalAdditionalCameraData() : null;
        if (data)
            data.antialiasing = (index == idxLow) ? AntialiasingMode.FastApproximateAntialiasing
                                                  : AntialiasingMode.None;
    }

    void RefreshMarks(int index)
    {
        if (markLow) markLow.SetActive(index == idxLow);
        if (markMed) markMed.SetActive(index == idxMed);
        if (markHigh) markHigh.SetActive(index == idxHigh);
    }

    static int IndexOf(string[] arr, string target, int fallback)
    {
        if (arr == null || arr.Length == 0) return fallback;
        int i = System.Array.FindIndex(arr, n => string.Equals(n, target, System.StringComparison.OrdinalIgnoreCase));
        return (i >= 0) ? i : Mathf.Clamp(fallback, 0, arr.Length - 1);
    }
}
