using UnityEngine;
using System;
/// <summary>
/// Zona CORTE para EXHIBICIÓN.
///
/// Flujo actual:
/// 1) FrutaMesa      → fruta entera sobre la mesa
/// 2) Cuchillo       → mitades con pepas (NO se retira bagazo)
/// 3) Fill/Film      → bandeja lista para exhibición
/// 4) Etiqueta       → se coloca la etiqueta del producto
/// 5) Supermarket    → producto final para vitrina
///
/// Usada en TUTORIAL (flujo de exhibición).
/// </summary>
public class FrutaPicadaCorteExhibicionController : MonoBehaviour
{
    [Header("Refs")]
    public FrutaPicadaActivity activity;
    public PapayaVisualState papayaVisual;

    [Header("Visual Etiqueta (Exhibición)")]
    [Tooltip("Etiqueta visual que se colocará sobre la bandeja de exhibición.")]
    public GameObject etiquetaExhibicion;

    [Header("Debug")]
    public bool zonaActiva;

    public event Action OnZonaCorteExhibicionCompletada;

    [Header("Instrucciones (solo Tutorial)")]
    [Tooltip("Se dispara cuando colocas la FRUTA en la mesa.")]
    public int I_FrutaMesa_OK = -1;

    [Tooltip("Se dispara cuando usas el CUCHILLO (mitades con pepas).")]
    public int I_Cuchillo_OK = -1;

    [Tooltip("Se dispara cuando aplicas el FILL/FILM (bandeja de exhibición).")]
    public int I_Fill_OK = -1;

    [Tooltip("Se dispara cuando colocas la ETIQUETA de exhibición.")]
    public int I_Etiqueta_OK = -1;

    [Tooltip("Se dispara cuando envías a 'SUPERMARKET' (paso final previo a vitrina).")]
    public int I_Supermarket_OK = -1;

    private bool pasoFruta;
    private bool pasoCuchillo;
    private bool pasoFill;
    private bool pasoEtiqueta;
    private bool pasoSupermarket;

    public void ResetZona()
    {
        pasoFruta = pasoCuchillo = pasoFill = pasoEtiqueta = pasoSupermarket = false;

        if (papayaVisual)
            papayaVisual.HideAll();

        if (etiquetaExhibicion)
            etiquetaExhibicion.SetActive(false);
    }

    public void SetZonaActiva(bool active)
    {
        zonaActiva = active;
        
        if (active)
        {
            FrutaUIButtonHighlight.HighlightOnly(FrutaAccionTipo.Corte_Exhibicion_Papaya);
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

    // ─────────────────────────────────────────────
    // PASO 1: FRUTA EN MESA
    // ─────────────────────────────────────────────
    public void TryColocarFrutaEnMesa()
    {
        if (!zonaActiva) return;

        if (!pasoFruta && !pasoCuchillo && !pasoFill && !pasoEtiqueta && !pasoSupermarket)
        {
            pasoFruta = true;

            if (papayaVisual)
                papayaVisual.ShowMesaEntera();

            PlaySuccess();
            
            if (activity != null)
                activity.PlayInstructionFromButton(I_FrutaMesa_OK, onlyInTutorial: true);
            
            FrutaUIButtonHighlight.HighlightOnly(FrutaAccionTipo.Corte_Exhibicion_Cuchillo);
        }
        else
        {
            PlayError();
        }
    }

    // ─────────────────────────────────────────────
    // PASO 2: CUCHILLO (MITADES CON PEPAS)
    // ─────────────────────────────────────────────
    public void TryUsarCuchillo()
    {
        if (!zonaActiva) return;

        if (pasoFruta && !pasoCuchillo && !pasoFill && !pasoEtiqueta && !pasoSupermarket)
        {
            pasoCuchillo = true;

            if (papayaVisual)
                papayaVisual.ShowMitadesConPepas(); // NO se retira bagazo en Exhibición

            PlaySuccess();

            if (activity != null)
                activity.PlayInstructionFromButton(I_Cuchillo_OK, onlyInTutorial: true);
            
            FrutaUIButtonHighlight.HighlightOnly(FrutaAccionTipo.Corte_Exhibicion_Film);
        }
        else
        {
            PlayError();
        }
    }

    // ─────────────────────────────────────────────
    // PASO 3: FILL / FILM (BANDEJA EXHIBICIÓN)
    // ─────────────────────────────────────────────
    public void TryAplicarFillFilm()
    {
        if (!zonaActiva) return;

        // Requiere fruta + cuchillo, aún sin etiqueta
        if (pasoFruta && pasoCuchillo && !pasoFill && !pasoEtiqueta && !pasoSupermarket)
        {
            pasoFill = true;

            if (papayaVisual)
                papayaVisual.ShowBandejaExhibicion();

            PlaySuccess();

            if (activity != null)
                activity.PlayInstructionFromButton(I_Fill_OK, onlyInTutorial: true);
            
            FrutaUIButtonHighlight.HighlightOnly(FrutaAccionTipo.Corte_Exhibicion_Etiqueta);
        }
        else
        {
            PlayError();
        }
    }

    // ─────────────────────────────────────────────
    // PASO 4: ETIQUETA (NUEVO PASO)
    // ─────────────────────────────────────────────
    public void TryAplicarEtiqueta()
    {
        if (!zonaActiva) return;

        // Solo después de tener la bandeja lista (fill) y antes de supermarket
        if (pasoFruta && pasoCuchillo && pasoFill && !pasoEtiqueta && !pasoSupermarket)
        {
            pasoEtiqueta = true;

            if (etiquetaExhibicion)
                etiquetaExhibicion.SetActive(true);

            PlaySuccess();

            if (activity != null)
                activity.PlayInstructionFromButton(I_Etiqueta_OK, onlyInTutorial: true);
            FrutaUIButtonHighlight.HighlightOnly(FrutaAccionTipo.Corte_Exhibicion_Supermarket);
        }
        else
        {
            PlayError();
        }
    }

    // ─────────────────────────────────────────────
    // PASO 5: SUPERMARKET (FINAL CORTE EXHIBICIÓN)
    // ─────────────────────────────────────────────
    public void TryEnviarASupermarket()
    {
        if (!zonaActiva) return;
        
        if (pasoFruta && pasoCuchillo && pasoFill && pasoEtiqueta && !pasoSupermarket)
        {
            pasoSupermarket = true;

            PlaySuccess();

            if (activity != null)
                activity.PlayInstructionFromButton(I_Supermarket_OK, onlyInTutorial: true);
            
            FrutaUIButtonHighlight.ClearAll();

            CompletarZona();
        }
        else
        {
            PlayError();
        }
    }

    void CompletarZona()
    {
        zonaActiva = false;
        OnZonaCorteExhibicionCompletada?.Invoke();
    }
}
