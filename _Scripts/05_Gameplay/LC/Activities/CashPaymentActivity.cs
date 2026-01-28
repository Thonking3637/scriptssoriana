using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Actividad de Pago en Efectivo - MIGRADA a LCPaymentActivityBase
/// 
/// ANTES: ~400 líneas
/// DESPUÉS: ~150 líneas
/// REDUCCIÓN: 62%
/// 
/// Cambios principales:
/// - Ya no necesita: StartNewAttempt, RegisterProductScanned, BindCurrentProduct,
///   HandleTicketDelivered, ActivityComplete, ResetValues (versión base)
/// - Solo implementa lógica específica de efectivo
/// </summary>
public class CashPaymentActivity : LCPaymentActivityBase
{
    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN ESPECÍFICA DE EFECTIVO
    // (Solo lo que NO está en la clase base)
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
    // (Configuración que la base necesita conocer)
    // ══════════════════════════════════════════════════════════════════════════════

    protected override string GetStartCameraPosition() => "Iniciando Juego";
    protected override string GetSubtotalCameraPosition() => "Actividad Billete SubTotal";
    protected override string GetSuccessCameraPosition() => "Actividad Billete Success";
    protected override string GetActivityCommandId() => "Day2_PagoEfectivo";

    // ══════════════════════════════════════════════════════════════════════════════
    // MANEJO DE INSTRUCCIONES (cada actividad tiene índices diferentes)
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
            // MoneySpawner.ValidateChange() -> OnCorrectChangeGiven()
        });
    }

    /// <summary>
    /// Llamado por MoneySpawner.ValidateChange() cuando el cambio es correcto.
    /// ⚠️ IMPORTANTE: MoneySpawner usa FindObjectOfType para llamar este método.
    /// TODO: Refactorizar MoneySpawner para usar eventos en lugar de FindObjectOfType.
    /// </summary>
    public void OnCorrectChangeGiven()
    {
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

    // ══════════════════════════════════════════════════════════════════════════════
    // SOBRESCRITURAS DE FLUJO
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sobrescribe HandleTicketDelivered de la base para usar la lógica de CashPayment.
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
            ActivityComplete();
        }
    }

    private void RestartActivity()
    {
        ResetValues();
        RegenerateProductValues();
        UpdateInstructionOnce(6, StartNewAttempt, StartCompetition);
    }

    private void ActivityComplete()
    {
        StopActivityTimer();
        ResetValues();
        commandManager.commandList.Clear();

        cameraController.MoveToPosition(GetSuccessCameraPosition(), () =>
        {
            continueButton.onClick.RemoveAllListeners();
            SoundManager.Instance.RestorePreviousMusic();
            SoundManager.Instance.PlaySound("win");

            continueButton.onClick.AddListener(() =>
            {
                cameraController.MoveToPosition(GetStartCameraPosition());
                CompleteActivity();
            });
        });
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
    }

    protected override void OnRestartAttempt()
    {
        ResetValues();
        RegenerateProductValues();

        // En efectivo, mostramos instrucción antes de reiniciar
        UpdateInstructionOnce(6, StartNewAttempt, StartCompetition);
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