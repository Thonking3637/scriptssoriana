using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PanelResumenPesaje : MonoBehaviour
{
    [Header("Referencias UI")]
    public TMP_Text titulo;
    public TMP_Text textoResumen;
    public TMP_Text textoProgreso;
    public Button btnContinuar;
    public Button btnReintentar;

    [Header("Referencias de control")]
    public PesajeMermaActivity actividad;

    private void Start()
    {
        if (btnContinuar)
            btnContinuar.onClick.AddListener(OnContinuarClicked);

        if (btnReintentar)
            btnReintentar.onClick.AddListener(OnReintentarClicked);
    }

    public void MostrarResumen(int completados, int total)
    {
        if (titulo) titulo.text = "¡Excelente trabajo!";
        if (textoResumen) textoResumen.text = "Has completado el pesaje correctamente.";
        if (textoProgreso) textoProgreso.text = $"Productos pesados: {completados} / {total}";

        var cg = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.interactable = true;
        cg.blocksRaycasts = true;

        gameObject.SetActive(true);

        if (btnContinuar) btnContinuar.interactable = true;
        if (btnReintentar) btnReintentar.interactable = true;
    }

    private void OnContinuarClicked()
    {
        if (actividad)
            actividad.ContinuarFase2();   // llamaremos este método
    }

    private void OnReintentarClicked()
    {
        if (actividad)
            actividad.ReiniciarPractica();
    }
}
