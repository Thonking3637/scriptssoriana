using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel de resumen para FrutaPicadaActivity.
/// Proporciona el botón para finalizar la actividad y volver al flujo general.
/// </summary>
public class FrutaPicadaResumenPanel : MonoBehaviour
{
    [Header("Actividad Target")]
    [Tooltip("La actividad a la que se le llamará CompleteActivity()")]
    [SerializeField] private FrutaPicadaActivity activity;

    [Header("UI")]
    [SerializeField] private Button btnContinuar;

    private void Awake()
    {
        // Si no se asignó desde el Inspector, intenta encontrarla en escena
        if (activity == null)
            activity = FindObjectOfType<FrutaPicadaActivity>();
    }

    private void OnEnable()
    {
        if (btnContinuar)
        {
            // Evita listeners duplicados
            btnContinuar.onClick.RemoveListener(OnContinuar);
            btnContinuar.onClick.AddListener(OnContinuar);
        }
    }

    private void OnDisable()
    {
        if (btnContinuar)
        {
            btnContinuar.onClick.RemoveListener(OnContinuar);
        }
    }

    private void OnContinuar()
    {
        if (activity != null)
        {
            // Cierra la actividad y devuelve el control al ActivityLauncher / GameManager
            activity.CompleteActivity();
        }
        else
        {
            // Si por alguna razón no hay referencia, al menos oculta el panel
            gameObject.SetActive(false);
        }
    }
}