using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Actividad de Pago en Efectivo - MIGRADA a LCPaymentActivityBase
/// 
/// INTEGRACIÓN CON UnifiedSummaryPanel:
/// Para habilitar el sistema de 3 estrellas:
/// 1. Agregar componente ActivityMetricsAdapter al GameObject
/// 2. Configurar en el Inspector:
///    - successesFieldName = "successCount" (cobros exitosos)
///    - errorsFieldName = "errorCount" (errores de cambio)
///    - expectedTotal = 3 (número de intentos)
/// 3. El adapter se encarga automáticamente de mostrar el panel al completar
/// 
/// FLUJO:
/// 1. Cliente llega → Escanear productos → SUBTOTAL
/// 2. Cliente paga en efectivo → Escribir monto → EFECTIVO
/// 3. Dar cambio correcto → Entregar ticket → Cliente sale
/// 4. Repetir 3 veces → Mostrar panel de éxito (o UnifiedSummaryPanel si hay adapter)
/// 
/// ERRORES POSIBLES:
/// - Dar cambio incorrecto (único error trackeable en esta actividad)
/// </summary>
public class CashPaymentActivity : LCPaymentActivityBase
{
    // ══════════════════════════════════════════════════════════════════════════════
    // MÉTRICAS PARA ActivityMetricsAdapter
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>Cantidad de cobros exitosos (para métricas)</summary>
    [HideInInspector] public int successCount = 0;

    /// <summary>Cantidad de errores de cambio (para métricas)</summary>
    [HideInInspector] public int errorCount = 0;
    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN ESPECÍFICA DE EFECTIVO
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Cash Payment - Money")]
    [SerializeField] private MoneySpawner moneySpawner;
    [SerializeField] private CustomerPayment customerPayment;

    [Header("Cash Payment - Botones Efectivo")]
    [SerializeField] private List<Button> efectivoButtons;

    [Header("Cash Payment - Money Panel")]
    [SerializeField] private GameObject moneyPanel;
    [SerializeField] private Vector2 moneyPanelStartPos;
    [SerializeField] private Vector2 moneyPanelEndPos;
    [SerializeField] private Vector2 moneyPanelHidePos;

    // ══════════════════════════════════════════════════════════════════════════════
    // IMPLEMENTACIÓN DE MÉTODOS ABSTRACTOS
    // ══════════════════════════════════════════════════════════════════════════════

    protected override string GetStartCameraPosition() => "Iniciando Juego";
    protected override string GetSubtotalCameraPosition() => "Actividad Billete SubTotal";
    protected override string GetSuccessCameraPosition() => "Actividad Billete Success";
    protected override string GetActivityCommandId() => "Day2_PagoEfectivo";

    // ══════════════════════════════════════════════════════════════════════════════
    // MANEJO DE INSTRUCCIONES
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Instrucciones de CashPayment:
    /// 0 = Inicio (bienvenida)
    /// 1 = Subtotal
    /// 2 = Recoger efectivo
    /// 3 = Escribir monto
    /// 4 = Dar cambio
    /// 5 = Ticket
    /// 6 = Reiniciar
    /// </summary>
    protected override void ShowInitialInstruction()
    {
        UpdateInstructionOnce(0, StartNewAttempt);
    }

    protected override void OnSubtotalPhaseReady()
    {
        UpdateInstructionOnce(1); // "Presiona SUBTOTAL"
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // INICIALIZACIÓN ESPECÍFICA
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void OnActivityInitialize()
    {
        // Resetear métricas al inicio de la actividad
        ResetMetrics();

        // Suscribirse al evento de dinero recolectado del cliente
        if (customerPayment != null)
        {
            customerPayment.OnAllCustomerMoneyCollected -= OnAllMoneyCollected;
            customerPayment.OnAllCustomerMoneyCollected += OnAllMoneyCollected;
        }

        // Desactivar botones de efectivo inicialmente
        foreach (var button in efectivoButtons)
        {
            button.interactable = false;
        }
    }

    protected override void InitializeCommands()
    {
        // Comando SUBTOTAL
        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "SUBTOTAL",
            customAction = HandleSubTotal,
            requiredActivity = GetActivityCommandId(),
            commandButtons = subtotalButtons
        });

        // Comando EFECTIVO
        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "EFECTIVO",
            customAction = HandleEfectivo,
            requiredActivity = GetActivityCommandId(),
            commandButtons = efectivoButtons
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // LÓGICA ESPECÍFICA DE PAGO EN EFECTIVO
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sobrescribe el comportamiento después de presionar Subtotal.
    /// En efectivo: actualiza UI de dinero y genera pago del cliente.
    /// </summary>
    protected override void OnSubtotalPressed(float totalAmount)
    {
        // Actualizar UI del spawner de dinero
        moneySpawner.UpdateTotalPurchaseText(totalAmount);

        // Mover cámara a recoger efectivo
        cameraController.MoveToPosition("Actividad Billete Recoger Efectivo", () =>
        {
            UpdateInstructionOnce(2);

            // Generar los billetes/monedas que el cliente va a dar
            customerPayment.GenerateCustomerPayment(totalAmount);
        });
    }

    /// <summary>
    /// Callback cuando el cliente ha entregado todo su dinero.
    /// </summary>
    private void OnAllMoneyCollected(float amount)
    {
        SoundManager.Instance.PlaySound("success");

        // Mover cámara a escribir el monto recibido
        cameraController.MoveToPosition("Actividad Billete Escribir Efectivo", () =>
        {
            UpdateInstructionOnce(3);

            // Activar input de monto (método de la base)
            ActivateAmountInput(amount, OnAmountInputComplete);
        });
    }

    /// <summary>
    /// Callback cuando el usuario termina de escribir el monto.
    /// </summary>
    private void OnAmountInputComplete()
    {
        // Activar botones de efectivo
        foreach (var button in efectivoButtons)
        {
            button.interactable = true;
        }

        AnimateButtonsSequentiallyWithActivation(efectivoButtons);
    }

    /// <summary>
    /// Maneja el comando EFECTIVO - procede a dar cambio.
    /// </summary>
    public void HandleEfectivo()
    {
        SoundManager.Instance.PlaySound("success");

        // Mover cámara a dar cambio
        cameraController.MoveToPosition("Actividad Billete Dar Cambio", () =>
        {
            UpdateInstructionOnce(4); // "Selecciona el cambio correcto"

            // Abrir panel de dinero para dar cambio
            MoneyManager.OpenMoneyPanel(moneyPanel, moneyPanelStartPos, moneyPanelEndPos);

            // El flujo continúa cuando el usuario valida el cambio correcto
            // MoneySpawner.ValidateChange() -> OnCorrectChangeGiven() o OnIncorrectChangeGiven()
        });
    }

    /// <summary>
    /// Llamado por MoneySpawner.ValidateChange() cuando el cambio es correcto.
    /// ⚠️ IMPORTANTE: MoneySpawner usa FindObjectOfType para llamar este método.
    /// </summary>
    public void OnCorrectChangeGiven()
    {
        // ✅ Registrar éxito para métricas
        successCount++;

        // Cerrar panel de dinero
        MoneyManager.CloseMoneyPanel(moneyPanel, moneyPanelHidePos);
        SoundManager.Instance.PlaySound("success");

        // Mover cámara y cliente a recibir ticket
        cameraController.MoveToPosition("Actividad Billete Dar Ticket", () =>
        {
            UpdateInstructionOnce(5); // "Entrega el ticket"

            // Generar ticket (usa método de la base)
            InstantiateTicket(ticketPrefab, ticketSpawnPoint, ticketTargetPoint, HandleTicketDelivered);
        });
    }

    /// <summary>
    /// Llamado cuando el usuario da un cambio incorrecto.
    /// Este método debe ser llamado por MoneySpawner cuando ValidateChange() falla.
    /// 
    /// NOTA: Para habilitar esto, MoneySpawner.ValidateChange() debe ser modificado para:
    /// 1. Buscar CashPaymentActivity
    /// 2. Llamar OnIncorrectChangeGiven() cuando el cambio es incorrecto
    /// 
    /// Ejemplo de modificación en MoneySpawner.ValidateChange():
    /// <code>
    /// if (!Mathf.Approximately(givenAmount, changeDue))
    /// {
    ///     SoundManager.Instance.PlaySound("error");
    ///     var activity = FindObjectOfType<CashPaymentActivity>();
    ///     if (activity != null) activity.OnIncorrectChangeGiven();
    ///     return;
    /// }
    /// </code>
    /// </summary>
    public void OnIncorrectChangeGiven()
    {
        // ✅ Registrar error para métricas
        errorCount++;
        Debug.Log($"[CashPayment] Error de cambio registrado. Total errores: {errorCount}");
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // SOBRESCRITURAS DE FLUJO
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sobrescribe HandleTicketDelivered para lógica específica de CashPayment.
    /// </summary>
    protected override void HandleTicketDelivered()
    {
        SoundManager.Instance.PlaySound("success");
        currentAttempt++;

        // Cliente sale
        currentCustomerMovement?.MoveToExit();

        // Decidir siguiente paso
        if (currentAttempt < maxAttempts)
        {
            RestartActivity();
        }
        else
        {
            // Usar el método de la base que detecta ActivityMetricsAdapter automáticamente
            OnAllAttemptsComplete();
        }
    }

    private void RestartActivity()
    {
        ResetValues();
        RegenerateProductValues();

        // StartCompetition solo se llama después del primer intento (tutorial → práctica)
        // En intentos siguientes, la música ya está sonando
        if (currentAttempt == 1)
        {
            // Primer restart: iniciar modo práctica con música y timer
            UpdateInstructionOnce(6, StartNewAttempt, StartCompetition);
        }
        else
        {
            // Restarts siguientes: solo continuar, música ya suena
            UpdateInstructionOnce(6, StartNewAttempt);
        }
    }

    protected override void ResetValues()
    {
        // Llamar reset de la base (scanner, textos, etc.)
        base.ResetValues();

        // Reset específico de efectivo
        if (moneySpawner != null)
            moneySpawner.ResetMoneyUI();

        if (customerPayment != null)
            customerPayment.ResetCustomerPayment();

        // NO resetear successCount ni errorCount aquí - se acumulan durante toda la actividad
    }

    /// <summary>
    /// Resetea las métricas al inicio de la actividad.
    /// Llamar en StartActivity o OnActivityInitialize.
    /// </summary>
    private void ResetMetrics()
    {
        successCount = 0;
        errorCount = 0;
    }

    protected override void OnRestartAttempt()
    {
        ResetValues();
        RegenerateProductValues();

        // Solo mostrar instrucción - NO reiniciar música
        UpdateInstructionOnce(6, StartNewAttempt);
    }

    /// <summary>
    /// Inicia la fase de competencia con música y timer.
    /// IMPORTANTE: Usar restartIfSame = true para reiniciar la música entre intentos.
    /// </summary>
    protected override void StartCompetition()
    {
        if (activityMusicClip != null)
        {
            // ✅ FIX: Usar restartIfSame = true para reiniciar música entre intentos
            SoundManager.Instance.SetActivityMusic(activityMusicClip, 0.2f, true);
        }

        if (liveTimerText != null)
        {
            liveTimerText.gameObject.SetActive(true);
        }

        StartActivityTimer();
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // LIMPIEZA
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void OnDisable()
    {
        base.OnDisable();

        // Desuscribirse del evento de dinero
        if (customerPayment != null)
            customerPayment.OnAllCustomerMoneyCollected -= OnAllMoneyCollected;
    }
}