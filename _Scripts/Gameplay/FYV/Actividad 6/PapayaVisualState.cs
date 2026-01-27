using UnityEngine;

/// <summary>
/// Controla TODAS las variantes visuales de la papaya
/// repartidas en las diferentes zonas.
/// 
/// Importante: cada referencia puede estar en un lugar físico distinto:
/// - estadoLavadero: modelo ya posicionado en el lavadero.
/// - estadoEnJaba: modelo dentro de la jaba.
/// - etc.
/// </summary>
public class PapayaVisualState : MonoBehaviour
{
    [Header("Estados Visuales Papaya (cada uno en su zona física)")]
    public GameObject estadoLavadero;
    public GameObject estadoEnJaba;
    public GameObject estadoMesaEntera;
    public GameObject estadoMitadesConPepas;
    public GameObject estadoMitadesSinPepas;
    public GameObject estadoCubosMesa;
    public GameObject estadoTaperCoctel;
    public GameObject estadoBandejaExhibicion;
    public GameObject estadoEstante;
    public GameObject estadoPelada;

    private void Awake()
    {
        HideAll();
    }

    public void HideAll()
    {
        if (estadoLavadero) estadoLavadero.SetActive(false);
        if (estadoEnJaba) estadoEnJaba.SetActive(false);
        if (estadoMesaEntera) estadoMesaEntera.SetActive(false);
        if (estadoMitadesConPepas) estadoMitadesConPepas.SetActive(false);
        if (estadoMitadesSinPepas) estadoMitadesSinPepas.SetActive(false);
        if (estadoCubosMesa) estadoCubosMesa.SetActive(false);
        if (estadoTaperCoctel) estadoTaperCoctel.SetActive(false);
        if (estadoBandejaExhibicion) estadoBandejaExhibicion.SetActive(false);
        if (estadoPelada) estadoPelada.SetActive(false);
    }

    public void ShowLavadero()
    {
        HideAll();
        if (estadoLavadero) estadoLavadero.SetActive(true);
    }

    public void ShowEnJaba()
    {
        HideAll();
        if (estadoEnJaba) estadoEnJaba.SetActive(true);
    }

    public void ShowMesaEntera()
    {
        HideAll();
        if (estadoMesaEntera) estadoMesaEntera.SetActive(true);
    }

    public void ShowMitadesConPepas()
    {
        HideAll();
        if (estadoMitadesConPepas) estadoMitadesConPepas.SetActive(true);
    }

    public void ShowMitadesSinPepas()
    {
        HideAll();
        if (estadoMitadesSinPepas) estadoMitadesSinPepas.SetActive(true);
    }

    public void ShowCubosMesa()
    {
        HideAll();
        if (estadoCubosMesa) estadoCubosMesa.SetActive(true);
    }

    public void ShowTaperCoctel()
    {
        HideAll();
        if (estadoTaperCoctel) estadoTaperCoctel.SetActive(true);
    }

    public void ShowBandejaExhibicion()
    {
        HideAll();
        if (estadoBandejaExhibicion) estadoBandejaExhibicion.SetActive(true);
    }

    public void ShowExhibicion()
    {
        HideAll();
        if (estadoEstante) estadoEstante.SetActive(true);
    }

    public void ShowPelada()
    {
        HideAll();
        if(estadoPelada) estadoPelada.SetActive(true);
    }
}
