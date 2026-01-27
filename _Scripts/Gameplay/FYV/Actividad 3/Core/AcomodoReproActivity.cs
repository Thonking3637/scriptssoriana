using UnityEngine;

public class AcomodoReproActivity : PhasedActivityBasePro
{
    [Header("UI")]
    public GameObject panelHUD;
    public GameObject panelResumen;
    public GameObject panelCarruselUbicacion; // HUD del conveyor (Scroll + Content)
    public TMPro.TextMeshProUGUI tmpTimer;

    [Header("Spawner de botones")]
    public ReproSpawnerUI spawner;

    [Header("Lanes / Zonas")]
    public ReproLaneFromUbicacion laneReprocesos;
    public ReproLaneFromUbicacion laneReciclaje;
    public ReproLaneFromUbicacion laneMallas;
    public ReproLaneFromUbicacion laneCajas;

    [Header("Cámara")]
    public string camVistaGeneral;
    public string camVistaZonas;

    [Header("Conteos")]
    public int errores = 0;

    // ========= Wiring de eventos =========
    protected override void OnEnable()
    {
        UIDragToWorldRepro.OnDropUIButton += OnDropUIButton;

        if (laneReprocesos) { laneReprocesos.OnReachTarget += OnLaneReachTarget_Tutorial; laneReprocesos.OnProcesado += OnLaneProcesado; }
        if (laneReciclaje) { laneReciclaje.OnReachTarget += OnLaneReachTarget_Tutorial; laneReciclaje.OnProcesado += OnLaneProcesado; }
        if (laneMallas) { laneMallas.OnReachTarget += OnLaneReachTarget_Tutorial; laneMallas.OnProcesado += OnLaneProcesado; }
        if (laneCajas) { laneCajas.OnReachTarget += OnLaneReachTarget_Tutorial; laneCajas.OnProcesado += OnLaneProcesado; }
    }

    protected override void OnDisable()
    {
        UIDragToWorldRepro.OnDropUIButton -= OnDropUIButton;

        if (laneReprocesos) { laneReprocesos.OnReachTarget -= OnLaneReachTarget_Tutorial; laneReprocesos.OnProcesado -= OnLaneProcesado; }
        if (laneReciclaje) { laneReciclaje.OnReachTarget -= OnLaneReachTarget_Tutorial; laneReciclaje.OnProcesado -= OnLaneProcesado; }
        if (laneMallas) { laneMallas.OnReachTarget -= OnLaneReachTarget_Tutorial; laneMallas.OnProcesado -= OnLaneProcesado; }
        if (laneCajas) { laneCajas.OnReachTarget -= OnLaneReachTarget_Tutorial; laneCajas.OnProcesado -= OnLaneProcesado; }
    }

    // ========= Entrada de la actividad =========
    public override void StartActivity()
    {
        base.StartActivity();

        // Configuración de fases (heredada)
        autoStartOnEnable = false;
        usePracticeTimer = true;
        practiceDurationSeconds = 180;

        panelResumen?.SetActive(false);
        panelHUD?.SetActive(false);
        panelCarruselUbicacion?.SetActive(false);
        if (tmpTimer) tmpTimer.gameObject.SetActive(false);

        // Arranca el flujo por fases desde Tutorial
        StartPhasedActivity();
    }

    // ========= Tutorial =========
    protected override void OnTutorialStart()
    {
        // Paso 0
        GoToTutorialStep(0);
    }

    protected override void OnTutorialStep(int step)
    {
        switch (step)
        {
            case 0:
                // Cámara a vista general → instrucción 0 → pasar
                cameraController?.MoveToPosition(camVistaGeneral, () =>
                {
                    UpdateInstructionOnce(0, () => { NextTutorialStep(); });
                });
                break;

            case 1:
                // Cámara a zonas → instrucción 1 → pasar
                cameraController?.MoveToPosition(camVistaZonas, () =>
                {
                    UpdateInstructionOnce(1, () => { NextTutorialStep(); });
                });
                break;

            case 2:
                // Instrucción 2 → habilitar carrusel + HUD → preparar spawner (solo Reprocesos)
                UpdateInstructionOnce(2, () =>
                {
                    panelCarruselUbicacion?.SetActive(true);
                    panelHUD?.SetActive(true);
                    if (tmpTimer) tmpTimer.gameObject.SetActive(false); // sin timer en tutorial

                    if (spawner != null)
                    {
                        spawner.ClearAll();
                        spawner.SetWhitelist(JabaTipo.Reprocesos); // Solo Reprocesos en tutorial
                        spawner.enableAutoSpawn = true;
                        spawner.oldestOnLeft = true;
                        spawner.SetSpawnBudget(6); // o 6, como prefieras
                        spawner.ResetAllAndUsed();
                    }
                });
                break;

            case 3:
                // Al alcanzar altura objetivo (lo dispara OnLaneReachTarget_Tutorial) → dar instrucción 3/4
                UpdateInstructionOnce(3, () =>
                {
                    UpdateInstructionOnce(4); // “Presiona PROCESAR para continuar”
                });
                break;

            case 4:
                // Al PROCESAR (lo dispara OnLaneProcesado) → instrucción 5 → pasar a práctica
                UpdateInstructionOnce(5, () =>
                {
                    CompleteTutorial_StartPractice();
                });
                break;
        }
    }

    // Disparado por la lane cuando llega a objetivo durante el tutorial
    private void OnLaneReachTarget_Tutorial(ReproLaneFromUbicacion lane)
    {
        if (currentPhase != Phase.Tutorial) return;
        // Solo avanzar a ese step si todavía no llegamos
        if (tutorialStep < 3) GoToTutorialStep(3);
    }

    // Disparado al presionar PROCESAR en tutorial
    private void OnLaneProcesado(ReproLaneFromUbicacion lane)
    {
        if (currentPhase != Phase.Tutorial) return;
        if (tutorialStep < 4) GoToTutorialStep(4);
    }

    // ========= Práctica =========
    protected override void OnPracticeStart()
    {
        if (panelCarruselUbicacion) panelCarruselUbicacion.SetActive(true);
        if (panelHUD) panelHUD.SetActive(true);
        if (tmpTimer) tmpTimer.gameObject.SetActive(true);

        if (spawner != null)
        {
            spawner.ClearWhitelist();     // 4 tipos
            spawner.ClearSpawnBudget();   // sin límite en práctica
            spawner.enableAutoSpawn = true;
            spawner.oldestOnLeft = true;
            spawner.ResetAllAndUsed();
        }
    }

    protected override void OnPracticeTimerTick(int secondsLeft)
    {
        if (tmpTimer) tmpTimer.text = $"{secondsLeft / 60:00}:{secondsLeft % 60:00}";
    }

    protected override void OnPracticeEnd()
    {
        if (tmpTimer) tmpTimer.gameObject.SetActive(false);
        if (panelCarruselUbicacion) panelCarruselUbicacion.SetActive(false);
        if (panelHUD) panelHUD.SetActive(false);
    }

    // ========= Summary =========
    protected override void OnSummaryStart()
    {
        if (panelResumen) panelResumen.SetActive(true);

        UpdateInstructionOnce(6);

        if (musicSummary) SoundManager.Instance?.CrossfadeMusic(musicSummary, 0.5f, 0.8f, musicSummaryVol, true);
        else SoundManager.Instance?.CrossfadeToPrevious(0.5f, 0.6f);

    }

    // ========= Drops correctos / incorrectos (SFX + métricas) =========
    private void OnDropUIButton(JabaTipo tipo, GameObject prefab, bool valido)
    {
        if (valido)
        {
            RecordSuccess();
            try { SoundManager.Instance.PlaySound("success"); } catch { }
        }
        else
        {
            errores++;
            RecordError();
            try { SoundManager.Instance.PlaySound("error"); } catch { }
            try { ReproEvents.OnErrorDrop?.Invoke(); } catch { }
        }
    }

    protected override void Initialize()
    {
        throw new System.NotImplementedException();
    }
}
