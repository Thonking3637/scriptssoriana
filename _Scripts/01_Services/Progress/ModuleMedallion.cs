// ModuleMedallion.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ModuleMedallion : MonoBehaviour
{
    public TrainingModule module;
    public Image fillImage;
    public Image checkIcon;
    public TextMeshProUGUI label;

    void OnEnable() => Refresh();

    public void Refresh()
    {
        // Pre-cargar totales por escena si aún no existen
        foreach (var scn in module.scenes)
            if (CompletionService.GetSceneActivityCount(scn.sceneName) == 0 && scn.activityCount > 0)
                CompletionService.SetSceneActivityCount(scn.sceneName, scn.activityCount);

        var names = module.scenes.ConvertAll(s => s.sceneName);
        var (done, total) = CompletionService.GetModuleProgress(names);

        float fill = (total > 0) ? (float)done / total : 0f;
        if (fillImage) { fillImage.type = Image.Type.Filled; fillImage.fillAmount = fill; }
        if (checkIcon) checkIcon.enabled = (total > 0 && done >= total);
        if (label) label.text = total > 0 ? $"{done}/{total}" : "0/0";
    }
}
