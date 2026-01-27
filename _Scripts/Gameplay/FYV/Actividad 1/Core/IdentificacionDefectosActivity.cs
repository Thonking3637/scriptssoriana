using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class IdentificacionDefectosActivity : ActivityBase
{
    // ====== Panels ======
    [Header("Panels")]
    [SerializeField] private GameObject panelTutorial;
    [SerializeField] private GameObject panelCarrusel;
    [SerializeField] private GameObject panelDecision;
    [SerializeField] private GameObject panelResumen;

    [Header("Fichas técnicas")]
    [SerializeField] private TechSheetPanelController techPanel;
    [SerializeField] private bool fichasEnTutorial = true;
    [SerializeField] private bool fichasEnEjercicios = true;


    // ====== UI (Carrusel) ======
    [Header("UI - Carrusel")]
    [SerializeField] private Button btnPrev;
    [SerializeField] private Button btnNext;
    [SerializeField] private Button btnMarcarDefecto;
    [SerializeField] private Button btnMarcarBueno;
    [SerializeField] private Button btnEvaluar;
    [SerializeField] private TMP_Text txtNombre;
    [SerializeField] private TMP_Text txtPeso;
    [SerializeField] private TMP_Text txtHeaderCaja;
    [SerializeField] private TMP_Text txtProgreso;

    // ====== UI (Decisión) ======
    [Header("UI - Decisión")]
    [SerializeField] private TMP_Text txtDecisionResumen;
    [SerializeField] private Button btnConfirmarAceptar;
    [SerializeField] private Button btnConfirmarMerma;

    // ====== UI (Resumen Final) ======
    [Header("UI - Resumen Final")]
    [SerializeField] private TMP_Text txtResumenFinal;
    [SerializeField] private Button btnFinalizar;

    // ====== Datos ======
    [Header("Datos / Lotes")]
    public List<LoteConfig> lotes;
    [SerializeField] private FruitCarousel3DLite carrusel;
    [SerializeField] private FruitView fruitView;

    // ====== Flags ======
    [Header("Flags")]
    [SerializeField] private bool highlightEnDemo = true;
    [SerializeField] private bool autoAvanzarTrasMarcar = true;

    // ====== Cajas (tap) ======
    [Header("Cajas - Tap")]
    [SerializeField] private Transform cajasRoot;
    private BoxTap[] cajas;
    private bool esperandoTapCaja = false;

    // ====== Estado ======
    private enum Stage { Tutorial, Exercise, Summary }
    private Stage stage = Stage.Tutorial;

    private int completedExercises = 0;
    private int nextExerciseLotIndex = 1;

    private int exerciseIndex = 0;           // usado para header/cámara (1..3 mostrado como +1)
    private LoteConfig loteActual;

    // Ítems inspeccionados y decisión del jugador por índice (true=defecto / false=bueno)
    private HashSet<int> inspeccionados = new();
    private Dictionary<int, bool> jugadorMarca = new();
    private bool[] ejerciciosCorrectos = new bool[3];

    // Gating de botones
    private bool uiReady = false;
    private bool canMark = false;
    private bool canEvaluate = false;

    bool saidRotate, saidNav, saidMark, saidAllInspected, saidEvaluate, saidConfirm;
    OneFingerRotate rotator;

    bool navLock = false;
    float navUnlockAt = 0f;

    // ====== Helpers UI ======
    private static void ShowPanel(GameObject go, bool interactable = true)
    {
        if (!go) return;
        go.SetActive(true);
        var cg = go.GetComponent<CanvasGroup>();
        if (cg) { cg.alpha = 1f; cg.blocksRaycasts = interactable; cg.interactable = interactable; }
    }
    private void HidePanel(GameObject go)
    {
        if (!go) return;
        var cg = go.GetComponent<CanvasGroup>();
        if (cg) { cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false; }
        go.SetActive(false);
    }
    private void UpdateButtons()
    {
        // Permisos por etapa del tutorial
        bool allowNav = true;
        bool allowMark = true;
        bool allowEval = true;

        if (stage == Stage.Tutorial)
        {
            allowNav = saidRotate;
            allowMark = saidRotate && saidNav;
            allowEval = true;
        }

        // Interactables reales
        if (btnPrev) btnPrev.interactable = uiReady && allowNav;
        if (btnNext) btnNext.interactable = uiReady && allowNav;
        if (btnMarcarDefecto) btnMarcarDefecto.interactable = uiReady && canMark && allowMark;
        if (btnMarcarBueno) btnMarcarBueno.interactable = uiReady && canMark && allowMark;
        if (btnEvaluar) btnEvaluar.interactable = uiReady && canEvaluate && allowEval;
    }
    private void UpdateProgreso()
    {
        if (!txtProgreso || loteActual == null) return;
        int total = loteActual.items != null ? loteActual.items.Count : 0;
        txtProgreso.SetText($"Inspeccionados: {inspeccionados.Count}/{total}");
    }

    // ====== Ciclo de vida ======
    public override void StartActivity()
    {
        base.StartActivity();

        // Paso 0: introducción
        UpdateInstructionOnce(0, () =>
        {
            UpdateInstructionOnce(1);
        });

        // Estado UI inicial
        HidePanel(panelTutorial);
        HidePanel(panelCarrusel);
        HidePanel(panelDecision);
        HidePanel(panelResumen);

        uiReady = false; canMark = false; canEvaluate = false; UpdateButtons();

        // Cámara a inicio general -> cajas

        cameraController.MoveToPosition("A1_Boxes", () =>
        {
            RegistrarCajas();
            ConfigurarCajasPorEtapa();
            esperandoTapCaja = true;
        });     

        // Resumen -> finalizar
        if (btnFinalizar)
        {
            btnFinalizar.onClick.RemoveAllListeners();
            btnFinalizar.onClick.AddListener(CompleteActivity);
        }
    }

    // ====== Cajas ======
    private void RegistrarCajas()
    {
        if (cajas != null && cajas.Length > 0) return;

        if (cajasRoot != null)
            cajas = cajasRoot.GetComponentsInChildren<BoxTap>(true);
        else
            cajas = GameObject.FindObjectsOfType<BoxTap>(true);

        if (cajas == null || cajas.Length == 0)
        {
            Debug.LogWarning("[A1] No se encontraron BoxTap en la escena.");
            return;
        }

        foreach (var box in cajas)
        {
            box.OnOpened -= OnCajaTapped;
            box.OnOpened += OnCajaTapped;
        }
    }

    private void DesregistrarCajas()
    {
        if (cajas == null) return;
        foreach (var box in cajas) box.OnOpened -= OnCajaTapped;
        cajas = null;
    }

    private void OnCajaTapped(BoxTap box)
    {
        if (!esperandoTapCaja || box == null) return;

        // ✅ Validar lote correcto según etapa
        int esperado = (stage == Stage.Tutorial) ? 0 : nextExerciseLotIndex;
        if (box.loteIndex != esperado)
        {
            // Caja no válida: feedback y NO abrir
            if (SoundManager.Instance) SoundManager.Instance.PlaySound("error");
            // opcional: sacudir caja, mostrar hint, etc.
            return;
        }

        // Caja correcta → detener blink y ocultarla
        box.StopBlink();
        if (box.gameObject.activeSelf) box.gameObject.SetActive(false);

        esperandoTapCaja = false;

        if (stage == Stage.Tutorial)
        {
            OnSeleccionCaja(0, 0);
            return;
        }

        if (stage == Stage.Exercise)
        {
            if (nextExerciseLotIndex >= 1 && nextExerciseLotIndex <= 3 && lotes != null && lotes.Count > nextExerciseLotIndex)
            {
                int ordinal = Mathf.Clamp(completedExercises + 1, 1, 3);
                OnSeleccionCaja(nextExerciseLotIndex, ordinal);
                nextExerciseLotIndex++;
            }
            else
            {
                if (completedExercises >= 3) MostrarResumenFinal();
            }
        }
    }

    // Llamable desde 3D con índice y ordinal de ejercicio para cámara/header
    public void OnSeleccionCaja(int loteIndex, int forExerciseOrdinal = 0)
    {
        if (!ValidarInfra()) return;
        if (lotes == null || lotes.Count <= loteIndex) { Debug.LogError("[A1] Lote inválido."); return; }

        inspeccionados.Clear();
        jugadorMarca.Clear();

        loteActual = lotes[loteIndex];

        bool permitirFichas = (stage == Stage.Tutorial && fichasEnTutorial) ||
                      (stage == Stage.Exercise && fichasEnEjercicios);

        if (techPanel)
        {
            if (permitirFichas && loteActual.techSheets != null)
            {
                techPanel.Configure(loteActual.techSheets.sprites, loteActual.techSheets.enabled);

                //En Tutorial: gating (se habilita tras girar). En Práctica: habilitado YA.
                techPanel.SetOpenButtonInteractable(stage == Stage.Tutorial ? false : true);

                // Reset de notificaciones por lote (evita que se “queden pegadas” de la demo)
                techPanel.openedNotified = false;
                techPanel.firstTabNotified = false;
                techPanel.zoomNotified = false;

                //Enlazar eventos SOLO durante tutorial
                techPanel.OnOpened = () =>
                {
                    if (stage == Stage.Tutorial)
                        UpdateInstructionOnce(4);
                };
                techPanel.OnFirstTabPressed = () =>
                {
                    if (stage == Stage.Tutorial)
                        UpdateInstructionOnce(5);
                };
                techPanel.OnZoomUsed = () =>
                {
                    if (stage == Stage.Tutorial)
                    {
                        UpdateInstructionOnce(6);
                        saidNav = false;
                        UpdateButtons();
                    }
                };
            }
            else
            {
                techPanel.CloseAll();
                techPanel.SetOpenButtonInteractable(false);
            }
        }

        // Encabezado
        if (txtHeaderCaja)
        {
            if (stage == Stage.Tutorial) txtHeaderCaja.SetText("Tutorial");
            else txtHeaderCaja.SetText($"Ejercicio {Mathf.Clamp(forExerciseOrdinal, 1, 3)} / 3");
        }

        // Cargar carrusel
        carrusel.OnShowItem = OnShowItem;
        carrusel.Load(loteActual.items);

        WireCarruselButtons();

        HidePanel(panelTutorial);
        ShowPanel(panelCarrusel, true);

        uiReady = true;
        canMark = true;
        canEvaluate = false; // se habilita cuando inspeccione TODO
        UpdateButtons();
        UpdateProgreso();

        // Cámara a la caja seleccionada: tutorial usa A1_TutorialCaja; ejercicios usan ordinal 01..03
        if (stage == Stage.Tutorial)
        {
            saidRotate = false; saidNav = false; saidMark = false;
            saidAllInspected = false; saidEvaluate = false; saidConfirm = false;

            cameraController.MoveToPosition("A1_TutorialCaja", () =>
            {
                UpdateInstructionOnce(2);
                UpdateButtons();
            });
        }
        else
        {
            // Usa el ordinal pasado: 1..3
            int ord = Mathf.Clamp(forExerciseOrdinal, 1, 3);
            exerciseIndex = ord - 1; // guarda para header interno si se usa en otros puntos
            cameraController.MoveToPosition($"A1_Ejercicio_{ord.ToString("00")}", null);
        }
    }

    // ====== Botones carrusel ======
    private void WireCarruselButtons()
    {
        // === PREV ===
        if (btnPrev)
        {
            btnPrev.onClick.RemoveAllListeners();
            btnPrev.onClick.AddListener(() =>
            {
                if (!uiReady || navLock) return;

                // Gating extra por etapa (además del interactable)
                if (stage == Stage.Tutorial && !saidRotate) return; // primero debe girar

                carrusel.Prev();

                if (stage == Stage.Tutorial && !saidNav)
                {
                    saidNav = true;
                    UpdateInstructionOnce(7); // "Ahora usa Prev/Next..." (tu índice)
                    UpdateButtons();          // habilita marcar ✔/✖
                }
            });
        }

        // === NEXT ===
        if (btnNext)
        {
            btnNext.onClick.RemoveAllListeners();
            btnNext.onClick.AddListener(() =>
            {
                if (!uiReady || navLock) return;

                if (stage == Stage.Tutorial && !saidRotate) return; // primero debe girar

                carrusel.Next();

                if (stage == Stage.Tutorial && !saidNav)
                {
                    saidNav = true;
                    UpdateInstructionOnce(7);
                    UpdateButtons();
                }
            });
        }

        // === MARCAR DEFECTO ===
        if (btnMarcarDefecto)
        {
            btnMarcarDefecto.onClick.RemoveAllListeners();
            btnMarcarDefecto.onClick.AddListener(() =>
            {
                if (!uiReady) return;
                // En tutorial: no permitir marcar antes de navegar
                if (stage == Stage.Tutorial && (!saidRotate || !saidNav)) return;

                MarkCurrent(true);
            });
        }

        // === MARCAR BUENO ===
        if (btnMarcarBueno)
        {
            btnMarcarBueno.onClick.RemoveAllListeners();
            btnMarcarBueno.onClick.AddListener(() =>
            {
                if (!uiReady) return;
                if (stage == Stage.Tutorial && (!saidRotate || !saidNav)) return;

                MarkCurrent(false);
            });
        }

        // === EVALUAR ===
        if (btnEvaluar)
        {
            btnEvaluar.onClick.RemoveAllListeners();
            btnEvaluar.onClick.AddListener(() =>
            {
                if (!uiReady) return;
                // Evaluar sigue gobernado por canEvaluate + UpdateButtons(); no se requiere extra
                OnEvaluarPressed_UI();
            });
        }
    }


    // ====== Evento del carrusel ======
    public void OnShowItem(FruitData d, int idx)
    {
        if (d == null) return;

        // Actualiza textos
        if (txtNombre) txtNombre.SetText(d.nombre ?? "");
        if (txtPeso) txtPeso.SetText($"{d.pesoKg:0.###} kg");

        bool showHL = (stage == Stage.Tutorial) && highlightEnDemo && d.esDefectuoso;

        //Buscar el rotador del hijo activo (el que tiene MeshRenderer)
        if (stage == Stage.Tutorial && fruitView != null)
        {
            OneFingerRotate rotHijo = null;

            foreach (Transform child in fruitView.transform)
            {
                var mr = child.GetComponent<MeshRenderer>();
                if (mr != null && child.gameObject.activeInHierarchy)
                {
                    rotHijo = child.GetComponent<OneFingerRotate>();
                    if (rotHijo != null) break;
                }
            }

            if (rotHijo != null)
            {
                // Desuscribimos y volvemos a suscribir para evitar duplicados
                rotHijo.OnFirstRotate -= OnFirstRotateTutorial;
                rotHijo.OnFirstRotate += OnFirstRotateTutorial;
                rotHijo.ResetEmit();
            }
            else
            {
                Debug.LogWarning("[OnShowItem] No se encontró OneFingerRotate activo en los hijos visibles.");
            }
        }

        // Estado de inspección y botones
        bool yaInspeccionado = inspeccionados.Contains(idx);
        canMark = uiReady && !yaInspeccionado;
        canEvaluate = uiReady && (inspeccionados.Count >= (loteActual?.items?.Count ?? 0));

        UpdateButtons();
    }



    private void OnFirstRotateTutorial()
    {
        if (stage == Stage.Tutorial && !saidRotate)
        {
            saidRotate = true;

            UpdateInstructionOnce(3);

            if (techPanel)
            {
                techPanel.SetOpenButtonInteractable(true);
            }

            // Bloquear todos los demás botones hasta que abra los afiches
            if (btnPrev) btnPrev.interactable = false;
            if (btnNext) btnNext.interactable = false;
            if (btnMarcarDefecto) btnMarcarDefecto.interactable = false;
            if (btnMarcarBueno) btnMarcarBueno.interactable = false;
            if (btnEvaluar) btnEvaluar.interactable = false;
        }
    }



    // ====== Marcar actual (bueno/defecto) y avanzar ======
    private void MarkCurrent(bool isDefect)
    {
        if (!uiReady || carrusel == null || loteActual == null) return;

        int idx = carrusel.CurrentIndex();
        if (idx < 0) return;
        if (inspeccionados.Contains(idx)) return; // ya inspeccionado

        var item = carrusel.Current();
        if (item == null) return;

        // Guarda la decisión del jugador y marca como inspeccionado
        jugadorMarca[idx] = isDefect;
        inspeccionados.Add(idx);

        // 🔊 Sonido inmediato (correcto / incorrecto)
        bool acierto = (isDefect == item.esDefectuoso);
        if (SoundManager.Instance)
            SoundManager.Instance.PlaySound(acierto ? "success" : "error");

        // Feedback SOLO en el tutorial
        if (stage == Stage.Tutorial && !saidMark)
        {
            saidMark = true;
            UpdateInstructionOnce(8); // “sigue marcando…”
            UpdateButtons();
        }

        UpdateProgreso();

        int total = loteActual.items != null ? loteActual.items.Count : 0;
        canEvaluate = (inspeccionados.Count >= total);

        if (stage == Stage.Tutorial && canEvaluate && !saidAllInspected)
        {
            saidAllInspected = true;
            UpdateInstructionOnce(9);
            UpdateButtons();
        }

        UpdateButtons();
        LockNav(0.2f);

        if (autoAvanzarTrasMarcar && inspeccionados.Count < total)
            AvanzarAlSiguienteNoInspeccionado();
    }


    private void AvanzarAlSiguienteNoInspeccionado()
    {
        int total = loteActual.items.Count;
        int start = carrusel.CurrentIndex();
        for (int i = 1; i <= total; i++)
        {
            int idx = (start + i) % total;
            if (!inspeccionados.Contains(idx))
            {
                int hops = i;
                while (hops-- > 0) carrusel.Next();
                break;
            }
        }
    }

    private void LockNav(float seconds = 0.2f)
    {
        navLock = true;
        navUnlockAt = Time.unscaledTime + seconds;
    }

    // ====== Evaluación del lote (por porcentaje de piezas) ======
    public void OnEvaluarPressed_UI() => OnEvaluarPressed();

    public void OnEvaluarPressed()
    {
        if (stage == Stage.Tutorial && !saidEvaluate)
        {
            saidEvaluate = true;
            UpdateInstructionOnce(10);
        }
        MostrarPanelDecision();
    }

    private void MostrarPanelDecision()
    {
        if (loteActual == null) return;

        int totalItems = (loteActual.items != null) ? loteActual.items.Count : 0;
        int defectuososReales = 0;
        foreach (var it in loteActual.items)
            if (it != null && it.esDefectuoso) defectuososReales++;

        float pct = (totalItems > 0) ? (defectuososReales / (float)totalItems) : 0f;
        bool recomendacionMerma = (pct >= 0.10f); // ≥10% ⇒ Merma

        if (txtDecisionResumen)
        {
            if (stage == Stage.Tutorial)
            {
                // En el tutorial: mostrar la regla y recomendación explícita
                txtDecisionResumen.SetText(
                    $"{loteActual.nombreLote}\n" +
                    $"Piezas: {defectuososReales}/{totalItems} con defecto ({pct * 100f:0.#}%)\n" +
                    $"Regla: > 10% = Merma\n" +
                    $"Recomendación: {(recomendacionMerma ? "MERMA" : "ACEPTAR")}"
                );
            }
            else
            {
                // En ejercicios: NO mostrar la recomendación, solo los datos
                txtDecisionResumen.SetText(
                    $"{loteActual.nombreLote}\n" +
                    $"Piezas con defecto: {defectuososReales}/{totalItems} ({pct * 100f:0.#}%)\n" +
                    $"Elige: Aceptar Lote o Merma"
                );
            }
        }

        if (btnConfirmarAceptar)
        {
            btnConfirmarAceptar.onClick.RemoveAllListeners();
            btnConfirmarAceptar.onClick.AddListener(() => ConfirmarDecision(false)); // jugador elige ACEPTAR
        }
        if (btnConfirmarMerma)
        {
            btnConfirmarMerma.onClick.RemoveAllListeners();
            btnConfirmarMerma.onClick.AddListener(() => ConfirmarDecision(true));   // jugador elige MERMA
        }

        ShowPanel(panelDecision, true);

        // Bloquea inputs del carrusel mientras decide
        uiReady = false; canMark = false; canEvaluate = false; UpdateButtons();
    }

    private void ConfirmarDecision(bool jugadorEligioMerma)
    {
        HidePanel(panelDecision);

        // Re-computar recomendación (regla del 10%)
        int totalItems = (loteActual.items != null) ? loteActual.items.Count : 0;
        int defectuososReales = 0;
        foreach (var it in loteActual.items)
            if (it != null && it.esDefectuoso) defectuososReales++;
        bool recomendacionMerma = (totalItems > 0) && ((defectuososReales / (float)totalItems) >= 0.10f);

        bool acierto = (jugadorEligioMerma == recomendacionMerma);
        SoundManager.Instance.PlaySound(acierto ? "success" : "error");

        if (stage == Stage.Tutorial)
        {
            if (!saidConfirm) { saidConfirm = true; UpdateInstructionOnce(11); } // 🔊 “Confirmaste tu decisión…”

            stage = Stage.Exercise;
            HidePanel(panelCarrusel);

            cameraController.MoveToPosition("A1_Boxes", () =>
            {
                ConfigurarCajasPorEtapa();
                esperandoTapCaja = true;
            });
            return;
        }

        if (stage == Stage.Exercise)
        {
            // Registra resultado del ejercicio actual (completedExercises es el índice que estamos cerrando)
            if (completedExercises >= 0 && completedExercises < ejerciciosCorrectos.Length)
                ejerciciosCorrectos[completedExercises] = acierto;

            completedExercises++;

            if (completedExercises < 3)
            {
                HidePanel(panelCarrusel);
                cameraController.MoveToPosition("A1_Boxes", () =>
                {
                    ConfigurarCajasPorEtapa();
                    esperandoTapCaja = true;
                });
            }
            else
            {
                MostrarResumenFinal();
            }
        }
    }

    // ====== Resumen final ======
    private void MostrarResumenFinal()
    {
        stage = Stage.Summary;

        HidePanel(panelCarrusel);
        HidePanel(panelDecision);

        UpdateInstructionOnce(12);
        if (txtResumenFinal)
        {
            int correctas = 0;
            for (int i = 0; i < 3; i++) if (ejerciciosCorrectos[i]) correctas++;

            txtResumenFinal.SetText(
                /*
                $"Ejercicio 1: {(ejerciciosCorrectos[0] ? "Correcto" : "Incorrecto")}\n" +
                $"Ejercicio 2: {(ejerciciosCorrectos[1] ? "Correcto" : "Incorrecto")}\n" +
                $"Ejercicio 3: {(ejerciciosCorrectos[2] ? "Correcto" : "Incorrecto")}\n" +
                */
                $"Puntaje: {correctas} / 3"
            );
        }

        ShowPanel(panelResumen, true);
    }

    private void ConfigurarCajasPorEtapa()
    {
        if (cajas == null) return;

        foreach (var box in cajas)
        {
            if (!box) continue;

            bool esCajaValida = false;

            if (stage == Stage.Tutorial)
            {
                esCajaValida = (box.loteIndex == 0);
            }
            else if (stage == Stage.Exercise)
            {
                esCajaValida = (box.loteIndex == nextExerciseLotIndex);
            }

            // Interacción sólo en la caja válida
            box.SetInteractable(esCajaValida && box.gameObject.activeInHierarchy);

            // Blink sólo en la caja válida y visible
            if (esCajaValida && box.gameObject.activeInHierarchy) box.StartBlink();
            else box.StopBlink();
        }
    }

    private void LateUpdate()
    {
        if (navLock && Time.unscaledTime >= navUnlockAt) navLock = false;
    }

    // ====== Util ======
    private bool ValidarInfra()
    {
        if (carrusel == null) { Debug.LogError("[A1] Carrusel no asignado."); return false; }
        if (fruitView == null) { Debug.LogError("[A1] FruitView no asignado."); return false; }
        return true;
    }


#if UNITY_EDITOR
    private void OnValidate()
    {
        if (carrusel != null && carrusel.view == null)
            Debug.LogWarning("[A1] Asigna FruitCarousel3DLite.view al FruitView de la escena.");
        if (fruitView != null)
        {
            if (fruitView.baseFilter == null || fruitView.baseRenderer == null)
                Debug.LogWarning("[A1] FruitView: asigna Base (MeshFilter y MeshRenderer).");
        }
    }
#endif

    private OneFingerRotate GetActiveChildRotator()
    {
        if (fruitView == null) return null;

        OneFingerRotate found = null;
        foreach (Transform child in fruitView.transform)
        {
            var mr = child.GetComponent<MeshRenderer>();
            if (mr != null && child.gameObject.activeInHierarchy)
            {
                found = child.GetComponent<OneFingerRotate>();
                break;
            }
        }
        return found;
    }

    protected override void Initialize() { /* sin implementación */ }
}
