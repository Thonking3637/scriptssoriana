using DG.Tweening;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// AlertCashWithdrawal - Actividad de Alerta de Retiro de Efectivo
/// 
/// FLUJO:
/// 1. Aparece alerta de retiro → Presionar ENTER
/// 2. Mostrar bolsa → Mover cámara a bandeja
/// 3. Abrir panel de dinero → Seleccionar $4000
/// 4. Validar monto → Mostrar inputs de denominación
/// 5. Ingresar cantidades por denominación
/// 6. Generar ticket → Entregar bolsa
/// 7. Completar actividad
/// 
/// TIPO DE EVALUACIÓN: AccuracyBased
/// - Único punto de error: Dar mal los $4000
/// 
/// CONFIGURACIÓN DEL ADAPTER:
/// - evaluationType: AccuracyBased
/// - errorsFieldName: "_errorCount"
/// - successesFieldName: "_successCount"
/// - expectedTotal: 0 (se calcula automático)
/// 
/// INSTRUCCIONES:
/// 0 = Bienvenida / Alerta inicial
/// 1 = Mostrar bolsa
/// 2 = Abrir panel de dinero
/// 3 = Ingresar denominaciones
/// 4 = Ticket
/// 5 = Entregar bolsa
/// </summary>
public class AlertCashWithdrawal : ActivityBase
{
    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - DINERO Y UI
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Money UI")]
    [SerializeField] private MoneySpawner moneySpawner;
    [SerializeField] private Transform bagTarget;
    [SerializeField] private Transform bagFinal;
    [SerializeField] private GameObject alertPanel;
    [SerializeField] private GameObject moneyPanel;
    [SerializeField] private TextMeshProUGUI targetAmountText;
    [SerializeField] private TextMeshProUGUI currentAmountText;
    [SerializeField] private Button validateButton;
    [SerializeField] private Vector2 panelStartPosition;
    [SerializeField] private Vector2 panelEndPosition;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - BOTONES
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Botones")]
    [SerializeField] private List<Button> enterButtons;
    [SerializeField] private List<Button> numberButtons;
    [SerializeField] private List<Button> enterValidationButtons;
    [SerializeField] private Button continueButton;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - DENOMINACIONES
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Inputs por Denominación")]
    [SerializeField] private GameObject denominationPanel;
    [SerializeField] private List<TMP_InputField> denominationInputs;
    [SerializeField] private List<float> denominationsOrdered;
    [SerializeField] private TMP_InputField amountInputField;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - RESUMEN Y TICKET
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Resumen")]
    [SerializeField] private TextMeshProUGUI totalEnteredText;

    [Header("Ticket")]
    [SerializeField] private GameObject ticketPrefab;
    [SerializeField] private Transform ticketSpawnPoint;
    [SerializeField] private Transform ticketTargetPoint;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONSTANTES
    // ══════════════════════════════════════════════════════════════════════════════

    private const int ALERT_AMOUNT = 4000;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN DE CÁMARA
    // ══════════════════════════════════════════════════════════════════════════════

    private const string CAM_START = "Iniciando Juego";
    private const string CAM_ALERT = "Actividad 5 Inicio";
    private const string CAM_BAG = "Actividad 5 Mostrar Bolsa";
    private const string CAM_TRAY = "Actividad 5 Bandeja";
    private const string CAM_INPUTS = "Actividad 5 Mostrar Inputs";
    private const string CAM_OVERVIEW = "Actividad 5 Vista General";
    private const string CAM_SUCCESS = "Actividad 5 Success";

    // ══════════════════════════════════════════════════════════════════════════════
    // ESTADO INTERNO
    // ══════════════════════════════════════════════════════════════════════════════

    private int _currentInputIndex = 0;
    private int _totalEntered = 0;

    // ══════════════════════════════════════════════════════════════════════════════
    // MÉTRICAS - LEÍDAS POR ADAPTER VÍA REFLECTION
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Contador de errores - Se incrementa cuando el usuario da mal los $4000.
    /// El MoneySpawner debe llamar a RegisterError() cuando detecta monto incorrecto.
    /// </summary>
    private int _errorCount = 0;

    /// <summary>
    /// Contador de aciertos - Se incrementa cuando el usuario completa correctamente.
    /// </summary>
    private int _successCount = 0;

    // ══════════════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void Initialize() { }

    public override void StartActivity()
    {
        base.StartActivity();

        ResetMetrics();
        ResetState();
        InitializeCommands();

        UpdateInstructionOnce(0, () =>
        {
            DOVirtual.DelayedCall(0.5f, ShowInitialAlert);
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // INICIALIZACIÓN
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void InitializeCommands()
    {
        base.InitializeCommands();

        // Desactivar todos los botones
        foreach (var button in enterButtons)
            button.gameObject.SetActive(false);
        foreach (var button in numberButtons)
            button.gameObject.SetActive(false);
        foreach (var button in enterValidationButtons)
            button.gameObject.SetActive(false);

        // Solo registrar el comando SIN customAction
        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "ENTER_",
            // customAction = ShowMoneyPanel,  ← REMOVER ESTO
            requiredActivity = "Day3_AlertaValores",
            commandButtons = enterButtons
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

    private void ResetState()
    {
        _currentInputIndex = 0;
        _totalEntered = 0;
    }

    /// <summary>
    /// Llamado por MoneySpawner cuando el usuario da un monto incorrecto.
    /// Debe configurarse en MoneySpawner para llamar este método.
    /// </summary>
    public void RegisterError()
    {
        _errorCount++;
        SoundManager.Instance.PlaySound("error");
        Debug.Log($"[AlertCashWithdrawal] Error registrado. Total: {_errorCount}");
    }

    /// <summary>
    /// Llamado cuando el usuario completa correctamente una validación.
    /// </summary>
    private void RegisterSuccess()
    {
        _successCount++;
        Debug.Log($"[AlertCashWithdrawal] Acierto registrado. Total: {_successCount}");
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FASE 1: ALERTA INICIAL
    // ══════════════════════════════════════════════════════════════════════════════

    private void ShowInitialAlert()
    {
        alertPanel.SetActive(true);

        cameraController.MoveToPosition(CAM_ALERT, () =>
        {
            AnimateButtonsSequentiallyWithActivation(enterButtons, ShowMoneyPanel);
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FASE 2: PANEL DE DINERO
    // ══════════════════════════════════════════════════════════════════════════════

    private void ShowMoneyPanel()
    {
        Debug.Log("[AlertCash] === ShowMoneyPanel ===");
        cameraController.MoveToPosition(CAM_BAG, () =>
        {
            Debug.Log("[AlertCash] CAM_BAG completado");
            bagTarget.gameObject.SetActive(true);
            UpdateInstructionOnce(1, MoveToTray);
        });
    }

    private void MoveToTray()
    {
        Debug.Log("[AlertCash] === MoveToTray ===");
        cameraController.MoveToPosition(CAM_TRAY, SetupMoneyPanel);
    }

    private void SetupMoneyPanel()
    {
        Debug.Log("[AlertCash] === SetupMoneyPanel ===");
        UpdateInstructionOnce(2, ConfigureMoneySpawner);
    }

    private void ConfigureMoneySpawner()
    {
        Debug.Log("[AlertCash] === ConfigureMoneySpawner ===");
        alertPanel.SetActive(false);
        MoneyManager.OpenMoneyPanel(moneyPanel, panelStartPosition, panelEndPosition);

        moneySpawner.SetPartialWithdrawalTexts(targetAmountText, currentAmountText, ALERT_AMOUNT);
        moneySpawner.SetCustomDeliveryTarget(bagTarget, OnMoneyDelivered, ALERT_AMOUNT);

        validateButton.onClick.RemoveAllListeners();
        validateButton.onClick.AddListener(() =>
        {
            bool isValid = moneySpawner.ValidateAlertAmountWithResult();
            if (!isValid)
            {
                RegisterError();
            }
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FASE 3: DINERO ENTREGADO - INPUTS DE DENOMINACIÓN
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Callback cuando el usuario entrega el monto correcto de $4000.
    /// </summary>
    private void OnMoneyDelivered()
    {
        // El usuario dio el monto correcto
        RegisterSuccess();
        SoundManager.Instance.PlaySound("success");

        Dictionary<float, int> counts = moneySpawner.GetDenominationCounts();
        denominationPanel.SetActive(true);
        MoneyManager.CloseMoneyPanel(moneyPanel, panelStartPosition);

        cameraController.MoveToPosition(CAM_INPUTS, () =>
        {
            UpdateInstructionOnce(3, () =>
            {
                StartInputSequence(counts);
            });
        });
    }

    private void StartInputSequence(Dictionary<float, int> denominationCounts)
    {
        _currentInputIndex = 0;
        ProceedToNextInput(denominationCounts);
    }

    private void ProceedToNextInput(Dictionary<float, int> counts)
    {
        // Si ya procesamos todas las denominaciones
        if (_currentInputIndex >= denominationsOrdered.Count)
        {
            SoundManager.Instance.PlaySound("success");
            OnAllValidationsComplete();
            return;
        }

        float denomination = denominationsOrdered[_currentInputIndex];
        int quantity = counts.ContainsKey(denomination) ? counts[denomination] : 0;

        TMP_InputField currentInput = denominationInputs[_currentInputIndex];
        amountInputField = currentInput;

        // Preparar botones para la cantidad
        string amountString = quantity.ToString();
        List<Button> buttonsToPress = GetButtonsForAmount(amountString, numberButtons);

        foreach (var btn in numberButtons)
            btn.gameObject.SetActive(false);

        currentInput.text = "";
        currentInput.gameObject.SetActive(true);

        // Secuencia: números → enter → siguiente denominación
        ActivateButtonWithSequence(buttonsToPress, 0, () =>
        {
            ActivateButtonWithSequence(enterValidationButtons, 0, () =>
            {
                if (!int.TryParse(currentInput.text, out int valueEntered))
                {
                    SoundManager.Instance.PlaySound("error");
                    return;
                }

                int denominationValue = (int)denomination;
                _totalEntered += valueEntered * denominationValue;
                UpdateTotalEnteredUI();

                _currentInputIndex++;
                ProceedToNextInput(counts);
            });
        });
    }

    /// <summary>
    /// Callback de los botones numéricos.
    /// </summary>
    public void OnNumberButtonPressed(string number)
    {
        if (amountInputField != null && amountInputField.gameObject.activeInHierarchy)
        {
            amountInputField.text += number;
        }
    }

    private void UpdateTotalEnteredUI()
    {
        if (totalEnteredText != null)
        {
            totalEnteredText.text = $"TOTAL INGRESADO: ${_totalEntered}";
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FASE 4: TICKET Y ENTREGA DE BOLSA
    // ══════════════════════════════════════════════════════════════════════════════

    private void OnAllValidationsComplete()
    {
        cameraController.MoveToPosition(CAM_OVERVIEW, () =>
        {
            UpdateInstructionOnce(4);
            InstantiateTicket(ticketPrefab, ticketSpawnPoint, ticketTargetPoint, HandleTicketDelivered);
        });
    }

    private void HandleTicketDelivered()
    {
        SoundManager.Instance.PlaySound("success");

        cameraController.MoveToPosition(CAM_BAG, () =>
        {
            UpdateInstructionOnce(5);

            // Habilitar entrega de bolsa
            bagTarget.gameObject.GetComponent<BoxCollider>().enabled = true;

            BagDelivery delivery = bagTarget.GetComponent<BagDelivery>();
            if (delivery == null)
            {
                delivery = bagTarget.AddComponent<BagDelivery>();
            }

            delivery.Initialize(bagFinal);
            delivery.OnBagDelivered += ActivityComplete;
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FINALIZACIÓN
    // ══════════════════════════════════════════════════════════════════════════════

    private void ActivityComplete()
    {
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
    // LIMPIEZA
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void OnDisable()
    {
        base.OnDisable();

        // Limpiar suscripción a BagDelivery
        if (bagTarget != null)
        {
            var delivery = bagTarget.GetComponent<BagDelivery>();
            if (delivery != null)
                delivery.OnBagDelivered -= ActivityComplete;
        }

        // Limpiar listener de validación
        if (validateButton != null)
            validateButton.onClick.RemoveAllListeners();
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // RESET
    // ══════════════════════════════════════════════════════════════════════════════

    private void ResetValues()
    {
        moneySpawner.ResetMoney();
        moneySpawner.ResetMoneyUI();
        ResetState();
    }
}