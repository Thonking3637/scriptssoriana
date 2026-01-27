using UnityEngine;
using TMPro;

public class ClaseoHUDErrors : MonoBehaviour
{
    [Header("Refs")]
    public PhasedActivityBasePro activity;   // arrastra tu ClaseoActivity (o se autoencuentra)
    public TMP_Text txtErrores;              // “Errores: 0” en el HUD
    [SerializeField] string prefix = "";

    void OnEnable()
    {
        if (!activity) activity = FindObjectOfType<PhasedActivityBasePro>();
        ClaseoEvents.OnErrorDrop += HandleErrorDrop;
        Refresh();
    }

    void OnDisable()
    {
        ClaseoEvents.OnErrorDrop -= HandleErrorDrop;
    }

    void HandleErrorDrop() => Refresh();

    public void Refresh()
    {
        if (txtErrores && activity)
            txtErrores.text = prefix + activity.erroresTotales.ToString();
    }
}
