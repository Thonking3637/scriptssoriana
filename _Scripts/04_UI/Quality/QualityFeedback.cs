using UnityEngine;
using TMPro;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class QualityFeedback : MonoBehaviour
{
    public TextMeshProUGUI label;      // Arrastra un TMP_Text
    public CanvasGroup group;          // CanvasGroup del cartel

    void Awake()
    {
        group.alpha = 0f;
        ShowNow();
    }

    public void ShowNow()
    {
        StopAllCoroutines();
        StartCoroutine(ShowRoutine());
    }

    IEnumerator ShowRoutine()
    {
        int i = QualitySettings.GetQualityLevel();
        string qName = QualitySettings.names[i];

        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        string msaa = urp ? (urp.msaaSampleCount == 0 ? "0" : urp.msaaSampleCount.ToString()) : "-";
        string rs = urp ? urp.renderScale.ToString("0.00") : "-";

        label.text = $"CALIDAD: {qName}\nMSAA: {msaa}  |  RS: {rs}";
        // fade in/out simple
        group.alpha = 1f;
        yield return new WaitForSeconds(1.5f);
        float t = 0f;
        while (t < 0.4f) { t += Time.unscaledDeltaTime; group.alpha = 1f - (t / 0.4f); yield return null; }
        group.alpha = 0f;
    }
}
