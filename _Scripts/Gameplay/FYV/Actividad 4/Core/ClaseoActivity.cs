using UnityEngine;
using TMPro;
using System.Collections;

public class ClaseoActivity : PhasedActivityBasePro
{
    [Header("UI")]
    public GameObject panelHUD;
    public GameObject panelCarrusel;
    public GameObject panelResumen;
    public TMP_Text tmpTimer; // opcional

    [Header("Spawner (Claseo)")]
    public ClaseoSpawnerUI spawner;

    [Header("Bloques de producto (cada uno tiene 3 áreas)")]
    public GameObject bloquePapaya;
    public GameObject bloquePlatano;
    public GameObject bloqueAjitomate;

    public ClaseoBlockController ctrPapaya;
    public ClaseoBlockController ctrPlatano;
    public ClaseoBlockController ctrAjitomate;

    [Header("Cámaras")]
    public string camPapaya;
    public string camPlatano;
    public string camAjitomate;

    [Header("Meta por área (jaba)")]
    public int targetPorArea = 8;

    [Header("Anchors por bloque (a DÓNDE mover el plano compartido)")]
    // Solo tutorial
    public Transform shelfAnchorPapaya;
    public Collider shelfBoundsPapaya;
    // Práctica 1
    public Transform shelfAnchorPlatano;
    public Collider shelfBoundsPlatano;
    // Práctica 2
    public Transform shelfAnchorAjitomate;
    public Collider shelfBoundsAjitomate;

    // 1 = Platano, 2 = Ajitomate (Papaya solo tutorial)
    private int bloqueIndex = 0;

    public override void StartActivity()
    {
        base.StartActivity();
        autoStartOnEnable = false;
        usePracticeTimer = false;

        if (panelHUD) panelHUD.SetActive(false);
        if (panelCarrusel) panelCarrusel.SetActive(false);
        if (panelResumen) panelResumen.SetActive(false);
        if (tmpTimer) tmpTimer.gameObject.SetActive(false);

        // Callbacks de fin de bloque
        if (ctrPapaya) ctrPapaya.onBlockCompleted = OnBlockPapayaCompleted;
        if (ctrPlatano) ctrPlatano.onBlockCompleted = OnBlockPlatanoCompleted;
        if (ctrAjitomate) ctrAjitomate.onBlockCompleted = OnBlockAjitomateCompleted;

        StartPhasedActivity();
    }

    // =================== TUTORIAL ===================
    protected override void OnTutorialStart()
    {
        GoToTutorialStep(0);
    }
    
    protected override void OnTutorialStep(int step)
    {
        switch (step)
        {
            case 0:
                cameraController?.MoveToPosition(camPapaya, () =>
                {
                    UpdateInstructionOnce(0, NextTutorialStep);
                });
                break;

            case 1:
                if (bloquePapaya) bloquePapaya.SetActive(true);
                if (bloquePlatano) bloquePlatano.SetActive(false);
                if (bloqueAjitomate) bloqueAjitomate.SetActive(false);

                ctrPapaya?.ResetAllAreas(targetPorArea);

                ApplyShelfToSpawner(shelfAnchorPapaya, shelfBoundsPapaya);

                UpdateInstructionOnce(1, NextTutorialStep);
                break;

            case 2:
                if (panelHUD) panelHUD.SetActive(true);
                if (panelCarrusel) panelCarrusel.SetActive(true);

                if (spawner)
                {
                    spawner.ClearAll();
                    spawner.SpawnExactBalanced(JabaTipo.Papaya, targetPorArea, true);
                }

                UpdateInstructionOnce(2);
                break;

            case 3:
                UpdateInstructionOnce(3, CompleteTutorial_StartPractice);
                break;
        }
    }

    private void OnBlockPapayaCompleted()
    {
        if (currentPhase == Phase.Tutorial)
        {
            if (tutorialStep < 3) GoToTutorialStep(3);
        }
    }

    // =================== PRÁCTICA (SOLO PLÁTANO → AJITOMATE) ===================
    protected override void OnPracticeStart()
    {
        bloqueIndex = 1;
        erroresTotales = 0;
        if (panelHUD) panelHUD.SetActive(true);
        if (panelCarrusel) panelCarrusel.SetActive(true);
        if (tmpTimer && usePracticeTimer) tmpTimer.gameObject.SetActive(true);

        StartBlockPlatano();
    }

    private void StartBlockPlatano()
    {
        if (bloquePapaya) bloquePapaya.SetActive(false);
        if (bloquePlatano) bloquePlatano.SetActive(true);
        if (bloqueAjitomate) bloqueAjitomate.SetActive(false);

        ctrPlatano?.ResetAllAreas(targetPorArea);

        cameraController?.MoveToPosition(camPlatano, null);

        // ⬅ mueve el PLANO COMPARTIDO a la pose del bloque Plátano
        ApplyShelfToSpawner(shelfAnchorPlatano, shelfBoundsPlatano);

        if (spawner)
        {
            spawner.ClearAll();
            spawner.SpawnExactBalanced(JabaTipo.Platano, targetPorArea, true);
        }
    }

    private void StartBlockAjitomate()
    {
        if (bloquePapaya) bloquePapaya.SetActive(false);
        if (bloquePlatano) bloquePlatano.SetActive(false);
        if (bloqueAjitomate) bloqueAjitomate.SetActive(true);

        ctrAjitomate?.ResetAllAreas(targetPorArea);

        cameraController?.MoveToPosition(camAjitomate, null);
        Debug.Log($"🟣 StartBlockAjitomate() llamado en frame {Time.frameCount}");
        // ⬅ mueve el PLANO COMPARTIDO a la pose del bloque Ajitomate
        ApplyShelfToSpawner(shelfAnchorAjitomate, shelfBoundsAjitomate);

        if (spawner)
        {
            spawner.ClearAll();
            spawner.SpawnExactBalancedDeterministic(JabaTipo.Ajitomate, targetPorArea, true);

            // 👇 Nuevo fix
            StartCoroutine(VerifyAjitomateSpawn());
        }
    }

    private IEnumerator VerifyAjitomateSpawn()
    {
        yield return new WaitForSeconds(0.25f);
        if (!spawner || !spawner.content) yield break;

        int total = spawner.content.childCount;
        Debug.Log($"[ClaseoActivity] Verificación Ajitomate: {total} hijos en content (esperado ~25 con template).");

        // Si por alguna razón hay 24 (template + 23 válidos), forzamos 1 más:
        if (total < 25)
        {
            Debug.LogWarning("[ClaseoActivity] Ajitomate: faltó 1 botón, regenerando el faltante de Maduro.");
            spawner.SpawnExactBalancedDeterministic(JabaTipo.Ajitomate, 1);
        }
    }

    private void OnBlockPlatanoCompleted()
    {
        if (currentPhase != Phase.Practice || bloqueIndex != 1) return;

        bloqueIndex = 2;
        try { SoundManager.Instance.PlaySound("fase_fin"); } catch { }
        StartBlockAjitomate();
    }

    private void OnBlockAjitomateCompleted()
    {
        if (currentPhase != Phase.Practice || bloqueIndex != 2) return;
        EndPractice();
    }

    protected override void OnPracticeEnd()
    {
        if (panelHUD) panelHUD.SetActive(false);
        if (panelCarrusel) panelCarrusel.SetActive(false);
        if (tmpTimer) tmpTimer.gameObject.SetActive(false);
    }

    // =================== SUMMARY ===================
    protected override void OnSummaryStart()
    {
        if (panelResumen) panelResumen.SetActive(true);
        UpdateInstructionOnce(4);
        // botón continuar del panel → CompleteActivity()
    }

    protected override void Initialize() { }

    // =================== HELPERS ===================
    /// Mueve el plano compartido del spawner al anchor del bloque activo
    private void ApplyShelfToSpawner(Transform anchor, Collider bounds)
    {
        if (!spawner) return;

        if (spawner.planeAnchor && anchor)
        {
            spawner.planeAnchor.position = anchor.position;
            spawner.planeAnchor.rotation = anchor.rotation;
        }

        // bounds que usará el drag para clampear dentro del plano del bloque activo
        spawner.planeBounds = bounds;
    }
}
