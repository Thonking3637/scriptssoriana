// SceneMedallion.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SceneMedallion : MonoBehaviour
{
    [Header("Data")]
    public string sceneName;
    public int fallbackActivityCount = 0;

    [Header("UI")]
    public Image fillImage;
    public Image checkIcon;
    public TextMeshProUGUI label;

    [Range(0, 1f)] public float minVisibleFill = 0.02f;

    void OnEnable() => Refresh();

    public void Refresh()
    {
        int savedCount = CompletionService.GetSceneActivityCount(sceneName);

        // Usar el count guardado si existe, sino usar el fallback
        int displayTotal = savedCount > 0 ? savedCount : fallbackActivityCount;

        // Contar actividades completadas
        int done = 0;
        for (int i = 0; i < displayTotal; i++)
        {
            if (CompletionService.IsActivityDone(sceneName, i))
                done++;
        }

        float fill = (displayTotal > 0) ? (float)done / displayTotal : 0f;
        if (fill > 0 && fill < minVisibleFill) fill = minVisibleFill;

        if (fillImage)
        {
            fillImage.type = Image.Type.Filled;
            fillImage.fillAmount = fill;
        }

        if (checkIcon)
        {
            checkIcon.enabled = (displayTotal > 0 && done >= displayTotal);
        }

        if (label)
        {
            label.text = displayTotal > 0 ? $"{done}/{displayTotal}" : "0/0";
        }
    }
}