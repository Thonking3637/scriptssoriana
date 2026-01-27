using UnityEngine;
using System.Collections;

/// <summary>
/// Actividad FRUTA PICADA (Papaya).
/// 
/// - TUTORIAL: flujo de EXHIBICIÓN.
/// - PRÁCTICA: flujo de COCTEL (hasta N papayas).
/// 
/// Usa 4 zonas:
/// 1) Lavado
/// 2) Desinfección
/// 3) Corte (Exhibición en Tutorial / Coctel en Práctica)
/// 4) Vitrina (separada en dos controladores: ctrlVitrinaExhibicion / ctrlVitrinaCoctel)
/// </summary>
public class FrutaPicadaActivity : PhasedActivityBasePro
{
    [Header("UI")]
    public GameObject panelHUD;
    public GameObject panelCarruselAcciones;
    public GameObject panelResumen;

    [Header("Cámaras")]
    public string camLavado;
    public string camDesinfeccion;
    public string camCorte;
    public string camVitrina;
    
    public string camVitrinaExhibicion;
    public string camVitrinaCoctel;

    [Header("Zonas (control lógico)")]
    public FrutaPicadaLavadoController zonaLavado;
    public FrutaPicadaDesinfeccionController zonaDesinfeccion;
    public FrutaPicadaCorteExhibicionController zonaCorteExhibicion;
    public FrutaPicadaCorteCoctelController zonaCorteCoctel;
    public FrutaPicadaVitrinaController ctrlVitrinaExhibicion; // vitrina para EXHIBICIÓN
    public FrutaPicadaVitrinaController ctrlVitrinaCoctel;     // vitrina para COCTEL

    [Header("Roots visuales de zona (opcionales)")]
    public GameObject rootLavado;
    public GameObject rootDesinfeccion;
    public GameObject rootCorteCoctel;
    public GameObject rootCorteExhibicion;
    public GameObject rootVitrinaExhibicion;
    public GameObject rootVitrinaCoctel;

    [Header("Carruseles por zona")]
    public GameObject carruselLavado;
    public GameObject carruselDesinfeccion;
    public GameObject carruselCorteExhibicion;
    public GameObject carruselCorteCoctel;
    public GameObject carruselEstanteExhibicion;
    public GameObject carruselVitrinaExhibicion;
    public GameObject carruselVitrinaCoctel;

    [Header("Instrucciones Tutorial")]
    public int I_Lavado_Tutorial = 0;
    public int I_Desinfeccion_Tutorial = 1;
    public int I_CorteExhibicion_Tutorial = 2;
    public int I_Vitrina_Tutorial = 3;

    [Header("Instrucciones Práctica")]
    public int I_PracticaIntro = 4;
    
    [Header("Config práctica")]
    public int practicePapayas = 2;

    private bool lavadoDone;
    private bool desinfeccionDone;
    private bool corteDone;
    private bool vitrinaDone;

    private int currentPracticeIndex;
    
    /// <summary>
    /// Helper público para disparar instrucciones desde los controllers
    /// (Lavado, Desinfección, Corte, Vitrina).
    /// </summary>
    public void PlayInstructionFromButton(int instructionIndex, bool onlyInTutorial = true, System.Action onComplete = null)
    {
        if (instructionIndex < 0) return;

        if (onlyInTutorial && currentPhase != Phase.Tutorial)
            return;

        if (!onlyInTutorial && currentPhase != Phase.Practice)
            return;

        if (onComplete != null)
            UpdateInstructionOnce(instructionIndex, onComplete);
        else
            UpdateInstructionOnce(instructionIndex);
    }

    // ──────────────────────────────────────────────
    // INITIALIZE / START
    // ──────────────────────────────────────────────

    protected override void Initialize() {}
    public override void StartActivity()
    {
        base.StartActivity();

        autoStartOnEnable = false;
        usePracticeTimer = false;

        if (zonaLavado != null)
        {
            zonaLavado.activity = this;
            zonaLavado.OnZonaLavadoCompletada += OnLavadoCompletado;
        }

        if (zonaDesinfeccion != null)
        {
            zonaDesinfeccion.activity = this;
            zonaDesinfeccion.OnZonaDesinfeccionCompletada += OnDesinfeccionCompletada;
        }

        if (zonaCorteExhibicion != null)
        {
            zonaCorteExhibicion.activity = this;
            zonaCorteExhibicion.OnZonaCorteExhibicionCompletada += OnCorteCompletado;
        }

        if (zonaCorteCoctel != null)
        {
            zonaCorteCoctel.activity = this;
            zonaCorteCoctel.OnZonaCorteCoctelCompletada += OnCorteCompletado;
        }
        
        if (ctrlVitrinaExhibicion != null)
        {
            ctrlVitrinaExhibicion.activity = this;
            ctrlVitrinaExhibicion.OnZonaVitrinaCompletada += OnVitrinaCompletada;
        }

        if (ctrlVitrinaCoctel != null)
        {
            ctrlVitrinaCoctel.activity = this;
            ctrlVitrinaCoctel.OnZonaVitrinaCompletada += OnVitrinaCompletada;
        }

        if (panelHUD) panelHUD.SetActive(false);
        if (panelCarruselAcciones) panelCarruselAcciones.SetActive(false);
        if (panelResumen) panelResumen.SetActive(false);

        ShowZoneRoots(false, false, false, false, false);
        ResetZonas();

        StartPhasedActivity();
    }

    // ──────────────────────────────────────────────
    // TUTORIAL (EXHIBICIÓN)
    // ──────────────────────────────────────────────

    protected override void OnTutorialStart()
    {
        ResetZonas();

        if (panelHUD) panelHUD.SetActive(true);
        if (panelCarruselAcciones) panelCarruselAcciones.SetActive(true);
        if (panelResumen) panelResumen.SetActive(false);

        // Intro en dos pasos (respetando tu patrón)
        UpdateInstructionOnce(0, () =>
        {
            GoToTutorialStep(0);
            UpdateInstructionOnce(1);
        });
    }

    protected override void OnTutorialStep(int step)
    {
        switch (step)
        {
            case 0: // Lavado
                lavadoDone = desinfeccionDone = corteDone = vitrinaDone = false;

                ResetZonas();
                zonaLavado?.SetZonaActiva(false);
                zonaDesinfeccion?.SetZonaActiva(false);
                zonaCorteExhibicion?.SetZonaActiva(false);
                zonaCorteCoctel?.SetZonaActiva(false);
                ctrlVitrinaExhibicion?.SetZonaActiva(false);
                ctrlVitrinaCoctel?.SetZonaActiva(false);

                ShowZoneRoots(true, false, false, false, false);
                ShowCarrusel(carruselLavado);
                
                zonaLavado?.SetZonaActiva(true);
                cameraController?.MoveToPosition(camLavado, () =>
                {
                    UpdateInstructionOnce(I_Lavado_Tutorial);
                });
                break;

            case 1:
                zonaLavado?.SetZonaActiva(false);
                zonaDesinfeccion?.ResetZona();
                zonaDesinfeccion?.SetZonaActiva(false);

                ShowZoneRoots(false, true, false, false, false);
                ShowCarrusel(carruselDesinfeccion);
                
                zonaDesinfeccion?.SetZonaActiva(true);
                cameraController?.MoveToPosition(camDesinfeccion, () =>
                {
                    UpdateInstructionOnce(I_Desinfeccion_Tutorial);
                });
                break;

            case 2: // Corte Exhibición
                zonaDesinfeccion?.SetZonaActiva(false);
                zonaCorteExhibicion?.ResetZona();
                zonaCorteExhibicion?.SetZonaActiva(false);

                ShowZoneRoots(false, false, false, true, false);
                ShowCarrusel(carruselCorteExhibicion);
                
                zonaCorteExhibicion?.SetZonaActiva(true);
                cameraController?.MoveToPosition(camCorte, () =>
                {
                    UpdateInstructionOnce(I_CorteExhibicion_Tutorial);
                });
                break;

            case 3: // Vitrina EXHIBICIÓN
                
                vitrinaDone = false;
                zonaCorteExhibicion?.SetZonaActiva(false);
                ctrlVitrinaCoctel?.SetZonaActiva(false);
                ctrlVitrinaExhibicion?.ResetZona();
                ctrlVitrinaExhibicion?.SetZonaActiva(false);

                ShowZoneRoots(false, false, false, false, true);
                ShowCarrusel(carruselVitrinaExhibicion);
                
                ctrlVitrinaExhibicion?.SetZonaActiva(true);
                
                string camTargetTutorial = !string.IsNullOrEmpty(camVitrinaExhibicion) ? camVitrinaExhibicion : camVitrina;

                cameraController?.MoveToPosition(camTargetTutorial, () =>
                {
                    UpdateInstructionOnce(I_Vitrina_Tutorial);
                });
                break;
        }
    }

    private void OnLavadoCompletado()
    {
        lavadoDone = true;
        if (currentPhase == Phase.Tutorial)
            NextTutorialStep();   // 0 → 1
    }

    private void OnDesinfeccionCompletada()
    {
        desinfeccionDone = true;
        if (currentPhase == Phase.Tutorial)
            NextTutorialStep();   // 1 → 2
    }

    private void OnCorteCompletado()
    {
        corteDone = true;
        if (currentPhase == Phase.Tutorial)
            NextTutorialStep();   // 2 → 3
    }

    private void OnVitrinaCompletada()
    {
        vitrinaDone = true;

        if (currentPhase == Phase.Tutorial)
        {
            CompleteTutorial_StartPractice();
        }
    }

    // ──────────────────────────────────────────────
    // PRÁCTICA (COCTEL)
    // ──────────────────────────────────────────────

    protected override void OnPracticeStart()
    {
        if (panelHUD) panelHUD.SetActive(true);
        if (panelCarruselAcciones) panelCarruselAcciones.SetActive(true);
        if (panelResumen) panelResumen.SetActive(false);

        currentPracticeIndex = 0;
        StartCoroutine(PracticeRoutine());
    }

    private IEnumerator PracticeRoutine()
    {
        UpdateInstructionOnce(I_PracticaIntro);

        for (currentPracticeIndex = 0; currentPracticeIndex < practicePapayas; currentPracticeIndex++)
        {
            ResetZonas();
            lavadoDone = desinfeccionDone = corteDone = vitrinaDone = false;

            // LAVADO
            zonaLavado?.ResetZona();
            zonaLavado?.SetZonaActiva(true);

            zonaDesinfeccion?.SetZonaActiva(false);
            zonaCorteCoctel?.SetZonaActiva(false);
            zonaCorteExhibicion?.SetZonaActiva(false);
            ctrlVitrinaExhibicion?.SetZonaActiva(false);
            ctrlVitrinaCoctel?.SetZonaActiva(false);

            ShowZoneRoots(true, false, false, false, false);
            ShowCarrusel(carruselLavado);

            cameraController?.MoveToPosition(camLavado);

            yield return new WaitUntil(() => lavadoDone);

            // DESINFECCIÓN
            zonaLavado?.SetZonaActiva(false);
            zonaDesinfeccion?.ResetZona();
            zonaDesinfeccion?.SetZonaActiva(true);

            ShowZoneRoots(false, true, false, false, false);
            ShowCarrusel(carruselDesinfeccion);

            cameraController?.MoveToPosition(camDesinfeccion);

            yield return new WaitUntil(() => desinfeccionDone);

            // CORTE COCTEL
            zonaDesinfeccion?.SetZonaActiva(false);
            zonaCorteCoctel?.ResetZona();
            zonaCorteCoctel?.SetZonaActiva(true);

            ShowZoneRoots(false, false, true, false, false);
            ShowCarrusel(carruselCorteCoctel);
            
            zonaCorteCoctel?.SetZonaActiva(true);
            cameraController?.MoveToPosition(camCorte, () =>
            {
                //UpdateInstructionOnce(I_CorteCoctel_Practica);
            });

            yield return new WaitUntil(() => corteDone);

            // VITRINA COCTEL
            zonaCorteCoctel?.SetZonaActiva(false);

            vitrinaDone = false;

            ctrlVitrinaExhibicion?.SetZonaActiva(false);
            ctrlVitrinaCoctel?.ResetZona();
            ctrlVitrinaCoctel?.SetZonaActiva(true);

            ShowZoneRoots(false, false, false, false, true);
            ShowCarrusel(carruselVitrinaCoctel);
            
            ctrlVitrinaCoctel?.SetZonaActiva(true);

            string camTargetPractice = !string.IsNullOrEmpty(camVitrinaCoctel) ? camVitrinaCoctel : camVitrina;

            cameraController?.MoveToPosition(camTargetPractice, () =>
            {
                //UpdateInstructionOnce(I_Vitrina_Practica);
            });

            yield return new WaitUntil(() => vitrinaDone);
            
            ReportDespacho(1);
        }

        EndPractice();
    }

    // ──────────────────────────────────────────────
    // RESUMEN
    // ──────────────────────────────────────────────

    protected override void OnSummaryStart()
    {
        if (panelHUD) panelHUD.SetActive(false);
        if (panelCarruselAcciones) panelCarruselAcciones.SetActive(false);
        if (panelResumen) panelResumen.SetActive(true);
        SoundManager.Instance.PlaySound("win");
        UpdateInstructionOnce(23);
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    private void ResetZonas()
    {
        zonaLavado?.ResetZona();
        zonaLavado?.SetZonaActiva(false);

        zonaDesinfeccion?.ResetZona();
        zonaDesinfeccion?.SetZonaActiva(false);

        zonaCorteExhibicion?.ResetZona();
        zonaCorteExhibicion?.SetZonaActiva(false);

        zonaCorteCoctel?.ResetZona();
        zonaCorteCoctel?.SetZonaActiva(false);

        ctrlVitrinaExhibicion?.ResetZona();
        ctrlVitrinaExhibicion?.SetZonaActiva(false);

        ctrlVitrinaCoctel?.ResetZona();
        ctrlVitrinaCoctel?.SetZonaActiva(false);
    }

    private void ShowZoneRoots(bool showLavado, bool showDesinf, bool showCorteCoctel, bool showCorteExhibicion, bool showVitrina)
    {
        if (rootLavado) rootLavado.SetActive(showLavado);
        if (rootDesinfeccion) rootDesinfeccion.SetActive(showDesinf);
        if (rootCorteCoctel) rootCorteCoctel.SetActive(showCorteCoctel);
        if (rootCorteExhibicion) rootCorteExhibicion.SetActive(showCorteExhibicion);
        
        if (rootVitrinaExhibicion)
            rootVitrinaExhibicion.SetActive(showVitrina && currentPhase == Phase.Tutorial);

        if (rootVitrinaCoctel)
            rootVitrinaCoctel.SetActive(showVitrina && currentPhase == Phase.Practice);
    }

    private void ShowCarrusel(GameObject target, bool isVitrina = false)
    {
        if (!panelCarruselAcciones) return;
        
        panelCarruselAcciones.SetActive(target != null);
        
        GameObject[] all =
        {
            carruselLavado,
            carruselDesinfeccion,
            carruselCorteExhibicion,
            carruselCorteCoctel,
            carruselEstanteExhibicion,
            carruselVitrinaExhibicion,
            carruselVitrinaCoctel
        };
        
        foreach (var c in all)
        {
            if (!c) continue;
            c.SetActive(c == target);
        }
    }
}
