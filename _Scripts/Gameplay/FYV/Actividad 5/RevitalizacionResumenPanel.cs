// RevitalizacionResumenPanel.cs
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel de resumen para RevitalizacionActivity.
/// Proporciona el bot�n para completar la actividad.
/// </summary>
public class RevitalizacionResumenPanel : MonoBehaviour
{
    [Header("Actividad Target")]
    [Tooltip("La actividad a la que se le llamar� CompleteActivity()")]
    [SerializeField] private RevitalizacionActivity activity;

    [Header("UI")]
    [SerializeField] private Button btnContinuar;
    
    private void Awake()
    {
        // Si no se asign�, intenta buscarla en la escena
        if (activity == null)
            activity = FindObjectOfType<RevitalizacionActivity>();
    }

    private void OnEnable()
    {
        if (btnContinuar)
        {
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
            activity.CompleteActivity();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}