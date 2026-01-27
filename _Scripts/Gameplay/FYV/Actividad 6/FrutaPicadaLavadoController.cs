using UnityEngine;
using System;
/// <summary>
/// Zona de LAVADO.
/// Flujo NUEVO: Agua → Desinfectante → Papaya → Completar.
/// 
/// Los botones del carrusel llaman a:
/// - TryAplicarAgua()
/// - TryAplicarDesinfectante()
/// - TryColocarPapayaLavadero()
/// </summary>
public class FrutaPicadaLavadoController : MonoBehaviour
{
    [Header("Refs")]
    public FrutaPicadaActivity activity;
    public PapayaVisualState papayaVisual;

    [Header("Objetos visuales de agua")]
    public GameObject aguaVisual1;
    public GameObject aguaVisual2;
    public GameObject aguaVisualDesinfectante;

    [Header("Debug")]
    public bool zonaActiva;

    public event Action OnZonaLavadoCompletada;

    [Header("Instrucciones (solo Tutorial)")]
    public int I_Agua_OK = -1;
    public int I_Desinfect_OK = -1;
    public int I_Papaya_OK = -1;
    public int I_Timer_OK = -1;

    private bool pasoAgua;
    private bool pasoDesinfectante;
    private bool pasoPapaya;

    public void ResetZona()
    {
        pasoAgua = pasoDesinfectante = pasoPapaya = false;

        if (aguaVisual1) aguaVisual1.SetActive(false);
        if (aguaVisual2) aguaVisual2.SetActive(false);
        if (aguaVisualDesinfectante) aguaVisualDesinfectante.SetActive(false);

        if (papayaVisual) papayaVisual.HideAll();
    }

    public void SetZonaActiva(bool active)
    {
        zonaActiva = active;
        
        if (active)
        {
            FrutaUIButtonHighlight.HighlightOnly(FrutaAccionTipo.Lavado_Agua);
        }
        else
        {
            FrutaUIButtonHighlight.ClearAll();
        }
    }

    public void PlaySuccess()
    {
        SoundManager.Instance.PlaySound("success");
    }

    public void PlayError()
    {
        activity?.ReportError(1);
        SoundManager.Instance?.PlaySound("error");
    }

    //──────────────────────────────────────────────────────────────
    // PASO 1: AGUA
    //──────────────────────────────────────────────────────────────
    public void TryAplicarAgua()
    {
        if (!zonaActiva) return;
        
        if (!pasoAgua && !pasoDesinfectante && !pasoPapaya)
        {
            pasoAgua = true;

            if (aguaVisual1) aguaVisual1.SetActive(true);
            if (aguaVisual2) aguaVisual2.SetActive(true);
            if (aguaVisualDesinfectante) aguaVisualDesinfectante.SetActive(false);

            PlaySuccess();
            activity?.PlayInstructionFromButton(I_Agua_OK, onlyInTutorial: true);
            FrutaUIButtonHighlight.HighlightOnly(FrutaAccionTipo.Lavado_Desinfectante);
        }
        else
        {
            PlayError();
        }
    }

    //──────────────────────────────────────────────────────────────
    // PASO 2: DESINFECTANTE (AHORA VA DESPUÉS DEL AGUA)
    //──────────────────────────────────────────────────────────────
    public void TryAplicarDesinfectante()
    {
        if (!zonaActiva) return;
        
        if (pasoAgua && !pasoDesinfectante && !pasoPapaya)
        {
            pasoDesinfectante = true;
            
            if (aguaVisual1) aguaVisual1.SetActive(false);
            if (aguaVisual2) aguaVisual2.SetActive(false);
            if (aguaVisualDesinfectante) aguaVisualDesinfectante.SetActive(true);

            PlaySuccess();
            activity?.PlayInstructionFromButton(I_Desinfect_OK, onlyInTutorial: true);
            FrutaUIButtonHighlight.HighlightOnly(FrutaAccionTipo.Lavado_Papaya);
        }
        else
        {
            PlayError();
        }
    }

    //──────────────────────────────────────────────────────────────
    // PASO 3: PAPAYA (AL FINAL)
    //──────────────────────────────────────────────────────────────
    public void TryColocarPapayaLavadero()
    {
        if (!zonaActiva) return;
        
        if (pasoAgua && pasoDesinfectante && !pasoPapaya)
        {
            pasoPapaya = true;

            if (papayaVisual) papayaVisual.ShowLavadero();

            PlaySuccess();
            activity?.PlayInstructionFromButton(I_Papaya_OK, onlyInTutorial: true);
            FrutaUIButtonHighlight.ClearAll();
            
            CompletarZona();
        }
        else
        {
            PlayError();
        }
    }

    //──────────────────────────────────────────────────────────────
    // FINALIZAR ZONA
    //──────────────────────────────────────────────────────────────
    void CompletarZona()
    {
        zonaActiva = false;

        // Instrucción opcional final
        if (activity != null)
            activity.PlayInstructionFromButton(I_Timer_OK, onlyInTutorial: true);

        OnZonaLavadoCompletada?.Invoke();
    }
}
