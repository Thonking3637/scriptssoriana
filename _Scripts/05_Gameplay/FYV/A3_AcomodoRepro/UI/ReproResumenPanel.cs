using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Panel de resumen para Acomodo Repro:
/// - Muestra SOLO: Total de Despachos y Errores Totales.
/// - Si la actividad (PhasedActivityBasePro) lleva el conteo global (RecordDespacho / RecordError),
///   usa esos campos. Si no, hace fallback sumando las lanes referenciadas.
/// </summary>
public class ReproResumenPanel : MonoBehaviour
{
    [Header("Actividad (usa m�tricas globales)")]
    [SerializeField] private PhasedActivityBasePro activity;

    [Header("Opcional (fallback): Lanes para sumar despachos si no usas RecordDespacho()")]
    [SerializeField] private ReproLaneFromUbicacion laneReprocesos;
    [SerializeField] private ReproLaneFromUbicacion laneReciclaje;
    [SerializeField] private ReproLaneFromUbicacion laneMallas;
    [SerializeField] private ReproLaneFromUbicacion laneCajas;

    [Header("UI")]
    [SerializeField] private TMP_Text txtTotalDespachos;
    [SerializeField] private TMP_Text txtErrores;
    [SerializeField] private Button btnContinuar;

    private void OnEnable()
    {
        Refresh();

        if (btnContinuar)
        {
            btnContinuar.onClick.RemoveListener(OnContinuar);
            btnContinuar.onClick.AddListener(OnContinuar);
        }
    }

    private void OnDisable()
    {
        if (btnContinuar) btnContinuar.onClick.RemoveListener(OnContinuar);
    }

    /// <summary>
    /// Actualiza los textos de Total de Despachos y Errores.
    /// </summary>
    public void Refresh()
    {
        // --- Total de Despachos ---
        int totalDespachos = 0;

        // 1) Preferimos el conteo global si la actividad lo lleva
        if (activity != null && activity.despachosTotales > 0)
        {
            totalDespachos = activity.despachosTotales;
        }
        else
        {
            // 2) Fallback: sumar de las lanes si no usas RecordDespacho()
            if (laneReprocesos) totalDespachos += laneReprocesos.Despachos;
            if (laneReciclaje) totalDespachos += laneReciclaje.Despachos;
            if (laneMallas) totalDespachos += laneMallas.Despachos;
            if (laneCajas) totalDespachos += laneCajas.Despachos;
        }

        if (txtTotalDespachos) txtTotalDespachos.text = "Despachos  " + totalDespachos.ToString();

        // --- Errores Totales ---
        //int totalErrores = 0;
        //if (activity != null) totalErrores = Mathf.Max(0, activity.erroresTotales);
        //if (txtErrores) txtErrores.text = "Errores  " + totalErrores.ToString();
    }

    private void OnContinuar()
    {
        // Cierra la actividad si est� disponible
        if (activity != null)
        {
            activity.CompleteActivity();
        }
        else
        {
            // Si no hay referencia a la activity, al menos ocultamos el panel
            gameObject.SetActive(false);
        }
    }
}
