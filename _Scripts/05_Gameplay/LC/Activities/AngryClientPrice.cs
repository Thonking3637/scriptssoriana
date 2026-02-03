using DG.Tweening;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// AngryClientPrice - Actividad de Cliente con Precio Cambiado
/// 
/// FLUJO:
///   1. Escanear 4 productos
///   2. Cliente reclama que el precio está mal
///   3. Pregunta: ¿Cómo responder? → "Llamaré a mi supervisora"
///   4. Presionar botón de supervisora
///   5. Supervisora llega
///   6. Pregunta: ¿Qué le dices? → "El precio está mal"
///   7. Supervisora confirma → Password → Cambiar precio
///   8. Recalcular total → Subtotal → Pago tarjeta → Ticket
///   9. Adapter evalúa resultado
/// 
/// NOTA: Los diálogos son INLINE (no usan JSON pool) porque enseñan
/// un procedimiento específico de Chedraui para cambio de precio.
/// 
/// TIPO DE EVALUACIÓN: AccuracyBased
/// - _successCount: Preguntas respondidas correctamente al primer intento
/// - _errorCount: Intentos incorrectos acumulados
/// 
/// SCORING (2 preguntas, retry permitido):
/// - 0 errores = 100% → ⭐⭐⭐
/// - 1 error   = 67%  → ⭐⭐
/// - 2 errores = 50%  → ⭐
/// - 3+ errores        → 0 estrellas
/// 
/// CONFIGURACIÓN DEL ADAPTER:
/// - evaluationType: AccuracyBased
/// - errorsFieldName: "_errorCount"
/// - successesFieldName: "_successCount"
/// - expectedTotal: 0
/// - Star Thresholds: 100/60/40
/// 
/// INSTRUCCIONES:
///  0 = Inicio / Bienvenida
///  1 = Escanea los productos
///  2 = Responde al cliente
///  3 = Presiona el botón de supervisora
///  4 = La supervisora viene en camino
///  5 = Comando de cambio de precio
///  6 = Password de supervisora
///  7 = Escribe el nuevo precio
///  8 = Presiona SUBTOTAL
///  9 = Comando de tarjeta
/// 10 = Escribe el monto
/// 11 = Entrega el ticket
/// </summary>
public class AngryClientPrice : ActivityBase
{
    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - PRODUCTOS Y SCANNER
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Productos y Scanner")]
    [SerializeField] private Transform _spawnPoint;
    [SerializeField] private ProductScanner _scanner;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - UI
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("UI de Actividad")]
    [SerializeField] private TextMeshProUGUI _activityProductsText;
    [SerializeField] private TextMeshProUGUI _activityTotalPriceText;
    [SerializeField] private TMP_InputField _amountInputField;
    [SerializeField] private Button _continueButton;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - BOTONES
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Botones")]
    [SerializeField] private List<Button> _numberButtons;
    [SerializeField] private List<Button> _enterButtons;
    [SerializeField] private List<Button> _subtotalButtons;
    [SerializeField] private List<Button> _commandCardButtons;

    [Header("Botones - Cambio de Precio")]
    [SerializeField] private List<Button> _commandChangePrice;
    [SerializeField] private List<Button> _enterChangePrice;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - CAMBIO DE PRECIO
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Cambio de Precio")]
    [SerializeField] private GameObject _supervisorPasswordPanel;
    [SerializeField] private TextMeshProUGUI _passwordText;
    [SerializeField] private GameObject _priceInputPanel;
    [SerializeField] private TMP_InputField _amountChangePriceInputField;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - CLIENTE
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Cliente")]
    [SerializeField] private CustomerSpawner _customerSpawner;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - TICKET
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Ticket")]
    [SerializeField] private GameObject _ticketPrefab;
    [SerializeField] private Transform _ticketSpawnPoint;
    [SerializeField] private Transform _ticketTargetPoint;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - SUPERVISORA
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Supervisora")]
    [SerializeField] private SupervisorButton _supervisorButton;
    [SerializeField] private GameObject _supervisorPrefab;
    [SerializeField] private Transform _supervisorSpawnPoint;
    [SerializeField] private Transform _supervisorEntryPoint;
    [SerializeField] private List<Transform> _supervisorMiddlePath;
    [SerializeField] private Transform _supervisorExitPoint;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONSTANTES
    // ══════════════════════════════════════════════════════════════════════════════

    private const string CAM_START = "Iniciando Juego";
    private const string CAM_CLIENT = "Actividad 1 Cliente Camera";
    private const string CAM_LOOK_CLIENT = "Actividad 1 Mirar Cliente";
    private const string CAM_SUPERVISOR = "Actividad Supervisor Camera";
    private const string CAM_SUPERVISOR_BTN = "Vista Boton Supervisora";
    private const string CAM_CASHIER = "Actividad 3 Cajera";
    private const string CAM_CHANGE_PRICE = "Actividad 3 Cambiar Precio";
    private const string CAM_SUBTOTAL = "Actividad 3 Subtotal";
    private const string CAM_AMOUNT = "Actividad 3 Escribir Monto";
    private const string CAM_SUCCESS = "Actividad 3 Success";

    private const int PRODUCTS_TO_SCAN = 4;
    private const float PRICE_DISCOUNT = 10f;

    // ══════════════════════════════════════════════════════════════════════════════
    // ESTADO INTERNO
    // ══════════════════════════════════════════════════════════════════════════════

    private GameObject _currentCustomer;
    private CustomerMovement _currentCustomerMovement;
    private Client _currentClient;
    private GameObject _currentProduct;
    private GameObject _currentSupervisor;
    private int _scannedCount = 0;
    private int _lastProductIndex = -1;
    private DragObject _lastScannedProduct;
    private List<DragObject> _scannedProducts = new();
    private TMP_InputField _currentInputField;

    // ══════════════════════════════════════════════════════════════════════════════
    // MÉTRICAS - LEÍDAS POR ADAPTER VÍA REFLECTION
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>Intentos incorrectos en diálogos.</summary>
    private int _errorCount = 0;

    /// <summary>Preguntas respondidas correctamente al primer intento.</summary>
    private int _successCount = 0;

    /// <summary>Flag para saber si la pregunta actual ya tuvo error.</summary>
    private bool _currentQuestionHadError = false;

    // ══════════════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void Initialize() { }

    public override void StartActivity()
    {
        base.StartActivity();

        ResetValues();

        // Configurar botón de supervisora
        if (_supervisorButton != null)
        {
            _supervisorButton.gameObject.SetActive(false);
            _supervisorButton.OnPressed -= OnSupervisorButtonPressed;
            _supervisorButton.OnPressed += OnSupervisorButtonPressed;
        }

        InitializeCommands();

        if (_scanner != null)
        {
            _scanner.UnbindUI(this);
            _scanner.ClearUI();
            _scanner.BindUI(this, _activityProductsText, _activityTotalPriceText, true);
        }

        productNames = ObjectPoolManager.Instance.GetAvailablePrefabNames(PoolTag.Producto).ToArray();

        UpdateInstructionOnce(0, () =>
        {
            DOVirtual.DelayedCall(0.5f, SpawnCustomerAndStart);
        });
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (_supervisorButton != null)
            _supervisorButton.OnPressed -= OnSupervisorButtonPressed;

        if (_scanner != null)
            _scanner.UnbindUI(this);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // INICIALIZACIÓN
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void InitializeCommands()
    {
        base.InitializeCommands();

        foreach (var button in _subtotalButtons) button.gameObject.SetActive(false);
        foreach (var button in _enterButtons) button.gameObject.SetActive(false);
        foreach (var button in _commandChangePrice) button.gameObject.SetActive(false);
        foreach (var button in _enterChangePrice) button.gameObject.SetActive(false);
        foreach (var button in _commandCardButtons) button.gameObject.SetActive(false);

        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "D+C+ENTER_",
            customAction = HandleChangePassword,
            requiredActivity = "Day4_ClientePrecioCambiado",
            commandButtons = _commandChangePrice
        });

        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "SUBTOTAL",
            customAction = HandleSubTotal,
            requiredActivity = "Day4_ClientePrecioCambiado",
            commandButtons = _subtotalButtons
        });

        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "T+B+ENTER____",
            customAction = HandleEnterAmount,
            requiredActivity = "Day4_ClientePrecioCambiado",
            commandButtons = _commandCardButtons
        });
    }

    private void ResetValues()
    {
        _scannedCount = 0;
        _lastProductIndex = -1;
        _currentProduct = null;
        _lastScannedProduct = null;
        _scannedProducts.Clear();
        _errorCount = 0;
        _successCount = 0;
        _currentQuestionHadError = false;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FASE 1: ESCANEO DE PRODUCTOS
    // ══════════════════════════════════════════════════════════════════════════════

    private void SpawnCustomerAndStart()
    {
        _currentCustomer = _customerSpawner.SpawnCustomer();
        _currentClient = _currentCustomer.GetComponent<Client>();
        _currentCustomerMovement = _currentCustomer.GetComponent<CustomerMovement>();

        cameraController.MoveToPosition(CAM_START, () =>
        {
            _currentCustomerMovement.MoveToCheckout(() =>
            {
                UpdateInstructionOnce(1, SpawnNextProduct);
            });
        });
    }

    private void SpawnNextProduct()
    {
        if (_scannedCount >= PRODUCTS_TO_SCAN) return;

        GameObject prefab = ObjectPoolManager.Instance.GetRandomPrefabFromPool(PoolTag.Producto, ref _lastProductIndex);
        if (prefab == null) return;

        _currentProduct = ObjectPoolManager.Instance.GetFromPool(PoolTag.Producto, prefab.name);
        if (_currentProduct == null) return;

        _currentProduct.transform.position = _spawnPoint.position;
        _currentProduct.transform.rotation = prefab.transform.rotation;
        _currentProduct.transform.SetParent(null);
        _currentProduct.SetActive(true);

        DragObject drag = _currentProduct.GetComponent<DragObject>();
        drag?.SetOriginalPoolName(prefab.name);

        BindCurrentProduct();
    }

    public void RegisterProductScanned()
    {
        if (_currentProduct == null) return;

        _lastScannedProduct = _currentProduct.GetComponent<DragObject>();
        _scannedProducts.Add(_lastScannedProduct);

        string poolName = (_lastScannedProduct != null && !string.IsNullOrEmpty(_lastScannedProduct.OriginalPoolName))
            ? _lastScannedProduct.OriginalPoolName
            : _currentProduct.name;

        ObjectPoolManager.Instance.ReturnToPool(PoolTag.Producto, poolName, _currentProduct);
        _currentProduct = null;

        _scannedCount++;

        if (_scannedCount < PRODUCTS_TO_SCAN)
            SpawnNextProduct();
        else
            UpdateInstructionOnce(2, AskClientQuestion);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FASE 2: DIÁLOGO CON CLIENTE (pregunta 1)
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Cliente reclama que el precio está mal.
    /// Pregunta procedural: ¿Cómo responder? → "Llamaré a mi supervisora"
    /// </summary>
    private void AskClientQuestion()
    {
        _currentQuestionHadError = false;

        cameraController.MoveToPosition(CAM_CLIENT, () =>
        {
            DialogSystem.Instance.ShowClientDialog(
                _currentClient,
                "Hey, ese precio está mal, allá se mostraba $10 menos.",
                () =>
                {
                    DialogSystem.Instance.ShowClientDialogWithOptions(
                        "¿Cómo deberías responder ante esta situación?",
                        new List<string>
                        {
                            "Llamaré a mi supervisora, espere un momento por favor.",
                            "Ese es el precio, si no le gusta puede irse.",
                            "Yo no tengo la culpa, así viene en el sistema.",
                            "No sé, pregúntele a otra persona."
                        },
                        "Llamaré a mi supervisora, espere un momento por favor.",
                        OnClientQuestionCorrect,
                        OnClientQuestionWrong
                    );
                });
        });
    }

    private void OnClientQuestionCorrect()
    {
        if (!_currentQuestionHadError)
            _successCount++;

        UpdateInstructionOnce(3, ShowSupervisorButton);
    }

    private void OnClientQuestionWrong()
    {
        _errorCount++;
        _currentQuestionHadError = true;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FASE 3: LLAMAR SUPERVISORA
    // ══════════════════════════════════════════════════════════════════════════════

    private void ShowSupervisorButton()
    {
        cameraController.MoveToPosition(CAM_SUPERVISOR_BTN, () =>
        {
            if (_supervisorButton != null)
                _supervisorButton.gameObject.SetActive(true);
        });
    }

    public void OnSupervisorButtonPressed()
    {
        if (_supervisorButton != null)
            _supervisorButton.gameObject.SetActive(false);

        cameraController.MoveToPosition(CAM_START, () =>
        {
            UpdateInstructionOnce(4, SpawnSupervisor);
        });
    }

    private void SpawnSupervisor()
    {
        GameObject supervisorGO = Instantiate(_supervisorPrefab, _supervisorSpawnPoint.position, _supervisorPrefab.transform.rotation);
        _currentSupervisor = supervisorGO;

        SupervisorMovement movement = supervisorGO.GetComponent<SupervisorMovement>();
        movement.entryPoint = _supervisorEntryPoint;
        movement.middlePath = _supervisorMiddlePath;
        movement.exitPoint = _supervisorExitPoint;
        movement.animator = supervisorGO.GetComponent<Animator>();

        movement.GoToEntryPoint(() =>
        {
            cameraController.MoveToPosition(CAM_SUPERVISOR, ShowSupervisorDialog);
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FASE 4: DIÁLOGO CON SUPERVISORA (pregunta 2)
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Supervisora pregunta qué sucedió.
    /// Pregunta procedural: ¿Qué le dices? → "El precio está mal"
    /// </summary>
    private void ShowSupervisorDialog()
    {
        _currentQuestionHadError = false;

        DialogSystem.Instance.ShowClientDialog(
            _currentSupervisor.GetComponent<Client>(),
            "¿Qué sucedió? Cuéntame.",
            () =>
            {
                DialogSystem.Instance.ShowClientDialogWithOptions(
                    "¿Qué le debes decir a la supervisora?",
                    new List<string>
                    {
                        "El cliente menciona que el precio está mal",
                        "Ese cliente está loco",
                        "No entiendo qué quiere",
                        "No sé para qué vino"
                    },
                    "El cliente menciona que el precio está mal",
                    OnSupervisorQuestionCorrect,
                    OnSupervisorQuestionWrong
                );
            });
    }

    private void OnSupervisorQuestionCorrect()
    {
        if (!_currentQuestionHadError)
            _successCount++;

        DOVirtual.DelayedCall(0.5f, () =>
        {
            DialogSystem.Instance.ShowClientDialog(
                _currentSupervisor.GetComponent<Client>(),
                "Sí, es correcto, en este momento lo corregiremos.",
                () =>
                {
                    DialogSystem.Instance.HideDialog();

                    SupervisorMovement movement = _currentSupervisor.GetComponent<SupervisorMovement>();

                    cameraController.MoveToPosition(CAM_CASHIER, () =>
                    {
                        movement.GoThroughMiddlePath(() =>
                        {
                            UpdateInstructionOnce(5);
                            cameraController.MoveToPosition(CAM_CHANGE_PRICE, () =>
                            {
                                ActivateButtonWithSequence(_commandChangePrice, 0, HandleChangePassword);
                            });
                        });
                    });
                });
        });
    }

    private void OnSupervisorQuestionWrong()
    {
        _errorCount++;
        _currentQuestionHadError = true;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FASE 5: CAMBIO DE PRECIO
    // ══════════════════════════════════════════════════════════════════════════════

    private void HandleChangePassword()
    {
        UpdateInstructionOnce(6, () =>
        {
            _supervisorPasswordPanel.SetActive(true);
            AnimatePasswordEntry(() =>
            {
                _supervisorPasswordPanel.SetActive(false);
                ShowPriceInputPanel();
            });
        });
    }

    private void ShowPriceInputPanel()
    {
        _priceInputPanel.SetActive(true);

        if (_amountChangePriceInputField != null)
        {
            _amountChangePriceInputField.text = "";
            _amountChangePriceInputField.gameObject.SetActive(true);
            _amountChangePriceInputField.DeactivateInputField();
            _amountChangePriceInputField.ActivateInputField();
            _currentInputField = _amountChangePriceInputField;
        }

        float newPrice = Mathf.Max(0, _lastScannedProduct.productData.price - PRICE_DISCOUNT);
        string priceString = ((int)newPrice).ToString() + "00";

        List<Button> selectedButtons = GetButtonsForAmount(priceString, _numberButtons);

        foreach (var button in _numberButtons)
            button.gameObject.SetActive(false);

        UpdateInstructionOnce(7, () =>
        {
            ActivateButtonWithSequence(selectedButtons, 0, () =>
            {
                ActivateButtonWithSequence(_enterChangePrice, 0, ConfirmPriceChange);
            });
        });
    }

    private void ConfirmPriceChange()
    {
        if (!float.TryParse(_amountChangePriceInputField.text, out float rawValue)) return;

        float newPrice = rawValue / 100f;
        string targetName = _lastScannedProduct.productData.productName;

        // Actualizar precio en todos los productos con el mismo nombre
        foreach (var product in _scannedProducts)
        {
            if (product.productData.productName == targetName)
                product.productData.price = newPrice;
        }

        SupervisorMovement movement = _currentSupervisor.GetComponent<SupervisorMovement>();

        movement.GoToExit(() =>
        {
            _priceInputPanel.SetActive(false);
            RebuildScannerUIText();

            cameraController.MoveToPosition(CAM_SUBTOTAL, () =>
            {
                UpdateInstructionOnce(8);
                AnimateButtonsSequentiallyWithActivation(_subtotalButtons);
            });
        });
    }

    private void AnimatePasswordEntry(Action onComplete)
    {
        _passwordText.text = "";
        string[] sequence = { "1", "2", "3", "4" };
        int index = 0;

        DOVirtual.DelayedCall(0.3f, () =>
        {
            AddDigit();

            void AddDigit()
            {
                if (index >= sequence.Length)
                {
                    DOVirtual.DelayedCall(0.3f, () => onComplete?.Invoke());
                    return;
                }

                _passwordText.text += "*";
                index++;
                DOVirtual.DelayedCall(0.3f, AddDigit);
            }
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FASE 6: SUBTOTAL Y PAGO
    // ══════════════════════════════════════════════════════════════════════════════

    public void HandleSubTotal()
    {
        float totalAmount = GetTotalAmount(_activityTotalPriceText);

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
                                UpdateInstructionOnce(9);
                                ActivateCommandButtons(_commandCardButtons);
                                ActivateButtonWithSequence(_commandCardButtons, 0, HandleEnterAmount);
                            });
                        });
                });
        });
    }

    public void HandleEnterAmount()
    {
        float totalAmount = GetTotalAmount(_activityTotalPriceText);
        ActivateAmountInput(totalAmount);
    }

    private void ActivateAmountInput(float amount)
    {
        SoundManager.Instance.PlaySound("success");

        cameraController.MoveToPosition(CAM_AMOUNT);

        if (_amountInputField != null)
        {
            _amountInputField.text = "";
            _amountInputField.gameObject.SetActive(true);
            _amountInputField.DeactivateInputField();
            _amountInputField.ActivateInputField();
            _currentInputField = _amountInputField;
        }

        string amountString = ((int)amount).ToString() + "00";
        List<Button> selectedButtons = GetButtonsForAmount(amountString, _numberButtons);

        foreach (var button in _numberButtons)
            button.gameObject.SetActive(false);

        UpdateInstructionOnce(10);
        ActivateButtonWithSequence(selectedButtons, 0, () =>
        {
            ActivateButtonWithSequence(_enterButtons, 0, MoveClientAndGenerateTicket);
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
                    UpdateInstructionOnce(11, () =>
                    {
                        InstantiateTicket(_ticketPrefab, _ticketSpawnPoint, _ticketTargetPoint, HandleTicketDelivered);
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
    /// Muestra resultado usando el Adapter/UnifiedSummaryPanel.
    /// </summary>
    private void ShowActivityComplete()
    {
        if (_scanner != null)
            _scanner.ClearUI();

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

        _continueButton.onClick.RemoveAllListeners();
        _continueButton.onClick.AddListener(() =>
        {
            cameraController.MoveToPosition(CAM_START);
            CompleteActivity();
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // UTILIDADES
    // ══════════════════════════════════════════════════════════════════════════════

    private void RebuildScannerUIText()
    {
        float total = 0f;
        _activityProductsText.text = "";

        foreach (var product in _scannedProducts)
        {
            float subtotal = product.productData.price * product.productData.quantity;
            total += subtotal;

            _activityProductsText.text += $"{product.productData.code} - {product.productData.productName} - {product.productData.quantity} - ${subtotal:F2}\n";
        }

        _activityTotalPriceText.text = $"${total:F2}";
    }

    public void OnNumberButtonPressed(string number)
    {
        if (_currentInputField != null)
            _currentInputField.text += number;
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

        if (_scanner != null)
        {
            _scanner.RegisterProductScan(obj);
            RegisterProductScanned();
        }
    }
}