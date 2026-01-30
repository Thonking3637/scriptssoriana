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
        int totalDone = 0;
        int totalActivities = 0;

        foreach (var scn in module.scenes)
        {
            int savedCount = CompletionService.GetSceneActivityCount(scn.sceneName);
            int displayTotal = savedCount > 0 ? savedCount : scn.activityCount;

            int done = 0;
            for (int i = 0; i < displayTotal; i++)
            {
                bool isDone = CompletionService.IsActivityDone(scn.sceneName, i);
                Debug.Log($"[ModuleMedallion] {scn.sceneName} index {i}: isDone={isDone}");
                if (isDone)
                    done++;
            }

            Debug.Log($"[ModuleMedallion] {scn.sceneName}: done={done}/{displayTotal}, savedCount={savedCount}");

            totalDone += done;
            totalActivities += displayTotal;
        }

        Debug.Log($"[ModuleMedallion] TOTAL: {totalDone}/{totalActivities}");

        float fill = (totalActivities > 0) ? (float)totalDone / totalActivities : 0f;

        if (fillImage)
        {
            fillImage.type = Image.Type.Filled;
            fillImage.fillAmount = fill;
        }

        if (checkIcon)
        {
            checkIcon.enabled = (totalActivities > 0 && totalDone >= totalActivities);
        }

        if (label)
        {
            label.text = totalActivities > 0 ? $"{totalDone}/{totalActivities}" : "0/0";
        }
    }
}