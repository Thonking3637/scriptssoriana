using UnityEngine;
using UnityEngine.UI;

public class ClaseoAreaCounter : MonoBehaviour
{
    [Tooltip("Meta por área (jaba). Para Claseo: 8.")]
    public int target = 8;

    [Tooltip("Cuenta actual de elementos colocados en esta área.")]
    public int count = 0;

    [Tooltip("TMP o Text opcional para mostrar progreso (ej. 5/8).")]
    public TMPro.TMP_Text txtProgress;

    [Tooltip("Barrita opcional de UI (0..1).")]
    public Image fillBar;

    public System.Action<ClaseoAreaCounter> onAreaCompleted;

    public void ResetCounter()
    {
        count = 0;
        RefreshUI();
    }

    public void Add(int inc)
    {
        count += Mathf.Max(0, inc);
        RefreshUI();

        if (count >= target)
            onAreaCompleted?.Invoke(this);
    }

    private void RefreshUI()
    {
        if (txtProgress) txtProgress.text = $"{Mathf.Clamp(count, 0, target)}/{target}";
        if (fillBar) fillBar.fillAmount = Mathf.InverseLerp(0, target, Mathf.Clamp(count, 0, target));
    }
}
