using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PanelResumenUbicacion : MonoBehaviour
{
    [Header("Referencias UI")]
    public TMP_Text titulo;
    public TMP_Text textoResumen;
    public TMP_Text textoProgreso;
    public Button btnContinuar;
    public Button btnReintentar;

    [Header("Control")]
    public PesajeMermaActivity actividad;

    private void Start()
    {
        if (btnContinuar) btnContinuar.onClick.AddListener(OnContinuarClicked);
        if (btnReintentar) btnReintentar.onClick.AddListener(OnReintentarClicked);
    }

    public void MostrarResumen(int colocadas, int total)
    {
        if (titulo) titulo.text = "Ubicación completada";
        if (textoResumen) textoResumen.text = "Has colocado cada jaba en su área correspondiente.";
        if (textoProgreso) textoProgreso.text = $"Jabas colocadas: {colocadas} / {total}";
        gameObject.SetActive(true);
    }

    private void OnContinuarClicked()
    {
        if (actividad) actividad.ContinuarFase3();
    }

    private void OnReintentarClicked()
    {
        if (actividad) actividad.ReiniciarUbicacion();
    }
}
