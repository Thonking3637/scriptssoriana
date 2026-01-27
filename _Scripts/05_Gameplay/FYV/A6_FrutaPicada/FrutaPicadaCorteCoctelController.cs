using UnityEngine;
using System;
/// <summary>
/// Zona CORTE para COCTEL.
/// 
/// Flujo:
/// 1) FrutaMesa        → fruta entera sobre la mesa
/// 2) Cuchillo1        → pelar la fruta
/// 3) Cuchillo2        → cortar por la mitad
/// 4) Bagazo           → retirar pepas/bagazo
/// 5) Cuchillo3        → cortar en cubos (coctel)
/// 6) Taper            → colocar cubos en taper
/// 7) Etiqueta         → colocar etiqueta y completar zona.
/// 
/// Usada en PRÁCTICA.
/// </summary>
public class FrutaPicadaCorteCoctelController : MonoBehaviour
{
    [Header("Refs")]
    public FrutaPicadaActivity activity;
    public PapayaVisualState papayaVisual;

    [Header("Etiqueta visual opcional")]
    public GameObject etiquetaVisual; // child sobre el taper

    [Header("Debug")]
    public bool zonaActiva;

    public event Action OnZonaCorteCoctelCompletada;

    [Header("Instrucciones (por paso)")]
    [Tooltip("Se dispara cuando colocas la FRUTA en la mesa.")]
    public int I_FrutaMesa_OK = -1;

    [Tooltip("Se dispara al usar el CUCHILLO 1 (pelar).")]
    public int I_Cuchillo1_OK = -1;

    [Tooltip("Se dispara al usar el CUCHILLO 2 (cortar por la mitad).")]
    public int I_Cuchillo2_OK = -1;

    [Tooltip("Se dispara al retirar el BAGAZO / pepas.")]
    public int I_Bagazo_OK = -1;

    [Tooltip("Se dispara al usar el CUCHILLO 3 (corte en cubos).")]
    public int I_Cuchillo3_OK = -1;

    [Tooltip("Se dispara al colocar el TAPER con cubos.")]
    public int I_Taper_OK = -1;

    [Tooltip("Se dispara al colocar la ETIQUETA final.")]
    public int I_Etiqueta_OK = -1;

    bool pasoFruta;
    bool pasoCuchillo1;
    bool pasoCuchillo2;
    bool pasoBagazo;
    bool pasoCuchillo3;
    bool pasoTaper;
    bool pasoEtiqueta;

    public void ResetZona()
    {
        pasoFruta = pasoCuchillo1 = pasoCuchillo2 =
        pasoBagazo = pasoCuchillo3 = pasoTaper = pasoEtiqueta = false;

        if (papayaVisual)
            papayaVisual.HideAll();

        if (etiquetaVisual)
            etiquetaVisual.SetActive(false);
    }

    public void SetZonaActiva(bool active)
    {
        zonaActiva = active;
        
        if (active)
        {
            FrutaUIButtonHighlight.HighlightOnly(FrutaAccionTipo.Corte_Coctel_Papaya);
        }
        else
        {
            FrutaUIButtonHighlight.ClearAll();
        }
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

        if (!pasoFruta && !pasoCuchillo1 && !pasoCuchillo2 && !pasoBagazo && !pasoCuchillo3 && !pasoTaper &&
            !pasoEtiqueta)
        {
            pasoFruta = true;

            if (papayaVisual)
                papayaVisual.ShowMesaEntera();

            SoundManager.Instance.PlaySound("success");

        // Instrucción asociada a FRUTA MESA (en práctica también)
            if (activity != null)
                activity.PlayInstructionFromButton(I_FrutaMesa_OK, onlyInTutorial: false);
            FrutaUIButtonHighlight.HighlightOnly(FrutaAccionTipo.Corte_Coctel_Cuchillo1);
        }
        else
        {
            PlayError();
        }
    }

    // ─────────────────────────────────────────────
    // PASO 2: CUCHILLO 1 (PELAR)
    // ─────────────────────────────────────────────
    public void TryUsarCuchillo1()
    {
        if (!zonaActiva) return;

        if (pasoFruta && !pasoCuchillo1 && !pasoCuchillo2 && !pasoBagazo && !pasoCuchillo3 && !pasoTaper && !pasoEtiqueta)
        {
            pasoCuchillo1 = true;

            // Papaya pelada
            if (papayaVisual)
                papayaVisual.ShowPelada(); // asegúrate de tener este método en PapayaVisualState

            SoundManager.Instance.PlaySound("success");


            if (activity != null)
                activity.PlayInstructionFromButton(I_Cuchillo1_OK, onlyInTutorial: false);

            FrutaUIButtonHighlight.HighlightOnly(FrutaAccionTipo.Corte_Coctel_Cuchillo2);
        }
        else
        {
            PlayError();
        }
    }

    // ─────────────────────────────────────────────
    // PASO 3: CUCHILLO 2 (CORTAR POR LA MITAD)
    // ─────────────────────────────────────────────
    public void TryUsarCuchillo2()
    {
        if (!zonaActiva) return;

        if (pasoFruta && pasoCuchillo1 && !pasoCuchillo2 && !pasoBagazo && !pasoCuchillo3 && !pasoTaper && !pasoEtiqueta)
        {
            pasoCuchillo2 = true;

            // Mitades con pepas (antes de retirar bagazo)
            if (papayaVisual)
                papayaVisual.ShowMitadesConPepas();

            SoundManager.Instance.PlaySound("success");

            if (activity != null)
                activity.PlayInstructionFromButton(I_Cuchillo2_OK, onlyInTutorial: false);
            FrutaUIButtonHighlight.HighlightOnly(FrutaAccionTipo.Corte_Coctel_Bagazo);
        }
        else
        {
            PlayError();
        }
    }

    // ─────────────────────────────────────────────
    // PASO 4: RETIRAR BAGAZO / PEPAS
    // ─────────────────────────────────────────────
    public void TryRetirarBagazo()
    {
        if (!zonaActiva) return;

        if (pasoFruta && pasoCuchillo1 && pasoCuchillo2 && !pasoBagazo && !pasoCuchillo3 && !pasoTaper && !pasoEtiqueta)
        {
            pasoBagazo = true;

            if (papayaVisual)
                papayaVisual.ShowMitadesSinPepas();

            SoundManager.Instance.PlaySound("success");


            if (activity != null)
                activity.PlayInstructionFromButton(I_Bagazo_OK, onlyInTutorial: false);
            
            FrutaUIButtonHighlight.HighlightOnly(FrutaAccionTipo.Corte_Coctel_Cuchillo3);
        }
        else
        {
            PlayError();
        }
    }

    // ─────────────────────────────────────────────
    // PASO 5: CUCHILLO 3 (CUBOS EN MESA)
    // ─────────────────────────────────────────────
    public void TryUsarCuchillo3()
    {
        if (!zonaActiva) return;

        if (pasoFruta && pasoCuchillo1 && pasoCuchillo2 && pasoBagazo && !pasoCuchillo3 && !pasoTaper && !pasoEtiqueta)
        {
            pasoCuchillo3 = true;

            if (papayaVisual)
                papayaVisual.ShowCubosMesa();

            SoundManager.Instance.PlaySound("success");


            if (activity != null)
                activity.PlayInstructionFromButton(I_Cuchillo3_OK, onlyInTutorial: false);
            
            FrutaUIButtonHighlight.HighlightOnly(FrutaAccionTipo.Corte_Coctel_Taper);
        }
        else
        {
            PlayError();
        }
    }

    // ─────────────────────────────────────────────
    // PASO 6: TAPER (CUBOS DENTRO DEL TAPER)
    // ─────────────────────────────────────────────
    public void TryColocarTaper()
    {
        if (!zonaActiva) return;

        if (pasoFruta && pasoCuchillo1 && pasoCuchillo2 && pasoBagazo && pasoCuchillo3 && !pasoTaper && !pasoEtiqueta)
        {
            pasoTaper = true;

            if (papayaVisual)
                papayaVisual.ShowTaperCoctel();

            SoundManager.Instance.PlaySound("success");


            if (activity != null)
                activity.PlayInstructionFromButton(I_Taper_OK, onlyInTutorial: false);
            FrutaUIButtonHighlight.HighlightOnly(FrutaAccionTipo.Corte_Coctel_Etiqueta);
        }
        else
        {
            PlayError();
        }
    }

    // ─────────────────────────────────────────────
    // PASO 7: ETIQUETA (FINAL)
    // ─────────────────────────────────────────────
    public void TryAplicarEtiqueta()
    {
        if (!zonaActiva) return;

        if (pasoFruta && pasoCuchillo1 && pasoCuchillo2 && pasoBagazo && pasoCuchillo3 && pasoTaper && !pasoEtiqueta)
        {
            pasoEtiqueta = true;

            if (etiquetaVisual)
                etiquetaVisual.SetActive(true);

            SoundManager.Instance.PlaySound("success");


            if (activity != null)
                activity.PlayInstructionFromButton(I_Etiqueta_OK, onlyInTutorial: false);
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
        OnZonaCorteCoctelCompletada?.Invoke();
    }
}
