using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UI;

/// <summary>
/// RechargeActivity - Actividad de Recarga Telefónica
/// 
/// FLUJO:
/// 1. Cliente llega y solicita recarga telefónica
/// 2. Cajero presiona comando RECARGA_TELEFONICA
/// 3. Ingresar número de teléfono (2 veces para confirmar)
/// 4. Seleccionar compañía telefónica
/// 5. Seleccionar monto de recarga
/// 6. Repetir 3 veces → Completar actividad
/// 
/// TIPO DE EVALUACIÓN: AccuracyBased
/// - Se trackean errores en: teléfono incorrecto, compañía incorrecta, monto incorrecto
/// - El adapter lee el campo _errorCount por reflection
/// 
/// CONFIGURACIÓN DEL ADAPTER:
/// - evaluationType: AccuracyBased
/// - errorsFieldName: "_errorCount"
/// - successesFieldName: "_successCount"
/// - expectedTotal: 9 (3 intentos × 3 decisiones por intento)
/// - maxAllowedErrors: 3
/// 
/// INSTRUCCIONES:
/// 0 = Bienvenida
/// 1 = Presionar comando RECARGA
/// 2 = Ingresar número de teléfono
/// 3 = Seleccionar compañía
/// 4 = Seleccionar monto
/// 5 = Siguiente cliente / Competencia
/// </summary>
public class RechargeActivity : ActivityBase
{
    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - CLIENTE
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Cliente")]
    [SerializeField] private CustomerSpawner customerSpawner;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - UI TIMER
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("UI - Timer")]
    [SerializeField] private TextMeshProUGUI liveTimerText;
    [SerializeField] private TextMeshProUGUI successTimeText;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - PANELES DE INPUT
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Input - Panel Teléfono")]
    [SerializeField] private GameObject phoneInputPanel;
    [SerializeField] private TMP_InputField phoneInputField1;
    [SerializeField] private TMP_InputField phoneInputField2;

    [Header("Input - Botones Numéricos")]
    [SerializeField] private List<Button> numberButtons;
    [SerializeField] private List<Button> enterButtons;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - PANELES DE SELECCIÓN
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Selección - Compañía")]
    [SerializeField] private GameObject companyPanel;
    [SerializeField] private List<Button> companyButtons;

    [Header("Selección - Monto")]
    [SerializeField] private GameObject amountPanel;
    [SerializeField] private List<Button> amountButtons;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - COMANDOS
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Comando Recarga")]
    [SerializeField] private List<Button> recargaCommandButtons;

    // ══════════════════════════════════════════════════════════════════════════════
    // ESTADO INTERNO
    // ══════════════════════════════════════════════════════════════════════════════

    // Referencias cacheadas del cliente actual
    private GameObject _currentCustomer;
    private CustomerMovement _currentMovement;
    private Client _currentClient;

    // Control de intentos
    private int _currentAttempt = 0;
    private const int MAX_ATTEMPTS = 3;

    // ══════════════════════════════════════════════════════════════════════════════
    // MÉTRICAS - LEÍDAS POR ADAPTER VÍA REFLECTION
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Contador de errores - El ActivityMetricsAdapter lee este campo.
    /// Configurar en Inspector: errorsFieldName = "_errorCount"
    /// </summary>
    private int _errorCount = 0;

    /// <summary>
    /// Contador de aciertos - El ActivityMetricsAdapter puede leer este campo.
    /// Configurar en Inspector: successesFieldName = "_successCount"
    /// </summary>
    private int _successCount = 0;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN DE CÁMARA
    // ══════════════════════════════════════════════════════════════════════════════

    private const string CAM_START = "Iniciando Juego";
    private const string CAM_CLIENT = "Cliente Camera";
    private const string CAM_SHOW_RECHARGE = "Actividad 2 Mostrar Recarga";
    private const string CAM_SHOW_NUMBER = "Actividad 2 Mostrar Numero";
    private const string CAM_SHOW_COMPANY = "Actividad 2 Mostrar Company Buttons";
    private const string CAM_SUCCESS = "Actividad Recharge Success";

    // ══════════════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void Initialize() { }

    public override void StartActivity()
    {
        base.StartActivity();

        // Validar dependencias críticas
        if (customerSpawner == null)
        {
            Debug.LogError("[RechargeActivity] CustomerSpawner no está asignado.");
            return;
        }

        // Configurar timer
        activityTimerText = liveTimerText;
        activityTimeText = successTimeText;

        // Resetear métricas
        ResetMetrics();

        // Estado inicial de paneles
        HideAllPanels();

        // Configurar comandos
        InitializeCommands();

        // Iniciar flujo
        UpdateInstructionOnce(0, StartNewAttempt);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // MÉTRICAS
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Resetea los contadores de métricas al iniciar la actividad.
    /// </summary>
    private void ResetMetrics()
    {
        _errorCount = 0;
        _successCount = 0;
    }

    /// <summary>
    /// Registra un error (teléfono incorrecto, compañía incorrecta, monto incorrecto).
    /// </summary>
    private void RegisterError()
    {
        _errorCount++;
        SoundManager.Instance.PlaySound("error");
        Debug.Log($"[RechargeActivity] Error registrado. Total errores: {_errorCount}");
    }

    /// <summary>
    /// Registra un acierto (selección correcta).
    /// </summary>
    private void RegisterSuccess()
    {
        _successCount++;
        SoundManager.Instance.PlaySound("success");
        Debug.Log($"[RechargeActivity] Acierto registrado. Total aciertos: {_successCount}");
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // INICIALIZACIÓN DE COMANDOS
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void InitializeCommands()
    {
        // Ocultar botones de comando inicialmente
        foreach (var button in recargaCommandButtons)
            button.gameObject.SetActive(false);

        // Registrar comando de recarga
        CommandManager.CommandAction recargaCommand = new CommandManager.CommandAction
        {
            command = "RECARGA_TELEFONICA",
            customAction = OnRecargaCommandPressed,
            requiredActivity = "Day3_Recarga",
            commandButtons = recargaCommandButtons
        };

        commandManager.commandList.Add(recargaCommand);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FLUJO PRINCIPAL
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Inicia un nuevo intento (cliente nuevo).
    /// </summary>
    private void StartNewAttempt()
    {
        SpawnAndCacheCustomer();
        MoveCustomerAndShowDialog();
    }

    /// <summary>
    /// Spawnea cliente y cachea sus componentes.
    /// </summary>
    private void SpawnAndCacheCustomer()
    {
        _currentCustomer = customerSpawner.SpawnCustomer();

        // Intentar usar cache del spawner (optimizado)
        if (customerSpawner.TryGetCachedComponents(_currentCustomer, out var movement, out var client))
        {
            _currentMovement = movement;
            _currentClient = client;
        }
        else
        {
            // Fallback si el cache falla
            _currentMovement = _currentCustomer.GetComponent<CustomerMovement>();
            _currentClient = _currentCustomer.GetComponent<Client>();
        }

        // Generar datos aleatorios de recarga
        _currentClient.GenerateRechargeData();
    }

    /// <summary>
    /// Mueve cliente al checkout y muestra diálogo de solicitud.
    /// </summary>
    private void MoveCustomerAndShowDialog()
    {
        cameraController.MoveToPosition(CAM_START, () =>
        {
            cameraController.MoveToPosition(CAM_CLIENT, () =>
            {
                ShowRechargeRequestDialog();
            });
        });
    }

    /// <summary>
    /// Muestra el diálogo del cliente solicitando la recarga.
    /// </summary>
    private void ShowRechargeRequestDialog()
    {
        string dialogText = $"Quisiera hacer una recarga telefónica al número " +
                           $"<color=#FFA500>{_currentClient.phoneNumber}</color> de " +
                           $"<color=#FFA500>{_currentClient.phoneCompany}</color> con el monto de " +
                           $"<color=#FFA500>${_currentClient.rechargeAmount}</color>.";

        DialogSystem.Instance.ShowClientDialog(
            client: _currentClient,
            dialog: dialogText,
            onComplete: () =>
            {
                cameraController.MoveToPosition(CAM_SHOW_RECHARGE, () =>
                {
                    UpdateInstructionOnce(1);
                    ActivateButtonWithSequence(recargaCommandButtons, 0);
                });
            }
        );
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FASE 1: COMANDO RECARGA
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Callback cuando se presiona el comando RECARGA_TELEFONICA.
    /// </summary>
    private void OnRecargaCommandPressed()
    {
        SoundManager.Instance.PlaySound("success");
        StartPhoneInputPhase();
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FASE 2: INPUT DE TELÉFONO
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Inicia la fase de input del número telefónico.
    /// </summary>
    private void StartPhoneInputPhase()
    {
        phoneInputPanel.SetActive(true);

        cameraController.MoveToPosition(CAM_SHOW_NUMBER, () =>
        {
            UpdateInstructionOnce(2);

            // Preparar primer input
            PreparePhoneInput(phoneInputField1);

            // Generar secuencia de botones para el número
            GenerateNumberButtonSequence(_currentClient.phoneNumber, OnFirstNumberEntered);
        });
    }

    /// <summary>
    /// Prepara un campo de input de teléfono.
    /// </summary>
    private void PreparePhoneInput(TMP_InputField inputField)
    {
        inputField.text = "";
        inputField.ActivateInputField();
        commandManager.navigationManager.SetActiveInputField(inputField);
    }

    /// <summary>
    /// Callback cuando se ingresa el primer número.
    /// </summary>
    private void OnFirstNumberEntered()
    {
        UpdateInstructionOnce(2);

        // Preparar segundo input (confirmación)
        PreparePhoneInput(phoneInputField2);
        phoneInputField2.DeactivateInputField();
        phoneInputField2.ActivateInputField();

        // Generar secuencia para confirmar
        GenerateNumberButtonSequence(_currentClient.phoneNumber, OnSecondNumberEntered);
    }

    /// <summary>
    /// Callback cuando se ingresa el segundo número (confirmación).
    /// </summary>
    private void OnSecondNumberEntered()
    {
        // Validar que ambos números coincidan
        if (phoneInputField2.text == _currentClient.phoneNumber)
        {
            // ✅ Teléfono correcto
            RegisterSuccess();
            phoneInputPanel.SetActive(false);
            StartCompanySelectionPhase();
        }
        else
        {
            // ❌ Error: números no coinciden
            RegisterError();
            ResetPhoneInputs();
            StartPhoneInputPhase();
        }
    }

    /// <summary>
    /// Limpia los campos de input de teléfono.
    /// </summary>
    private void ResetPhoneInputs()
    {
        phoneInputField1.text = "";
        phoneInputField2.text = "";
    }

    /// <summary>
    /// Genera la secuencia de botones numéricos para ingresar un número.
    /// </summary>
    private void GenerateNumberButtonSequence(string targetNumber, System.Action onComplete)
    {
        List<Button> selectedButtons = GetButtonsForAmount(targetNumber, numberButtons);

        // Ocultar todos los botones numéricos primero
        foreach (var button in numberButtons)
            button.gameObject.SetActive(false);

        // Activar secuencia de botones + Enter
        ActivateButtonWithSequence(selectedButtons, 0, () =>
        {
            AnimateButtonsSequentiallyWithActivation(enterButtons, () =>
            {
                SoundManager.Instance.PlaySound("success");
                onComplete?.Invoke();
            });
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FASE 3: SELECCIÓN DE COMPAÑÍA
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Inicia la fase de selección de compañía telefónica.
    /// </summary>
    private void StartCompanySelectionPhase()
    {
        cameraController.MoveToPosition(CAM_SHOW_COMPANY, () =>
        {
            UpdateInstructionOnce(3);

            companyPanel.SetActive(true);
            ConfigureCompanyButtons();
        });
    }

    /// <summary>
    /// Configura los botones de compañía con la lógica de validación.
    /// </summary>
    private void ConfigureCompanyButtons()
    {
        foreach (var button in companyButtons)
        {
            button.gameObject.SetActive(true);
            button.onClick.RemoveAllListeners();

            string companyName = button.GetComponentInChildren<TextMeshProUGUI>().text;

            if (companyName == _currentClient.phoneCompany)
            {
                button.onClick.AddListener(OnCorrectCompanySelected);
            }
            else
            {
                button.onClick.AddListener(OnIncorrectCompanySelected);
            }
        }
    }

    /// <summary>
    /// Callback cuando se selecciona la compañía correcta.
    /// </summary>
    private void OnCorrectCompanySelected()
    {
        RegisterSuccess();
        companyPanel.SetActive(false);
        StartAmountSelectionPhase();
    }

    /// <summary>
    /// Callback cuando se selecciona una compañía incorrecta.
    /// </summary>
    private void OnIncorrectCompanySelected()
    {
        RegisterError();
        // No avanza - el usuario debe seleccionar la correcta
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FASE 4: SELECCIÓN DE MONTO
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Inicia la fase de selección de monto.
    /// </summary>
    private void StartAmountSelectionPhase()
    {
        cameraController.MoveToPosition(CAM_SHOW_COMPANY, () =>
        {
            UpdateInstructionOnce(4);

            amountPanel.SetActive(true);
            ConfigureAmountButtons();
        });
    }

    /// <summary>
    /// Configura los botones de monto con la lógica de validación.
    /// </summary>
    private void ConfigureAmountButtons()
    {
        foreach (var button in amountButtons)
        {
            button.gameObject.SetActive(true);
            button.onClick.RemoveAllListeners();

            string amountText = button.GetComponentInChildren<TextMeshProUGUI>().text;

            if (int.TryParse(amountText, out int amount) && amount == _currentClient.rechargeAmount)
            {
                button.onClick.AddListener(OnCorrectAmountSelected);
            }
            else
            {
                button.onClick.AddListener(OnIncorrectAmountSelected);
            }
        }
    }

    /// <summary>
    /// Callback cuando se selecciona el monto correcto.
    /// </summary>
    private void OnCorrectAmountSelected()
    {
        RegisterSuccess();
        amountPanel.SetActive(false);
        OnAttemptComplete();
    }

    /// <summary>
    /// Callback cuando se selecciona un monto incorrecto.
    /// </summary>
    private void OnIncorrectAmountSelected()
    {
        RegisterError();
        // No avanza - el usuario debe seleccionar el correcto
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // CONTROL DE INTENTOS
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Procesa la finalización de un intento.
    /// </summary>
    private void OnAttemptComplete()
    {
        _currentAttempt++;

        // Mover cliente a la salida
        _currentMovement.MoveToExit();

        // ¿Más intentos o finalizar?
        if (_currentAttempt < MAX_ATTEMPTS)
        {
            PrepareNextAttempt();
        }
        else
        {
            ActivityComplete();
        }
    }

    /// <summary>
    /// Prepara el siguiente intento con instrucción de transición.
    /// </summary>
    private void PrepareNextAttempt()
    {
        UpdateInstructionOnce(5, StartNewAttempt, StartCompetition);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // COMPETENCIA (TIMER)
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Inicia el modo competencia con música y timer.
    /// </summary>
    private void StartCompetition()
    {
        if (activityMusicClip != null)
        {
            SoundManager.Instance.SetActivityMusic(activityMusicClip, 0.2f, false);
        }

        if (liveTimerText != null)
        {
            liveTimerText.gameObject.SetActive(true);
        }

        StartActivityTimer();
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FINALIZACIÓN
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Completa la actividad mostrando el panel de éxito.
    /// </summary>
    private void ActivityComplete()
    {
        StopActivityTimer();

        // Limpiar estado
        commandManager.commandList.Clear();
        DialogSystem.Instance.HideDialog();

        cameraController.MoveToPosition(CAM_SUCCESS, () =>
        {
            SoundManager.Instance.RestorePreviousMusic();

            // Intentar usar ActivityMetricsAdapter para el sistema de 3 estrellas
            var adapter = GetComponent<ActivityMetricsAdapter>();

            if (adapter != null)
            {
                // El adapter lee _errorCount y _successCount por reflection
                adapter.NotifyActivityCompleted();
            }

        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // UTILIDADES
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Oculta todos los paneles de la actividad.
    /// </summary>
    private void HideAllPanels()
    {
        phoneInputPanel.SetActive(false);
        companyPanel.SetActive(false);
        amountPanel.SetActive(false);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // LIMPIEZA
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void OnDisable()
    {
        base.OnDisable();
    }
}