using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RevitalizacionActivity : PhasedActivityBasePro
{
    // ================== UI / HUD ==================
    [Header("UI")]
    public GameObject panelHUD;
    public GameObject panelResumen;
    public ReviHUD hud;

    // ================== Cámara (anchors opcionales) ==================
    [Header("Cámara (anchors opcionales)")]
    public string camRecorte = "Revita_Recorte";
    public string camRemojo = "Revita_Remojo";
    public string camEscurrido = "Revita_Escurrido";
    public string camFrio = "Revita_Frio";
    public string camVitrina = "Revita_Vitrina";

    // ================== Grupos por estación ==================
    [Header("Grupos por estación (root inactivos al inicio)")]
    public GameObject recorteGroup;
    public GameObject remojoGroup;
    public GameObject escurridoGroup;
    public GameObject frioGroup;
    public GameObject vitrinaGroup;

    [Header("Carruseles extra (Frío / Vitrina)")]
    public GameObject carruselFrio;
    public GameObject carruselVitrina;

    // ================== Roots que se arrastran por estación ==================
    [Header("Roots a arrastrar por estación (cada uno tiene su SimpleDragOnPlane)")]
    public Transform recorteMoveRoot;
    public Transform remojoMoveRoot;
    public Transform escurridoMoveRoot;
    public Transform frioMoveRoot;
    public Transform vitrinaMoveRoot;

    [Header("Posición inicial por estación (opcional)")]
    public Transform recorteStart;
    public Transform remojoStart;
    public Transform escurridoStart;
    public Transform frioStart;
    public Transform vitrinaStart;

    

    // ================== SnapZones por estación ==================
    [Header("SnapZones por estación")]
    public RevitaSnapZone snapJabaRecorte;        // Recorte: vegetal → jaba
    public RevitaSnapZone snapTinaRemojo;         // Remojo: jaba → tina
    public RevitaSnapZone snapRejillaEscurrido;   // Escurrido: jaba → rejilla

    // ================== Recorte (errores y jaba) ==================
    [Header("Recorte")]
    public List<GameObject> recorteErrores = new List<GameObject>(); // tallo + 3 hojas
    public GameObject jabaRecorte; // se activa cuando errores == 0

    // ================== Reset por ciclo ==================
    [Header("Reset por ciclo")]
    public List<RevitaResettable> resetables = new List<RevitaResettable>();

    // ================== Práctica / Summary – Opciones de flujo ==================
    [Header("Opciones de flujo")]
    [Tooltip("Si es true, al terminar el Tutorial se salta a Práctica y ésta se cierra inmediatamente (va directo a Summary).")]
    public bool skipPractice = true;

    [Tooltip("Si es true, el HUD se mantiene visible también durante el Summary.")]
    public bool keepHUDOnSummary = true;

    // ================== Timers por estación ==================
    [Header("Timers (segundos)")]
    public int remojoSeconds = 15;
    public int escurridoSeconds = 15;
    public int frioSeconds = 15;

    // ================== Índices de instrucciones ==================
    [Header("Índices de instrucciones")]
    public int I_TutorialIntro = 0; // Intro general
    public int I_Recorte = 1; // Quitar errores + a la jaba
    public int I_Remojo_In = 2; // Sumerge 15s
    public int I_Escurrido_In = 4; // 15s en rejilla
    public int I_Frio_In = 5; // 15s en refri
    public int I_Vitrina_In = 6; // Coloca en vitrina
    //public int I_Practica_CycleIntro = 7; // (no se usa si skipPractice = true)
    public int I_Summary = 8; // Cierre (opcional)

    // ================== Estado interno ==================
    private enum Estacion { Recorte, Remojo, Escurrido, Frio, Vitrina }
    private Estacion current;

    private SimpleDragOnPlane mover;

    bool _frioColocado;
    bool _vitrinaColocada;

    // ================== Requerido por ActivityBase ==================
    protected override void Initialize() { /* vacío a pedido */ }

    public override void StartActivity()
    {
        base.StartActivity();

        // Reset de instrucciones: indispensable para que suenen
        ResetInstructions();

        // Paneles base
        if (panelHUD) panelHUD.SetActive(false);
        if (panelResumen) panelResumen.SetActive(false);

        // Grupos OFF
        SetAllGroups(false);

        // Anchors (opcionales)
        camTutorialAnchor = camRecorte;
        camPracticeAnchor = camRecorte;  // no se usará si skipPractice=true
        camSummaryAnchor = camRecorte;

        StartPhasedActivity();
    }

    // ================== TUTORIAL ==================
    protected override void OnTutorialStart()
    {
        hud?.Show(true);

        if (!string.IsNullOrEmpty(camRecorte))
            cameraController?.MoveToPosition(camRecorte, null);

        UpdateInstructionOnce(I_TutorialIntro, StartTutorialFlow);
    }

    void StartTutorialFlow() => StartCoroutine(TutorialSequence());

    IEnumerator TutorialSequence()
    {
        // --- Recorte ---
        SetStation(Estacion.Recorte, false);
        UpdateInstructionOnce(I_Recorte);
        yield return WaitRecorteDone();

        // --- Remojo ---
        SetStation(Estacion.Remojo, false);
        UpdateInstructionOnce(I_Remojo_In);
        yield return WaitRemojoDone();

        // --- Escurrido ---
        SetStation(Estacion.Escurrido, false);
        UpdateInstructionOnce(I_Escurrido_In);
        yield return WaitEscurridoDone();

        // --- Frio ---
        SetStation(Estacion.Frio, false);
        UpdateInstructionOnce(I_Frio_In);
        yield return WaitFrioDone();

        // --- Vitrina ---
        SetStation(Estacion.Vitrina, false);
        UpdateInstructionOnce(I_Vitrina_In);
        yield return WaitVitrinaDone();

        // Termina Tutorial → pasa a Práctica (PhasePro)
        // Si skipPractice=true, la práctica se cerrará al instante en OnPracticeStart().
        CompleteTutorial_StartPractice();
    }

    // ================== PRÁCTICA ==================
    protected override void OnPracticeStart()
    {
        // Si queremos saltar la práctica, la cerramos de inmediato
        if (skipPractice)
        {
            // Opcional: dejar HUD visible ya desde aquí (lo pides “que se muestre el HUD”)
            if (panelHUD) panelHUD.SetActive(true);

            // No activamos grupos ni movemos cámara: cerramos práctica
            EndPractice();
            return;
        }

        /* (Si algún día quitas skipPractice, aquí iría tu flujo de práctica)
        if (panelHUD) panelHUD.SetActive(true);
        if (!string.IsNullOrEmpty(camRecorte))
            cameraController?.MoveToPosition(camRecorte, null);
        */
    }

    protected override void OnPracticeEnd()
    {
        // Apagamos grupos de estaciones
        SetAllGroups(false);

        // Mostrar Summary
        if (panelResumen) panelResumen.SetActive(true);

        // HUD: según preferencia
        if (keepHUDOnSummary)
        {
            if (panelHUD) panelHUD.SetActive(true); // mantenerlo a la vista
        }
        else
        {
            if (panelHUD) panelHUD.SetActive(false); // ocultarlo en summary
        }
    }

    // ================== SUMMARY ==================
    protected override void OnSummaryStart()
    {     
        if (I_Summary >= 0) UpdateInstructionOnce(I_Summary);
    }

    // ================== Helpers de estación / reset ==================
    void SetAllGroups(bool on)
    {
        if (recorteGroup) recorteGroup.SetActive(on);
        if (remojoGroup) remojoGroup.SetActive(on);
        if (escurridoGroup) escurridoGroup.SetActive(on);
        if (frioGroup) frioGroup.SetActive(on);
        if (vitrinaGroup) vitrinaGroup.SetActive(on);
    }

    void SetStation(Estacion est, bool isPractice)
    {
        current = est;
        SetAllGroups(false);
        SetAllCarouselsOff();
        mover = null;

        switch (est)
        {
            case Estacion.Recorte:
                if (!string.IsNullOrEmpty(camRecorte)) cameraController?.MoveToPosition(camRecorte);
                if (recorteGroup) recorteGroup.SetActive(true);
                hud?.SetStation("Recorte");
                ActivateMoveRoot(recorteMoveRoot, recorteStart);
                // Recorte entra BLOQUEADO: no se arrastra hasta limpiar errores
                ConfigureMover(allowX: false, allowZ: true, clampX: false, clampZ: true, locked: true);
                break;

            case Estacion.Remojo:
                if (!string.IsNullOrEmpty(camRemojo)) cameraController?.MoveToPosition(camRemojo);
                if (remojoGroup) remojoGroup.SetActive(true);
                hud?.SetStation("Remojo");
                ActivateMoveRoot(remojoMoveRoot, remojoStart);
                ConfigureMover(allowX: true, allowZ: true, clampX: false, clampZ: false, locked: false);
                break;

            case Estacion.Escurrido:
                if (!string.IsNullOrEmpty(camEscurrido)) cameraController?.MoveToPosition(camEscurrido);
                if (escurridoGroup) escurridoGroup.SetActive(true);
                hud?.SetStation("Escurrido");
                ActivateMoveRoot(escurridoMoveRoot, escurridoStart);
                ConfigureMover(allowX: true, allowZ: true, clampX: false, clampZ: false, locked: false);
                break;

            case Estacion.Frio:
                if (!string.IsNullOrEmpty(camFrio)) cameraController?.MoveToPosition(camFrio);
                if (frioGroup) frioGroup.SetActive(true);
                if (carruselFrio) carruselFrio.SetActive(true);
                hud?.SetStation("Refrigeración");
                break;

            case Estacion.Vitrina:
                if (!string.IsNullOrEmpty(camVitrina)) cameraController?.MoveToPosition(camVitrina);
                if (vitrinaGroup) vitrinaGroup.SetActive(true);
                if (carruselVitrina) carruselVitrina.SetActive(true);
                hud?.SetStation("Vitrina");
                break;
        }
    }

    void ActivateMoveRoot(Transform root, Transform start)
    {
        if (!root)
        {
            Debug.LogError($"[Revitalizacion] MoveRoot no asignado para {current}.");
            return;
        }

        if (!root.gameObject.activeSelf) root.gameObject.SetActive(true);

        if (start) root.SetPositionAndRotation(start.position, start.rotation);

        mover = root.GetComponent<SimpleDragOnPlane>();
        if (!mover) Debug.LogWarning($"[Revitalizacion] {root.name} no tiene SimpleDragOnPlane.");
    }

    void ConfigureMover(bool allowX, bool allowZ, bool clampX, bool clampZ, bool locked)
    {
        if (!mover) return;
        mover.allowX = allowX;
        mover.allowZ = allowZ;
        mover.clampX = clampX;
        mover.clampZ = clampZ;
        mover.lockWhileTimer = locked; // si true, SimpleDragOnPlane debe respetar este bloqueo en Update()
    }

    // ================== Esperas por estación ==================
    IEnumerator WaitRecorteDone()
    {
        // 1) esperar a que desaparezcan todos los errores
        while (!RecorteCleared()) yield return null;

        // ✅ liberar el drag AHORA
        if (mover) mover.lockWhileTimer = false;

        // 2) activar jaba y esperar snap a jaba
        if (jabaRecorte) jabaRecorte.SetActive(true);

        bool snapped = false;
        System.Action<GameObject> onSnap = _ => { snapped = true; };

        if (snapJabaRecorte) snapJabaRecorte.OnSnapped += onSnap;
        while (!snapped) yield return null;
        if (snapJabaRecorte) snapJabaRecorte.OnSnapped -= onSnap;
    }

    IEnumerator WaitRemojoDone()
    {
        bool snapped = false;
        System.Action<GameObject> onSnap = _ => { snapped = true; };

        if (snapTinaRemojo) snapTinaRemojo.OnSnapped += onSnap;
        while (!snapped) yield return null;
        if (snapTinaRemojo) snapTinaRemojo.OnSnapped -= onSnap;

        yield return TimerSeconds(remojoSeconds);
    }

    IEnumerator WaitEscurridoDone()
    {
        bool snapped = false;
        System.Action<GameObject> onSnap = _ => { snapped = true; };

        if (snapRejillaEscurrido) snapRejillaEscurrido.OnSnapped += onSnap;
        while (!snapped) yield return null;
        if (snapRejillaEscurrido) snapRejillaEscurrido.OnSnapped -= onSnap;

        yield return TimerSeconds(escurridoSeconds);
    }

    IEnumerator WaitFrioDone()
    {
        _frioColocado = false;

        while (!_frioColocado)
            yield return null;

        yield return TimerSeconds(frioSeconds);
    }
    IEnumerator WaitVitrinaDone()
    {
        _vitrinaColocada = false;

        while (!_vitrinaColocada)
            yield return null;

        yield break;
    }

    // ================== Timer genérico (HUD + lock drag) ==================
    IEnumerator TimerSeconds(int seconds)
    {
        if (mover) mover.lockWhileTimer = true;

        float t = seconds;
        hud?.ShowTimer(true);
        hud?.SetTimerSeconds(Mathf.CeilToInt(t));

        while (t > 0f)
        {
            t -= Time.deltaTime;
            hud?.SetTimerSeconds(Mathf.CeilToInt(Mathf.Max(0f, t)));
            yield return null;
        }

        hud?.ShowTimer(false);
        hud?.SetTimerSeconds(0);
        if (mover) mover.lockWhileTimer = false;
    }

    bool RecorteCleared()
    {
        for (int i = 0; i < recorteErrores.Count; i++)
            if (recorteErrores[i] && recorteErrores[i].activeSelf) return false;
        return true;
    }

    private void SetAllCarouselsOff()
    {
        if (carruselFrio) carruselFrio.SetActive(false);
        if (carruselVitrina) carruselVitrina.SetActive(false);
    }

    public void NotifyFrioColocadoDesdeUI()
    {
        _frioColocado = true;
    }

    public void NotifyVitrinaColocadaDesdeUI()
    {
        _vitrinaColocada = true;
    }
}
