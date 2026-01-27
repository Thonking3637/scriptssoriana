using System.Collections.Generic;
using UnityEngine;

public class PesajeMermaActivity : ActivityBase
{
    // ─────────────────────────────────────────────────────────────────────────────
    // ESTADO GLOBAL
    // ─────────────────────────────────────────────────────────────────────────────
    private enum Stage { Tutorial, Practice, Summary }
    private Stage stage = Stage.Tutorial;

    private enum StageUbic { Tutorial, Practice, Summary }
    private StageUbic stageUbic = StageUbic.Tutorial;

    // ─────────────────────────────────────────────────────────────────────────────
    // CÁMARA
    // ─────────────────────────────────────────────────────────────────────────────
    [Header("Cámara")]
    public string camVistaBalanza = "A2_VistaBalanza";
    public string camVistaUbicacion = "A2_VistaUbicacion";
    public string camVistaRegistro = "A2_VistaRegistro";

    [Header("Contenedores de Fases")]
    public GameObject phase1Container;
    public GameObject phase2Container;
    public GameObject phase3Container;

    // ─────────────────────────────────────────────────────────────────────────────
    // FASE 1 - PESAJE (SELECCIÓN)
    // ─────────────────────────────────────────────────────────────────────────────
    [Header("FASE 1 - Seleccion")]
    [Header("Panels (siempre visibles; se controla interactable)")]
    public GameObject panelTarjetasJaba;        // Panel 1
    public GameObject panelCarruselProductos;   // Panel 2
    public GameObject panelBalanza;             // HUD
    public GameObject panelResumen;             // opcional
    public PanelResumenPesaje panelResumenUI;

    [Header("UI Components")]
    public CardPaletteJabaUI tarjetasJabaUI;
    public ProductCarouselUI carruselUI;
    public BalanzaHUD balanzaHUD;

    [Header("Infra")]
    public BalanzaSlot balanzaSlot;

    [Header("Práctica")]
    public int ejerciciosPractica = 5;
    private int ejerciciosHechos = 0;

    [Header("Metas")]
    public int objetivoPrimeraFase = 9;

    // Gating flags tutorial
    private bool saidJabaPlaced = false;
    private bool saidTaraPressed = false;
    private bool saidProductoPlaced = false;

    // Pesos / producto seleccionado
    private float taraKg = 0f;
    private float pesoProductoKg = 0f;
    private ProductoCatalogSO _productoPendiente;

    // Conteos F1
    private int objetivoTotal = 0;
    private int completados = 0;

    // ─────────────────────────────────────────────────────────────────────────────
    // FASE 2 - UBICACIÓN
    // ─────────────────────────────────────────────────────────────────────────────
    [Header("FASE 2 - Ubicación")]
    public GameObject panelCarruselUbicacion;     // Scroll con tarjetas de jabas llenas
    public GameObject panelResumenUbicacion;      // Panel fin de F2 (si no usas el UI script)
    public PanelResumenUbicacion panelResumenUbicacionUI;

    public UbicacionCarouselUI carruselUbicUI;    // Carrusel de Fase 2
    public UbicacionArea[] areasUbicacion;        // Huevos / Frutas / Verduras

    public int objetivoSegundaFase = 12;          // Se recalcula al iniciar F2
    private int colocadasUbic = 0;

    // ─────────────────────────────────────────────────────────────────────────────
    // FASE 3 - REGISTRO
    // ─────────────────────────────────────────────────────────────────────────────
    [Header("FASE 3 - Registro")]
    public GameObject panelGuiaSemana;
    public GameObject panelPaletas;
    public GameObject panelResumenRegistro;

    public GameObject panelTableroRegistros;
    public PanelTableroRegistro panelTableroRegistro;

    public PanelResumenRegistro panelResumenRegistroUI;

    public GuiaSemanaSpawner guiaSpawner;
    public SemanaGuiaSO guiaSemana;

    // Progreso (HUD)
    [Header("Progreso")]
    public TMPro.TMP_Text progressF1;   // “0/9”
    public TMPro.TMP_Text progressF2;   // “0/9”
    public TMPro.TMP_Text progressF3;   // “0/7” (o los que tenga la guía)

    // Contadores totales de la actividad (para el cierre final)
    private int totalPesados = 0;     // F1 confirmados (Tomar Nota)
    private int totalUbicados = 0;    // F2 colocados
    private int totalRegistrados = 0; // F3 completados

    [Header("Resumen Final")]
    public GameObject panelResumenFinal;

    // F3 – índice de tarjetas
    private int casosTotales = 0;
    private int casosCompletados = 0;
    private readonly Dictionary<string, CaseCardUI> _cardsByKey = new();

    // ─────────────────────────────────────────────────────────────────────────────
    // CICLO DE VIDA / WIRING
    // ─────────────────────────────────────────────────────────────────────────────
    protected override void Initialize() { }

    private void OnEnable()
    {
        UIDragToWorld.OnDropJaba += OnJabaDrop;
        UIDragToWorld.OnDropProducto += OnProductoDrop;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        UIDragToWorld.OnDropJaba -= OnJabaDrop;
        UIDragToWorld.OnDropProducto -= OnProductoDrop;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // FASE 1 - PESAJE
    // ─────────────────────────────────────────────────────────────────────────────
    public override void StartActivity()
    {
        base.StartActivity();

        // Calcular metas F1
        if (carruselUI)
        {
            objetivoTotal = carruselUI.TotalTargets();
            completados = 0;
            objetivoPrimeraFase = Mathf.Min(objetivoPrimeraFase, objetivoTotal);
        }

        // Cámara inicial
        if (cameraController && !string.IsNullOrEmpty(camVistaBalanza))
            cameraController.MoveToPosition(camVistaBalanza, null);

        // Estado inicial UI
        HideAllUIPanels();
        HideAllProgress();

        // HUD Balanza
        if (balanzaHUD)
        {
            balanzaHUD.SetInteractableTara(false);
            balanzaHUD.SetInteractableTomarNota(false);
            balanzaHUD.ResetDisplay();
            balanzaHUD.OnTaraPressed += OnTaraPressed;
            balanzaHUD.OnTomarNotaPressed += OnTomarNotaPressed;
        }

        stage = Stage.Tutorial;

        SetPhaseContainerActive(phase1Container, true);

        // UpdateInstruction(0) — “Hoy aprenderemos…”
        UpdateInstructionOnce(0, () =>
        {
            UpdateInstructionOnce(1);
            SetPanelState(panelTarjetasJaba, true, true);
        });
    }

    private void SetPanelInteractable(GameObject panel, bool interactable)
    {
        if (!panel) return;
        var cg = panel.GetComponent<CanvasGroup>() ?? panel.AddComponent<CanvasGroup>();
        cg.interactable = interactable;
        cg.blocksRaycasts = interactable;

        if (panel == panelTarjetasJaba && tarjetasJabaUI) tarjetasJabaUI.interactable = interactable;
        if (panel == panelCarruselProductos && carruselUI) carruselUI.interactable = interactable;
    }

    // DROP: JABA sobre balanza  → instrucción 2
    private void OnJabaDrop(JabaTipo tipo, GameObject prefab)
    {
        if (balanzaSlot == null) return;

        var view = balanzaSlot.SpawnOrReplaceJaba(prefab, tipo);

        taraKg = 0f;
        pesoProductoKg = 0f;
        _productoPendiente = null;

        if (balanzaHUD)
        {
            balanzaHUD.SetInteractableTomarNota(false);
            balanzaHUD.SetPeso(view.pesoJabaKg);
        }

        // Panel productos según etapa
        if (stage == Stage.Tutorial) SetPanelState(panelCarruselProductos, false);
        else SetPanelState(panelCarruselProductos, true, false);

        UpdateInstructionOnce(2);                 // ← NO mover

        SetPanelState(panelBalanza, true, true);
        if (balanzaHUD) balanzaHUD.SetInteractableTara(true);

        if (stage == Stage.Tutorial && !saidJabaPlaced) saidJabaPlaced = true;
        SoundManager.Instance?.PlaySound("success");
    }

    // HUD: TARA  → instrucción 3
    private void OnTaraPressed()
    {
        var jaba = balanzaSlot?.GetJabaView();
        taraKg = jaba ? jaba.pesoJabaKg : 0f;

        if (balanzaHUD)
        {
            balanzaHUD.ResetDisplay();
            balanzaHUD.SetInteractableTomarNota(false);
        }

        // Evitar TARA con producto ya puesto
        if (jaba != null && (jaba.productoActual != null || pesoProductoKg > 0f))
        {
            SoundManager.Instance?.PlaySound("error");
            return;
        }

        UpdateInstructionOnce(3);                 // ← NO mover

        SetPanelState(panelCarruselProductos, true, true);
        carruselUI?.SetGlobalInteractable(true);

        if (stage == Stage.Tutorial && !saidTaraPressed) saidTaraPressed = true;
        SoundManager.Instance?.PlaySound("success");
    }

    // DROP: PRODUCTO sobre jaba  → instrucción 4
    private void OnProductoDrop(ProductoCatalogSO producto)
    {
        var jaba = balanzaSlot?.GetJabaView();
        if (jaba == null || producto == null) return;

        if (!jaba.AceptaProducto(producto))
        {
            SoundManager.Instance?.PlaySound("error");
            return;
        }

        pesoProductoKg = producto.GetPesoAleatorio();
        jaba.SetContenidoConProducto(producto, pesoProductoKg);

        float lecturaKg = (taraKg > 0f) ? pesoProductoKg : (jaba.pesoJabaKg + pesoProductoKg);
        if (balanzaHUD) balanzaHUD.SetPeso(lecturaKg);

        _productoPendiente = producto;

        if (balanzaHUD)
        {
            balanzaHUD.SetInteractableTomarNota(true);
            balanzaHUD.SetInteractableTara(false);
        }

        UpdateInstructionOnce(4);                 // ← NO mover

        if (stage == Stage.Tutorial && !saidProductoPlaced) saidProductoPlaced = true;
        SoundManager.Instance?.PlaySound("success");
    }

    // HUD: TOMAR NOTA  → instrucción 5 (fin tutorial F1)
    private void OnTomarNotaPressed()
    {
        // 1) Marcar producto usado y contar completados
        if (_productoPendiente != null && carruselUI)
        {
            carruselUI.MarkAsUsed(_productoPendiente);
            _productoPendiente = null;
            completados++;
        }

        SoundManager.Instance?.PlaySound("pencil");

        // 2) Limpiar
        balanzaSlot?.Clear();
        if (balanzaHUD)
        {
            balanzaHUD.ResetDisplay();
            balanzaHUD.SetInteractableTomarNota(false);
            balanzaHUD.SetInteractableTara(false);
        }
        SetPanelInteractable(panelCarruselProductos, false);
        if (carruselUI) carruselUI.SetGlobalInteractable(false);

        taraKg = 0f;
        pesoProductoKg = 0f;

        // 3) Avance de etapa
        if (stage == Stage.Tutorial)
        {
            UpdateInstructionOnce(5);            // ← NO mover

            stage = Stage.Practice;
            ejerciciosHechos = 0;

            SetPanelState(panelTarjetasJaba, true, true);
            SetPanelState(panelBalanza, true, false);
            SetPanelState(panelCarruselProductos, true, false);

            if (balanzaHUD)
            {
                balanzaHUD.SetInteractableTara(false);
                balanzaHUD.SetInteractableTomarNota(false);
                balanzaHUD.ResetDisplay();
            }

            saidJabaPlaced = saidTaraPressed = saidProductoPlaced = false;

            // ✅ Mostrar progreso F1 al entrar a práctica
            SetProgressVisible(progressF1, true);
            UpdateF1Progress();
        }
        else if (stage == Stage.Practice)
        {
            ejerciciosHechos++;

            bool metaPrimeraFase = objetivoPrimeraFase > 0 && completados >= objetivoPrimeraFase;
            bool sinProductos = carruselUI && carruselUI.singleUseGlobal && (carruselUI.RemainingAvailable() <= 0);
            bool metaPorRondas = ejerciciosHechos >= Mathf.Max(1, ejerciciosPractica);

            totalPesados = completados;
            UpdateF1Progress();

            if (metaPrimeraFase || sinProductos || metaPorRondas)
            {
                // ✅ Ocultar progreso antes de pasar a F2
                SetProgressVisible(progressF1, false);

                HideAllUIPanels();
                StartFaseUbicacion();            // salto directo a F2
                return;
            }

            // loop libre
            SetPanelState(panelTarjetasJaba, true, true);
            SetPanelState(panelBalanza, true, false);
            SetPanelState(panelCarruselProductos, true, false);
            if (balanzaHUD)
            {
                balanzaHUD.SetInteractableTara(false);
                balanzaHUD.SetInteractableTomarNota(false);
                balanzaHUD.ResetDisplay();
            }
            saidJabaPlaced = saidTaraPressed = saidProductoPlaced = false;
        }
    }


    public void ReiniciarPractica()
    {
        completados = 0;
        ejerciciosHechos = 0;
        stage = Stage.Practice;

        SetPanelState(panelTarjetasJaba, true, true);
        SetPanelState(panelBalanza, true, false);
        SetPanelState(panelCarruselProductos, true, false);

        carruselUI?.ResetUsados();

        if (balanzaHUD)
        {
            balanzaHUD.SetInteractableTara(false);
            balanzaHUD.SetInteractableTomarNota(false);
            balanzaHUD.ResetDisplay();
        }

        if (panelResumen) panelResumen.SetActive(false);
        if (panelResumenUI) panelResumenUI.gameObject.SetActive(false);

        saidJabaPlaced = saidTaraPressed = saidProductoPlaced = false;
    }

    public void ContinuarFase2()
    {
        SoundManager.Instance?.PlaySound("success");
        HideAllUIPanels();
        StartFaseUbicacion();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // FASE 2 - UBICACIÓN
    // ─────────────────────────────────────────────────────────────────────────────
    private void OnEnable_Fase2() => UIDragToWorldUbicacion.OnDropUbicacion += OnDropUbicacion_F2;
    private void OnDisable_Fase2() => UIDragToWorldUbicacion.OnDropUbicacion -= OnDropUbicacion_F2;

    private void StartFaseUbicacion()
    {
        if (cameraController && !string.IsNullOrEmpty(camVistaUbicacion))
            cameraController.MoveToPosition(camVistaUbicacion, () => UpdateInstructionOnce(6));

        // Oculta progresos de otras fases
        SetProgressVisible(progressF1, false);
        SetProgressVisible(progressF2, false);
        SetProgressVisible(progressF3, false);

        HideAllUIPanels();
        SetPhaseContainerActive(phase2Container, true);
        SetPanelState(panelCarruselUbicacion, true, true);

        if (carruselUbicUI)
        {
            objetivoSegundaFase = carruselUbicUI.TotalTargets();
            colocadasUbic = 0;
        }

        // 👇 AHORA sí actualiza el texto, pero sin mostrarlo aún (sigue en tutorial)
        UpdateF2Progress();

        carruselUbicUI?.SetGlobalInteractable(true);

        OnDisable_Fase2();
        OnEnable_Fase2();

        stageUbic = StageUbic.Tutorial;
    }

    private void FinishFaseUbicacion()
    {
        stageUbic = StageUbic.Summary;

        SetPanelState(panelCarruselUbicacion, false);
        carruselUbicUI?.SetGlobalInteractable(false);

        if (panelResumenUbicacionUI)
        {
            SetPanelState(panelResumenUbicacionUI.gameObject, true, true);
            panelResumenUbicacionUI.MostrarResumen(colocadasUbic, objetivoSegundaFase);
        }
        else
        {
            SetPanelState(panelResumenUbicacion, true, true);
        }
    }

    // DROP: F2 → instrucción 7 en primer acierto
    private void OnDropUbicacion_F2(JabaTipo tipoJaba, GameObject prefabJaba, UbicacionArea areaHit, UbicacionSlot slotHit)
    {
        if (areaHit == null || areaHit.tipoArea != tipoJaba)
        {
            SoundManager.Instance?.PlaySound("error");
            return;
        }

        // Slot target
        UbicacionSlot target = (slotHit != null && !slotHit.ocupado) ? slotHit : areaHit.GetFirstFreeSlot();
        if (target == null)
        {
            SoundManager.Instance?.PlaySound("error");
            return;
        }

        var inst = target.Place(prefabJaba);
        if (!inst)
        {
            SoundManager.Instance?.PlaySound("error");
            return;
        }

        carruselUbicUI?.MarkAsUsedByPrefab(prefabJaba);
        colocadasUbic++;

        totalUbicados = colocadasUbic;
        UpdateF2Progress();

        if (stageUbic == StageUbic.Tutorial)
        {
            SetProgressVisible(progressF2, true);
            UpdateInstructionOnce(7, () =>
            {
                stageUbic = StageUbic.Practice;
                UpdateF2Progress();
            });
        }

        SoundManager.Instance?.PlaySound("success");

        if (colocadasUbic >= Mathf.Max(1, objetivoSegundaFase))
        {
            SetProgressVisible(progressF2, false);

            HideAllUIPanels();
            StartFaseRegistro();  // salto directo a F3
            return;
        }
    }

    public void ContinuarFase3()
    {
        HideAllUIPanels();
        StartFaseRegistro();
    }

    public void ReiniciarUbicacion()
    {
        if (areasUbicacion != null)
            foreach (var a in areasUbicacion) a?.ClearAll();

        carruselUbicUI?.ResetUsados();
        colocadasUbic = 0;
        stageUbic = StageUbic.Practice;

        HideAllUIPanels();
        SetPanelState(panelCarruselUbicacion, true, true);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // FASE 3 - REGISTRO
    // ─────────────────────────────────────────────────────────────────────────────
    private void StartFaseRegistro()
    {
        HideAllUIPanels();
        SetPhaseContainerActive(phase3Container, true);

        if (cameraController && !string.IsNullOrEmpty(camVistaRegistro))
            cameraController.MoveToPosition(camVistaRegistro, () =>
            {
                UpdateInstructionOnce(8, () =>
                {
                    SetPanelState(panelGuiaSemana, true, true);

                    if (guiaSpawner)
                    {
                        guiaSpawner.guia = guiaSemana;
                        guiaSpawner.ClearAndPopulate();
                        BuildCaseCardIndex();
                    }

                    UpdateInstructionOnce(9, () =>
                    {
                        SetPanelState(panelTableroRegistros, true, true);

                        UpdateInstructionOnce(10, () =>
                        {
                            SetPanelState(panelPaletas, true, true);
                            UpdateInstructionOnce(11);
                        });

                        // Datos guía + progreso inicial
                        casosTotales = guiaSemana ? guiaSemana.casos.Count : 0;
                        casosCompletados = 0;

                        // Apaga los otros contadores y enciende el de F3
                        SetProgressVisible(progressF1, false);
                        SetProgressVisible(progressF2, false);
                        SetProgressVisible(progressF3, true);

                        UpdateF3Progress();

                        // Wiring tablero
                        if (panelTableroRegistro)
                        {
                            panelTableroRegistro.guiaSemana = guiaSemana;
                            panelTableroRegistro.OnCasoCompletado -= OnCasoCompletado;
                            panelTableroRegistro.OnCasoCompletado += OnCasoCompletado;
                        }
                    });
                });
            });
    }


    private void OnCasoCompletado(SemanaGuiaSO.CasoDia caso)
    {
        casosCompletados++;
        totalRegistrados = casosCompletados;
        UpdateF3Progress();

        UpdateInstructionOnce(12);
        // Marcar tarjeta en verde
        var key = Key(caso.dia, caso.producto);
        if (_cardsByKey.TryGetValue(key, out var card)) card.SetEstado(true);
        else
        {
            var cards = panelGuiaSemana.GetComponentsInChildren<CaseCardUI>(true);
            foreach (var c in cards)
            {
                if (c.data != null && string.Equals(Key(c.data.dia, c.data.producto), key))
                {
                    c.SetEstado(true); _cardsByKey[key] = c; break;
                }
            }
        }

        if (casosCompletados >= casosTotales && casosTotales > 0)
        {
            HideAllProgress();
            HideAllUIPanels();

            if (panelResumenFinal) panelResumenFinal.SetActive(true);

            SetPanelState(panelResumenRegistro, true, true);
            if (panelResumenRegistroUI)
            {               
                SoundManager.Instance?.PlaySound("win");
                UpdateInstructionOnce(13);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // HELPERS DE UI / PANELES
    // ─────────────────────────────────────────────────────────────────────────────
    private void SetPanelState(GameObject panel, bool visible, bool interactable = false)
    {
        if (!panel) return;

        // CanvasGroup
        var cg = panel.GetComponent<CanvasGroup>() ?? panel.AddComponent<CanvasGroup>();
        cg.alpha = visible ? 1f : 0f;
        cg.interactable = visible && interactable;
        cg.blocksRaycasts = visible && interactable;

        panel.SetActive(visible);

        // Reenvío a UIs específicas
        if (panel == panelTarjetasJaba && tarjetasJabaUI) tarjetasJabaUI.interactable = visible && interactable;
        if (panel == panelCarruselProductos && carruselUI) carruselUI.interactable = visible && interactable;
        if (panel == panelCarruselUbicacion && carruselUbicUI) carruselUbicUI.interactable = visible && interactable;
    }

    // PesajeMermaActivity.cs (Fragmento de HideAllUIPanels)

    private void HideAllUIPanels()
    {
        // F1 (Los paneles internos siguen gestionándose si es necesario)
        SetPanelState(panelTarjetasJaba, false);
        SetPanelState(panelCarruselProductos, false);
        SetPanelState(panelBalanza, false);
        SetPanelState(panelResumen, false);
        if (panelResumenUI) panelResumenUI.gameObject.SetActive(false);

        // F2
        SetPanelState(panelCarruselUbicacion, false);
        SetPanelState(panelResumenUbicacion, false);
        if (panelResumenUbicacionUI) panelResumenUbicacionUI.gameObject.SetActive(false);

        // F3
        SetPanelState(panelTableroRegistros, false);
        SetPanelState(panelGuiaSemana, false);
        SetPanelState(panelPaletas, false);
        SetPanelState(panelResumenRegistro, false);
        if (panelResumenRegistroUI) panelResumenRegistroUI.gameObject.SetActive(false);
    }

    private void SetPhaseContainerActive(GameObject panel, bool visible)
    {
        if (panel)
        {
            panel.SetActive(visible);
        }
    }

    private string Key(string dia, string prod) =>
        $"{dia?.Trim().ToUpperInvariant()}|{prod?.Trim().ToUpperInvariant()}";

    private void BuildCaseCardIndex()
    {
        _cardsByKey.Clear();
        var cards = panelGuiaSemana.GetComponentsInChildren<CaseCardUI>(true);
        foreach (var c in cards)
        {
            if (c.data == null) continue;
            var k = Key(c.data.dia, c.data.producto);
            if (!_cardsByKey.ContainsKey(k)) _cardsByKey.Add(k, c);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // PROGRESO (HUD)
    // ─────────────────────────────────────────────────────────────────────────────
    private void UpdateF1Progress()
    {
        if (progressF1)
            progressF1.text = $"{completados}/{objetivoPrimeraFase}";
    }

    private void UpdateF2Progress()
    {
        if (progressF2)
            progressF2.text = $"{colocadasUbic}/{Mathf.Max(1, objetivoSegundaFase)}";
    }

    private void UpdateF3Progress()
    {
        if (progressF3)
            progressF3.text = $"{casosCompletados}/{Mathf.Max(1, casosTotales)}";
    }

    // ── Helpers de visibilidad de progreso ─────────────────────────────
    private void SetProgressVisible(TMPro.TMP_Text label, bool visible)
    {
        if (!label) return;
        label.gameObject.SetActive(visible);
    }

    private void HideAllProgress()
    {
        SetProgressVisible(progressF1, false);
        SetProgressVisible(progressF2, false);
        SetProgressVisible(progressF3, false);
    }
}
