using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ClaseoResumenPanel : MonoBehaviour
{
    public PhasedActivityBasePro activity;
    public TMP_Text txtErrores;
    public Button btnContinuar;

    void OnEnable()
    {
        if (activity)
        {
            if (txtErrores) txtErrores.text = "Errores: " + activity.erroresTotales.ToString();
        }

        if (btnContinuar)
        {
            btnContinuar.onClick.RemoveAllListeners();
            btnContinuar.onClick.AddListener(() =>
            {
                if (activity) activity.CompleteActivity();
                else gameObject.SetActive(false);
            });
        }
    }
}
