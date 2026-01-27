using UnityEngine;
using System;

/// <summary>
/// Zona de VITRINA.
/// Se completa cuando el producto final (bandeja o taper) se coloca
/// en el slot correspondiente.
/// 
/// Tu SnapZone / Trigger 3D debe llamar a OnProductoColocadoEnVitrina()
/// cuando el objeto final llega a su sitio.
/// </summary>
public class FrutaPicadaVitrinaController : MonoBehaviour
{
    [Header("Refs")]
    public FrutaPicadaActivity activity;

    [Header("Highlight opcional para slot vitrina")]
    public GameObject vitrinaHighlight;

    [Header("Debug")]
    public bool zonaActiva;

    public event Action OnZonaVitrinaCompletada;

    bool productoColocado;

    public void ResetZona()
    {
        productoColocado = false;
        if (vitrinaHighlight) vitrinaHighlight.SetActive(true);
    }

    public void SetZonaActiva(bool active)
    {
        zonaActiva = active;
        
        if (active)
        {
            FrutaUIButtonHighlight.HighlightOnly(FrutaAccionTipo.Vitrina_ProductoFinal);
        }
        else
        {
            FrutaUIButtonHighlight.ClearAll();
        }
    }

    void PlaySuccess()
    {
        SoundManager.Instance.PlaySound("success");
    }

    public void PlayError()
    {
        activity?.ReportError(1);
        SoundManager.Instance?.PlaySound("error");
    }

    /// <summary>
    /// Llama esto desde el SnapZone/Trigger de la vitrina
    /// cuando el producto final se ha colocado correctamente.
    /// </summary>
    public void OnProductoColocadoEnVitrina()
    {
        if (!zonaActiva) return;

        if (!productoColocado)
        {
            productoColocado = true;
            //if (vitrinaHighlight) vitrinaHighlight.SetActive(false);
            PlaySuccess();
            CompletarZona();
        }
        else
        {
            // si quisieras tratar doble intento como error, ir�a aqu�
        }
    }

    void CompletarZona()
    {
        zonaActiva = false;
        OnZonaVitrinaCompletada?.Invoke();
    }
}
