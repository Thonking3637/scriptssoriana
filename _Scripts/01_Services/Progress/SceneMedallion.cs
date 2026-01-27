// SceneMedallion.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SceneMedallion : MonoBehaviour
{
    [Header("Data")]
    public string sceneName;
    public int fallbackActivityCount = 0; // del SO si aún no jugaste esa escena

    [Header("UI")]
    public Image fillImage;           // Image -> Type: Filled
    public Image checkIcon;           // aparece al 100%
    public TextMeshProUGUI label;     // "X/Y"

    [Range(0, 1f)] public float minVisibleFill = 0.02f;

    void OnEnable() => Refresh();

    public void Refresh()
    {
        if (CompletionService.GetSceneActivityCount(sceneName) == 0 && fallbackActivityCount > 0)
            CompletionService.SetSceneActivityCount(sceneName, fallbackActivityCount);

        var (done, total) = CompletionService.GetSceneProgress(sceneName);
        float fill = (total > 0) ? (float)done / total : 0f;
        if (fill > 0 && fill < minVisibleFill) fill = minVisibleFill;

        if (fillImage) { fillImage.type = Image.Type.Filled; fillImage.fillAmount = fill; }
        if (checkIcon) checkIcon.enabled = (total > 0 && done >= total);
        if (label) label.text = total > 0 ? $"{done}/{total}" : "0/0";
    }
}
