using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// AngryClientCalmDown - Actividad de Calmar al Cliente Molesto
/// 
/// FLUJO EN 2 FASES:
/// 
/// FASE 1 - CALMAR AL CLIENTE (6 preguntas, 5 niveles de emoción):
///   1. Cliente llega molesto (emoción = 0)
///   2. Ronda de 6 preguntas con opciones
///   3. Correcta: +1 emoción (bounce + success sound)
///   4. Incorrecta: -1 emoción (shake + error sound) → AVANZA igual
///   5. Al terminar las 6 preguntas:
///      - Emoción >= 3 → Pasa a Fase 2
///      - Emoción < 3  → Cliente se va → Adapter (0 estrellas) → Retry
/// 
/// FASE 2 - ESCANEO Y PAGO:
///   1. Escanear 4 productos
///   2. Subtotal → Pago con tarjeta → Ticket
///   3. Adapter evalúa resultado (estrellas según accuracy)
/// 
/// NOTAS DE DISEÑO:
/// - 6 preguntas + 5 niveles = margen de 1 error para llegar al máximo
/// - Error AVANZA a siguiente pregunta (como en la vida real, no puedes "desdecirte")
/// - El panel de emoción permanece visible durante toda la Fase 1
/// - Tanto éxito como fallo usan el Adapter/UnifiedSummaryPanel
/// 
/// TIPO DE EVALUACIÓN: AccuracyBased
/// - _successCount: Respuestas correctas
/// - _errorCount: Respuestas incorrectas
/// 
/// SCORING (AccuracyBased = successes / (successes + errors) * 100):
/// - 6/6 correct = 100% → ⭐⭐⭐
/// - 5/6 correct = 83%  → ⭐⭐
/// - 4/6 correct = 67%  → ⭐
/// - 3/6 o menos = 50%  → 0 estrellas (+ fail si emoción < 3)
/// 
/// CONFIGURACIÓN DEL ADAPTER:
/// - evaluationType: AccuracyBased
/// - errorsFieldName: "_errorCount"
/// - successesFieldName: "_successCount"
/// - expectedTotal: 0
/// - Star Thresholds: 100/80/60
/// 
/// INSTRUCCIONES:
/// 0 = Inicio / Bienvenida
/// 1 = Cliente llega molesto
/// 2 = Responde las preguntas para calmar al cliente
/// 3 = ¡Cliente calmado! Escanea los productos
/// 4 = Presiona SUBTOTAL
/// 5 = Comando de tarjeta
/// 6 = Escribe el monto
/// 7 = Entrega el ticket
/// </summary>
public class AngryClientCalmDown : ActivityBase
{
    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - CLIENTE Y PRODUCTOS
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Cliente y Productos")]
    [SerializeField] private CustomerSpawner customerSpawner;
    [SerializeField] private Transform productSpawnPoint;
    [SerializeField] private ProductScanner scanner;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - UI
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("UI de Actividad")]
    [SerializeField] private TextMeshProUGUI activityProductsText;
    [SerializeField] private TextMeshProUGUI activityTotalPriceText;
    [SerializeField] private TMP_InputField amountInputField;
    [SerializeField] private Button continueButton;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - BOTONES
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Botones")]
    [SerializeField] private List<Button> numberButtons;
    [SerializeField] private List<Button> enterButtons;
    [SerializeField] private List<Button> enterLastClicking;
    [SerializeField] private List<Button> subtotalButtons;
    [SerializeField] private List<Button> commandCardButtons;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - TICKET
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Ticket")]
    [SerializeField] private GameObject ticketPrefab;
    [SerializeField] private Transform ticketSpawnPoint;
    [SerializeField] private Transform ticketTargetPoint;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - EMOTION SLIDER
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Emotion Slider")]
    [SerializeField] private Slider emotionSlider;
    [SerializeField] private Gradient emotionColorGradient;
    [SerializeField] private CanvasGroup emotionContainer;
    [SerializeField] private RectTransform emotionPanelTransform;
    [SerializeField] private Vector2 hiddenPosition;
    [SerializeField] private Vector2 visiblePosition;

    [Header("Emotion - Animación")]
    [SerializeField] private float emotionAnimDuration = 0.4f;
    [SerializeField] private float shakeIntensity = 12f;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONSTANTES
    // ══════════════════════════════════════════════════════════════════════════════

    private const string CAM_START = "Iniciando Juego";
    private const string CAM_CLIENT = "Actividad 1 Cliente Camera";
    private const string CAM_LOOK_CLIENT = "Actividad 1 Mirar Cliente";
    private const string CAM_SUBTOTAL = "Actividad 2 Subtotal";
    private const string CAM_AMOUNT = "Actividad 2 Escribir Monto";
    private const string CAM_SUCCESS = "Actividad 2 Success";

    private const int MAX_EMOTION_LEVEL = 5;
    private const int MAX_PRODUCTS = 4;
    private const int TOTAL_QUESTIONS = 6;
    private const int MIN_EMOTION_TO_PASS = 3;
    private const int MAX_CLIENT_ATTEMPTS = 3;

    // ══════════════════════════════════════════════════════════════════════════════
    // ESTADO INTERNO
    // ══════════════════════════════════════════════════════════════════════════════

    private GameObject _currentCustomer;
    private CustomerMovement _currentCustomerMovement;
    private Client _currentClient;
    private GameObject _currentProduct;
    private int _scannedCount = 0;
    private int _currentEmotionLevel = 0;
    private int _currentQuestionIndex = 0;
    private int _clientAttempt = 0;

    // ══════════════════════════════════════════════════════════════════════════════
    // MÉTRICAS - LEÍDAS POR ADAPTER VÍA REFLECTION
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>Respuestas incorrectas al diálogo.</summary>
    private int _errorCount = 0;

    /// <summary>Respuestas correctas al diálogo.</summary>
    private int _successCount = 0;

    // ══════════════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void Initialize() { }

    public override void StartActivity()
    {
        base.StartActivity();

        ResetAll();
        // Cargar 6 preguntas aleatorias de la categoría "frustrated"
        DialogPoolLoader.RegisterInDialogSystem("frustrated", TOTAL_QUESTIONS);

        // Configurar scanner
        if (scanner != null)
        {
            scanner.UnbindUI(this);
            scanner.ClearUI();
            scanner.BindUI(this, activityProductsText, activityTotalPriceText, true);
        }

        productNames = ObjectPoolManager.Instance.GetAvailablePrefabNames(PoolTag.Producto).ToArray();

        InitializeCommands();

        // Estado inicial del emotion panel (oculto)
        emotionContainer.alpha = 0;
        emotionContainer.interactable = false;
        emotionContainer.blocksRaycasts = false;
        emotionPanelTransform.anchoredPosition = hiddenPosition;
        emotionSlider.value = 0;

        // Iniciar
        UpdateInstructionOnce(0, () =>
        {
            DOVirtual.DelayedCall(0.5f, BeginPhase1);
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // INICIALIZACIÓN DE COMANDOS
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void InitializeCommands()
    {
        base.InitializeCommands();

        foreach (var button in subtotalButtons) button.gameObject.SetActive(false);
        foreach (var button in enterButtons) button.gameObject.SetActive(false);
        foreach (var button in enterLastClicking) button.gameObject.SetActive(false);
        foreach (var button in commandCardButtons) button.gameObject.SetActive(false);

        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "SUBTOTAL",
            customAction = HandleSubTotal,
            requiredActivity = "Day4_ClienteMolestoCalmado",
            commandButtons = subtotalButtons
        });

        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "T+B+ENTER_",
            customAction = HandleEnterAmount,
            requiredActivity = "Day4_ClienteMolestoCalmado",
            commandButtons = commandCardButtons
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // MÉTRICAS
    // ══════════════════════════════════════════════════════════════════════════════

    private void ResetMetrics()
    {
        _errorCount = 0;
        _successCount = 0;
    }

    private void RegisterError()
    {
        _errorCount++;
        Debug.Log($"[AngryClientCalmDown] Error registrado. Total: {_errorCount}");
    }

    private void RegisterSuccess()
    {
        _successCount++;
        Debug.Log($"[AngryClientCalmDown] Acierto registrado. Total: {_successCount}");
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // ██  FASE 1: CALMAR AL CLIENTE (6 preguntas)
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Inicia Fase 1: Spawna cliente y empieza la ronda de preguntas.
    /// </summary>
    private void BeginPhase1()
    {
        _currentCustomer = customerSpawner.SpawnCustomer();
        _currentClient = _currentCustomer.GetComponent<Client>();
        _currentCustomerMovement = _currentCustomer.GetComponent<CustomerMovement>();

        cameraController.MoveToPosition(CAM_START, () =>
        {
            _currentCustomerMovement.MoveToCheckout(() =>
            {
                cameraController.MoveToPosition(CAM_CLIENT, () =>
                {
                    // Mostrar emotion slider UNA VEZ - permanece visible toda la Fase 1
                    ShowEmotionContainer(true);
                    emotionPanelTransform.DOAnchorPos(visiblePosition, 0.35f).SetEase(Ease.InOutQuad);
                    UpdateEmotionUI(0);

                    UpdateInstructionOnce(1, () =>
                    {
                        UpdateInstructionOnce(2, () =>
                        {
                            DOVirtual.DelayedCall(0.5f, AskNextQuestion);
                        });
                    });
                });
            });
        });
    }

    /// <summary>
    /// Muestra la siguiente pregunta. Si ya no hay más, evalúa resultado.
    /// </summary>
    private void AskNextQuestion()
    {
        var comments = DialogSystem.Instance.customerComments.FindAll(c => c.category == "frustrated");

        // ¿Terminaron todas las preguntas?
        if (_currentQuestionIndex >= comments.Count || _currentQuestionIndex >= TOTAL_QUESTIONS)
        {
            EvaluatePhase1();
            return;
        }

        var entry = comments[_currentQuestionIndex];

        DialogSystem.Instance.ShowClientDialog(_currentClient, entry.clientText, () =>
        {
            DialogSystem.Instance.ShowClientDialogWithOptions(
                entry.question,
                entry.options,
                entry.correctAnswer,
                OnCorrectAnswer,
                OnWrongAnswer
            );
        });
    }

    /// <summary>
    /// Respuesta correcta: sube emoción, avanza a siguiente pregunta.
    /// NOTA: DialogSystem YA llama HideDialog() y PlaySound("success"),
    /// no duplicar aquí.
    /// </summary>
    private void OnCorrectAnswer()
    {
        RegisterSuccess();

        _currentEmotionLevel = Mathf.Min(_currentEmotionLevel + 1, MAX_EMOTION_LEVEL);
        AnimateEmotionTo(_currentEmotionLevel);

        _currentQuestionIndex++;

        // DialogSystem ya llamó HideDialog(false) → isActive se resetea en 0.25s
        // Esperar lo suficiente y mostrar siguiente pregunta
        DOVirtual.DelayedCall(1.0f, AskNextQuestion);
    }

    /// <summary>
    /// Respuesta incorrecta: baja emoción, shake, AVANZA a siguiente pregunta.
    /// NOTA: DialogSystem YA llama PlaySound("error"), no duplicar.
    /// DialogSystem NO llama HideDialog en error, debemos hacerlo nosotros.
    /// </summary>
    private void OnWrongAnswer()
    {
        RegisterError();

        _currentEmotionLevel = Mathf.Max(0, _currentEmotionLevel - 1);
        AnimateEmotionTo(_currentEmotionLevel);

        // Feedback visual: shake del panel de emoción
        if (emotionPanelTransform != null)
        {
            emotionPanelTransform.DOShakeAnchorPos(0.3f, shakeIntensity, 15)
                .SetEase(Ease.OutQuad);
        }

        _currentQuestionIndex++;

        // Forzar cierre del diálogo (DialogSystem NO lo hace en error)
        // Esto resetea isActive después de 0.25s de animación
        DialogSystem.Instance.HideDialog(false);

        // Esperar a que HideDialog termine (0.25s fade) + margen
        DOVirtual.DelayedCall(1.0f, AskNextQuestion);
    }

    /// <summary>
    /// Evalúa si el cliente fue calmado suficiente para pasar a Fase 2.
    /// Si falla: el cliente se va y llega uno nuevo (máximo 3 intentos).
    /// Si pierde los 3: Adapter muestra resultado final (score bajo).
    /// NOTA: El diálogo ya fue cerrado por OnCorrectAnswer/OnWrongAnswer.
    /// </summary>
    private void EvaluatePhase1()
    {
        if (_currentEmotionLevel >= MIN_EMOTION_TO_PASS)
        {
            // ✅ Cliente calmado → Diálogo de transición y pasar a escaneo
            HideEmotionPanel();

            DialogSystem.Instance.ShowClientDialog(
                _currentClient,
                "Gracias por tu paciencia, ya me siento mejor.",
                () =>
                {
                    DialogSystem.Instance.HideDialog();
                    BeginPhase2();
                });
        }
        else
        {
            // ❌ Cliente aún molesto → Se va
            _clientAttempt++;

            DialogSystem.Instance.ShowClientDialog(
                _currentClient,
                "¡No me convences! Me voy a otra caja.",
                () =>
                {
                    DialogSystem.Instance.HideDialog();
                    _currentCustomerMovement.MoveToExit();

                    if (_clientAttempt < MAX_CLIENT_ATTEMPTS)
                    {
                        // Aún quedan intentos → Llega nuevo cliente
                        DOVirtual.DelayedCall(1.5f, SpawnNextClient);
                    }
                    else
                    {
                        // Se acabaron los intentos → Adapter muestra resultado
                        HideEmotionPanel();
                        DOVirtual.DelayedCall(1.5f, ShowActivityComplete);
                    }
                });
        }
    }

    /// <summary>
    /// Spawna un nuevo cliente después de que el anterior se fue.
    /// Resetea emoción y preguntas, pero conserva las métricas acumuladas.
    /// </summary>
    private void SpawnNextClient()
    {
        // Resetear estado de la ronda (NO las métricas)
        _currentEmotionLevel = 0;
        _currentQuestionIndex = 0;
        UpdateEmotionUI(0);

        // Resetear índice de diálogos para reusar las preguntas
        DialogSystem.Instance.ResetCategoryIndex("frustrated");

        // Spawnar nuevo cliente
        _currentCustomer = customerSpawner.SpawnCustomer();
        _currentClient = _currentCustomer.GetComponent<Client>();
        _currentCustomerMovement = _currentCustomer.GetComponent<CustomerMovement>();

        cameraController.MoveToPosition(CAM_START, () =>
        {
            _currentCustomerMovement.MoveToCheckout(() =>
            {
                cameraController.MoveToPosition(CAM_CLIENT, () =>
                {
                    UpdateEmotionUI(0);

                    // Mostrar diálogo del nuevo cliente
                    DialogSystem.Instance.ShowClientDialog(
                        _currentClient,
                        "A ver si tú sí me atiendes bien...",
                        () =>
                        {
                            DialogSystem.Instance.HideDialog(false);
                            DOVirtual.DelayedCall(0.5f, AskNextQuestion);
                        });
                });
            });
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // ██  FASE 2: ESCANEO Y PAGO
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Inicia Fase 2: Cliente calmado, ahora escanear productos.
    /// </summary>
    private void BeginPhase2()
    {
        _scannedCount = 0;

        cameraController.MoveToPosition(CAM_START, () =>
        {
            UpdateInstructionOnce(3, SpawnNextProduct);
        });
    }

    /// <summary>
    /// Spawna el siguiente producto para escanear.
    /// </summary>
    private void SpawnNextProduct()
    {
        if (_scannedCount >= MAX_PRODUCTS) return;

        GameObject product = GetPooledProduct(_scannedCount % productNames.Length, productSpawnPoint);
        if (product != null)
        {
            product.SetActive(true);
            _currentProduct = product;
            BindCurrentProduct();
        }
    }

    /// <summary>
    /// Registra el producto escaneado y decide si continuar o ir al subtotal.
    /// </summary>
    public void RegisterProductScanned()
    {
        // Devolver producto al pool
        if (_currentProduct != null)
        {
            var drag = _currentProduct.GetComponent<DragObject>();
            string poolName = (drag != null && !string.IsNullOrEmpty(drag.OriginalPoolName))
                ? drag.OriginalPoolName
                : _currentProduct.name;

            ObjectPoolManager.Instance.ReturnToPool(PoolTag.Producto, poolName, _currentProduct);
            _currentProduct = null;
        }

        _scannedCount++;

        if (_scannedCount < MAX_PRODUCTS)
        {
            SpawnNextProduct();
        }
        else
        {
            // Todos escaneados → Subtotal
            cameraController.MoveToPosition(CAM_SUBTOTAL, () =>
            {
                UpdateInstructionOnce(4, () =>
                {
                    ActivateCommandButtons(subtotalButtons);
                    AnimateButtonsSequentiallyWithActivation(subtotalButtons);
                });
            });
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // PAGO CON TARJETA
    // ══════════════════════════════════════════════════════════════════════════════

    public void HandleSubTotal()
    {
        float totalAmount = GetTotalAmount(activityTotalPriceText);

        if (totalAmount <= 0)
            return;

        SoundManager.Instance.PlaySound("success");

        cameraController.MoveToPosition(CAM_CLIENT, () =>
        {
            DialogSystem.Instance.ShowClientDialog(
                "Tu",
                dialog: "Disculpe, ¿cuál será su método de pago?",
                onComplete: () =>
                {
                    DialogSystem.Instance.ShowClientDialog(
                        _currentClient,
                        dialog: "Con tarjeta, por favor.",
                        onComplete: () =>
                        {
                            DialogSystem.Instance.HideDialog(false);
                            cameraController.MoveToPosition(CAM_SUBTOTAL, () =>
                            {
                                UpdateInstructionOnce(5);
                                ActivateCommandButtons(commandCardButtons);
                                ActivateButtonWithSequence(commandCardButtons, 0, HandleEnterAmount);
                            });
                        });
                });
        });
    }

    public void HandleEnterAmount()
    {
        cameraController.MoveToPosition(CAM_SUBTOTAL, () =>
        {
            float totalAmount = GetTotalAmount(activityTotalPriceText);
            ActivateAmountInput(totalAmount);
        });
    }

    private void ActivateAmountInput(float amount)
    {
        SoundManager.Instance.PlaySound("success");

        cameraController.MoveToPosition(CAM_AMOUNT);

        if (amountInputField != null)
        {
            amountInputField.text = "";
            amountInputField.gameObject.SetActive(true);
            amountInputField.DeactivateInputField();
            amountInputField.ActivateInputField();
        }

        string amountString = ((int)amount).ToString() + "00";
        List<Button> selectedButtons = GetButtonsForAmount(amountString, numberButtons);

        foreach (var button in numberButtons)
            button.gameObject.SetActive(false);

        UpdateInstructionOnce(6);
        ActivateButtonWithSequence(selectedButtons, 0, () =>
        {
            ActivateButtonWithSequence(enterLastClicking, 0, MoveClientAndGenerateTicket);
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // TICKET Y FINALIZACIÓN
    // ══════════════════════════════════════════════════════════════════════════════

    private void MoveClientAndGenerateTicket()
    {
        cameraController.MoveToPosition(CAM_LOOK_CLIENT, () =>
        {
            if (_currentCustomerMovement != null)
            {
                _currentCustomerMovement.MoveToPinEntry(() =>
                {
                    UpdateInstructionOnce(7, () =>
                    {
                        InstantiateTicket(ticketPrefab, ticketSpawnPoint, ticketTargetPoint, HandleTicketDelivered);
                    });
                });
            }
        });
    }

    private void HandleTicketDelivered()
    {
        SoundManager.Instance.PlaySound("success");
        _currentCustomerMovement?.MoveToExit();

        ShowActivityComplete();
    }

    /// <summary>
    /// Muestra el resultado usando el Adapter.
    /// Se usa tanto para éxito como para fallo (el score determina las estrellas).
    /// </summary>
    private void ShowActivityComplete()
    {
        if (scanner != null)
            scanner.ClearUI();

        commandManager.commandList.Clear();

        cameraController.MoveToPosition(CAM_SUCCESS, () =>
        {
            SoundManager.Instance.RestorePreviousMusic();

            var adapter = GetComponent<ActivityMetricsAdapter>();

            if (adapter != null)
            {
                adapter.NotifyActivityCompleted();
            }
            else
            {
                ShowManualSuccessPanel();
            }
        });
    }

    /// <summary>
    /// Panel de éxito manual (fallback sin Adapter).
    /// </summary>
    private void ShowManualSuccessPanel()
    {
        SoundManager.Instance.PlaySound("win");

        continueButton.onClick.RemoveAllListeners();
        continueButton.onClick.AddListener(() =>
        {
            cameraController.MoveToPosition(CAM_START);
            CompleteActivity();
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // EMOTION SLIDER - ANIMACIONES
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Anima el slider de emoción suavemente al nivel indicado.
    /// OutBack al subir (bounce satisfactorio), OutCubic al bajar.
    /// </summary>
    private void AnimateEmotionTo(int level)
    {
        if (emotionSlider == null) return;

        emotionSlider.DOKill();

        Ease ease = level > emotionSlider.value ? Ease.OutBack : Ease.OutCubic;

        emotionSlider.DOValue(level, emotionAnimDuration).SetEase(ease)
            .OnUpdate(() =>
            {
                float normalized = emotionSlider.value / (float)MAX_EMOTION_LEVEL;
                var fillImage = emotionSlider.fillRect.GetComponent<Image>();
                if (fillImage != null && emotionColorGradient != null)
                {
                    fillImage.color = emotionColorGradient.Evaluate(normalized);
                }
            });
    }

    /// <summary>
    /// Actualiza el slider inmediatamente (sin animación). Solo para reset.
    /// </summary>
    private void UpdateEmotionUI(int level)
    {
        if (emotionSlider == null) return;

        emotionSlider.value = level;
        float normalized = level / (float)MAX_EMOTION_LEVEL;
        var fillImage = emotionSlider.fillRect.GetComponent<Image>();
        if (fillImage != null && emotionColorGradient != null)
        {
            fillImage.color = emotionColorGradient.Evaluate(normalized);
        }
    }

    /// <summary>
    /// Oculta el panel de emoción con animación.
    /// </summary>
    private void HideEmotionPanel()
    {
        if (emotionPanelTransform != null)
            emotionPanelTransform.DOAnchorPos(hiddenPosition, 0.35f).SetEase(Ease.InOutQuad);

        ShowEmotionContainer(false);
    }

    private void ShowEmotionContainer(bool visible)
    {
        if (emotionContainer == null) return;
        StartCoroutine(FadeEmotionContainer(visible));
    }

    private IEnumerator FadeEmotionContainer(bool visible)
    {
        float targetAlpha = visible ? 1f : 0f;
        float duration = 0.25f;
        float startAlpha = emotionContainer.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            emotionContainer.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        emotionContainer.alpha = targetAlpha;
        emotionContainer.interactable = visible;
        emotionContainer.blocksRaycasts = visible;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // RESET Y LIMPIEZA
    // ══════════════════════════════════════════════════════════════════════════════

    private void ResetAll()
    {
        _scannedCount = 0;
        _currentEmotionLevel = 0;
        _currentQuestionIndex = 0;
        _clientAttempt = 0;

        if (emotionSlider != null)
            emotionSlider.DOKill();

        UpdateEmotionUI(0);

        if (scanner != null)
            scanner.ClearUI();

        ResetMetrics();
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // PRODUCTOS - BINDING
    // ══════════════════════════════════════════════════════════════════════════════

    private void BindCurrentProduct()
    {
        if (_currentProduct == null) return;

        var drag = _currentProduct.GetComponent<DragObject>();
        if (drag == null) return;

        drag.OnScanned -= OnProductScanned;
        drag.OnScanned += OnProductScanned;
    }

    private void OnProductScanned(DragObject obj)
    {
        if (obj == null) return;

        obj.OnScanned -= OnProductScanned;
        if (scanner != null)
            scanner.RegisterProductScan(obj);

        RegisterProductScanned();
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // UTILIDADES
    // ══════════════════════════════════════════════════════════════════════════════

    public void OnNumberButtonPressed(string number)
    {
        if (amountInputField != null)
        {
            amountInputField.text += number;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // LIMPIEZA
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void OnDisable()
    {
        base.OnDisable();

        // Limpiar DOTween del emotion slider
        if (emotionSlider != null)
            emotionSlider.DOKill();

        if (emotionPanelTransform != null)
            emotionPanelTransform.DOKill();

        // Limpiar scanner
        if (scanner != null)
            scanner.UnbindUI(this);
    }

}