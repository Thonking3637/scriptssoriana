using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Actividad de Canje de Puntos - MIGRADA a LCPaymentActivityBase
/// 
/// ANTES: ~350 líneas
/// DESPUÉS: ~250 líneas
/// REDUCCIÓN: ~29%
/// 
/// Flujo de instrucciones:
/// 0 = Inicio (bienvenida)
/// 1 = Subtotal
/// 2 = Mostrar tarjeta
/// 3 = Tarjeta segunda posición
/// 4 = Tarjeta posición final / WaitBeforePay
/// 5 = Panel de pago (P+A, Enter)
/// 6 = Aplicar descuento
/// 7 = Ticket
/// 8 = Reiniciar
/// 
/// CONFIGURACIÓN ActivityMetricsAdapter:
/// - successesFieldName: "currentAttempt"
/// - errorsFieldName: (vacío)
/// - expectedTotal: 3
/// - evaluationType: TimeBased
/// - idealTimeSeconds: 120
/// </summary>
public class RedeemPointsActivity : LCPaymentActivityBase
{
    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN ESPECÍFICA DE CANJE
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Redeem - Card Interaction")]
    [SerializeField] private CardInteraction cardInteraction;

    [Header("Redeem - UI Específica")]
    [SerializeField] private GameObject paymentPanel;

    [Header("Redeem - Botones Específicos")]
    [SerializeField] private List<Button> paButtons;
    [SerializeField] private List<Button> enterButtons;
    [SerializeField] private List<Button> efectivoButtons;

    // ══════════════════════════════════════════════════════════════════════════════
    // IMPLEMENTACIÓN DE MÉTODOS ABSTRACTOS
    // ══════════════════════════════════════════════════════════════════════════════

    protected override string GetStartCameraPosition() => "Iniciando Juego";
    protected override string GetSubtotalCameraPosition() => "Actividad Canjeo SubTotal";
    protected override string GetSuccessCameraPosition() => "Actividad Canjeo Success";
    protected override string GetActivityCommandId() => "Day2_CanjeoPuntos";

    // ══════════════════════════════════════════════════════════════════════════════
    // MANEJO DE INSTRUCCIONES
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void ShowInitialInstruction()
    {
        // RedeemPoints incrementa currentAttempt al inicio del intento
        UpdateInstructionOnce(0, CheckAndStartNewAttempt);
    }

    protected override void OnSubtotalPhaseReady()
    {
        UpdateInstructionOnce(1); // "Presiona SUBTOTAL"
        ActivateCommandButtons(subtotalButtons);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // INICIALIZACIÓN
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void Start()
    {
        base.Start();
        if (paymentPanel != null)
            paymentPanel.SetActive(false);
    }

    protected override void OnActivityInitialize()
    {
        // Suscribirse a eventos de CardInteraction
        if (cardInteraction != null)
        {
            cardInteraction.OnCardMovedToFirstPosition -= HandleCardArrived;
            cardInteraction.OnCardMovedToSecondPosition -= HandleCardMovedToSecondPosition;
            cardInteraction.OnCardReturned -= HandleCardReturned;

            cardInteraction.OnCardMovedToFirstPosition += HandleCardArrived;
            cardInteraction.OnCardMovedToSecondPosition += HandleCardMovedToSecondPosition;
            cardInteraction.OnCardReturned += HandleCardReturned;
        }

        // Desactivar botones específicos
        foreach (var button in paButtons)
            button.gameObject.SetActive(false);
        foreach (var button in enterButtons)
            button.gameObject.SetActive(false);
        foreach (var button in efectivoButtons)
            button.gameObject.SetActive(false);
    }

    protected override void InitializeCommands()
    {
        // Desactivar botones de subtotal
        foreach (var button in subtotalButtons)
            button.gameObject.SetActive(false);

        // Comando SUBTOTAL
        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "SUBTOTAL",
            customAction = () =>
            {
                ActivateCommandButtons(subtotalButtons);
                HandleSubTotal();
            },
            requiredActivity = GetActivityCommandId(),
            commandButtons = subtotalButtons
        });

        // Comando P+A
        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "P+A",
            customAction = HandlePACommand,
            requiredActivity = GetActivityCommandId(),
            commandButtons = paButtons
        });

        // Comando ENTER
        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "ENTER",
            customAction = HandleEnterCommand,
            requiredActivity = GetActivityCommandId(),
            commandButtons = enterButtons
        });

        // Comando EFECTIVO
        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "EFECTIVO",
            customAction = HandleEfectivoCommand,
            requiredActivity = GetActivityCommandId(),
            commandButtons = efectivoButtons
        });
    }

    /// <summary>
    /// Override para usar EnsureProductData después de spawnear.
    /// </summary>
    protected override void OnAfterProductSpawn(GameObject product)
    {
        EnsureProductData(product);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // LÓGICA ESPECÍFICA DE CANJE - FLUJO DE TARJETA
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Después de presionar Subtotal, mostrar la tarjeta.
    /// </summary>
    protected override void OnSubtotalPressed(float totalAmount)
    {
        ShowCard();
    }

    /// <summary>
    /// Muestra la tarjeta y activa la interacción.
    /// </summary>
    private void ShowCard()
    {
        SoundManager.Instance.PlaySound("success");
        cameraController.MoveToPosition("Actividad Omonel Primera Posicion", () =>
        {
            UpdateInstructionOnce(2); // "Desliza la tarjeta"
            cardInteraction.gameObject.SetActive(true);
        });
    }

    /// <summary>
    /// Callback cuando la tarjeta llega a la primera posición.
    /// </summary>
    private void HandleCardArrived()
    {
        cameraController.MoveToPosition("Actividad Omonel Segunda Posicion");
        UpdateInstructionOnce(3);
    }

    /// <summary>
    /// Callback cuando la tarjeta llega a la segunda posición.
    /// </summary>
    private void HandleCardMovedToSecondPosition()
    {
        SoundManager.Instance.PlaySound("success");
        UpdateInstructionOnce(4);
        WaitBeforePay();
    }

    /// <summary>
    /// Espera antes de mostrar opciones de pago.
    /// </summary>
    private void WaitBeforePay()
    {
        cameraController.MoveToPosition("Actividad Canjeo PA", () =>
        {
            ActivateCommandButtons(paButtons);
            AnimateButtonsSequentiallyWithActivation(paButtons, HandlePACommand);
        });
    }

    /// <summary>
    /// Callback cuando la tarjeta es devuelta.
    /// </summary>
    private void HandleCardReturned()
    {
        if (currentCustomer == null)
        {
            Debug.LogError("RedeemPointsActivity: No hay cliente asignado en HandleCardReturned().");
            return;
        }

        if (currentCustomerMovement == null)
        {
            Debug.LogError("RedeemPointsActivity: El cliente no tiene CustomerMovement.");
            return;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // LÓGICA DE PAGO CON PUNTOS
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Maneja el comando P+A.
    /// </summary>
    public void HandlePACommand()
    {
        SoundManager.Instance.PlaySound("success");
        cameraController.MoveToPosition("Actividad Canjeo Enter", () =>
        {
            UpdateInstructionOnce(5);
            paymentPanel.SetActive(true);
            AnimateButtonsSequentiallyWithActivation(enterButtons);
        });
    }

    /// <summary>
    /// Maneja el comando Enter - aplica el descuento.
    /// </summary>
    public void HandleEnterCommand()
    {
        SoundManager.Instance.PlaySound("success");
        UpdateInstructionOnce(6);
        ApplyPurchaseDeduction();
    }

    /// <summary>
    /// Aplica el descuento del 100% por canje de puntos.
    /// </summary>
    private void ApplyPurchaseDeduction()
    {
        activityProductsText.text += "\n100% de descuento aplicado";
        activityTotalPriceText.text = "$0.00";
        paymentPanel.SetActive(false);
        StartCoroutine(WaitForCashCommand());
    }

    /// <summary>
    /// Espera y muestra el comando de efectivo.
    /// </summary>
    private IEnumerator WaitForCashCommand()
    {
        yield return new WaitForSeconds(1f);
        cameraController.MoveToPosition("Actividad Canjeo PA", () =>
        {
            ActivateCommandButtons(efectivoButtons);
            AnimateButtonsSequentiallyWithActivation(efectivoButtons);
        });
    }

    /// <summary>
    /// Maneja el comando Efectivo - genera el ticket.
    /// </summary>
    public void HandleEfectivoCommand()
    {
        cameraController.MoveToPosition("Actividad Canjeo Ticket Instantiante", () =>
        {
            SpawnRedeemTicket();
        });
    }

    /// <summary>
    /// Genera el ticket de canje.
    /// </summary>
    private void SpawnRedeemTicket()
    {
        UpdateInstructionOnce(7);
        SoundManager.Instance.PlaySound("success");
        InstantiateTicket(ticketPrefab, ticketSpawnPoint, ticketTargetPoint, OnTicketDelivered);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // TICKET Y FINALIZACIÓN
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Callback cuando el ticket es entregado.
    /// </summary>
    private void OnTicketDelivered()
    {
        SoundManager.Instance.PlaySound("success");
        currentCustomerMovement?.MoveToExit();
        RestartActivity();
    }

    /// <summary>
    /// Reinicia la actividad.
    /// </summary>
    private void RestartActivity()
    {
        ResetValues();
        cardInteraction.ResetCard();
        RegenerateProductValues();

        if (currentAttempt == 1)
        {
            UpdateInstructionOnce(8, CheckAndStartNewAttempt, StartCompetition);
        }
        else
        {
            UpdateInstructionOnce(8, CheckAndStartNewAttempt);
        }
    }

    /// <summary>
    /// Verifica si hay más intentos disponibles antes de iniciar.
    /// En RedeemPoints, el conteo de intentos se hace AL INICIO del intento.
    /// </summary>
    private void CheckAndStartNewAttempt()
    {
        if (currentAttempt >= maxAttempts)
        {
            // ✅ ÚNICO CAMBIO: Usar OnAllAttemptsComplete() en vez de ShowActivityCompletePanel()
            OnAllAttemptsComplete();
            return;
        }

        currentAttempt++;
        StartNewAttempt();
    }

    /// <summary>
    /// Override de StartNewAttempt para no incrementar currentAttempt aquí
    /// (ya se incrementa en CheckAndStartNewAttempt).
    /// </summary>
    protected override void StartNewAttempt()
    {
        scannedCount = 0;

        currentCustomer = customerSpawner.SpawnCustomer();
        currentCustomerMovement = currentCustomer.GetComponent<CustomerMovement>();

        cameraController.MoveToPosition(GetStartCameraPosition(), () =>
        {
            currentCustomerMovement.MoveToCheckout(() =>
            {
                SpawnAndBindProduct();
                OnCustomerReady();
            });
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // RESET Y LIMPIEZA
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void ResetValues()
    {
        base.ResetValues();

        // Reset específico de canje
        if (paymentPanel != null)
            paymentPanel.SetActive(false);
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        // Desuscribirse de eventos de CardInteraction
        if (cardInteraction != null)
        {
            cardInteraction.OnCardMovedToFirstPosition -= HandleCardArrived;
            cardInteraction.OnCardMovedToSecondPosition -= HandleCardMovedToSecondPosition;
            cardInteraction.OnCardReturned -= HandleCardReturned;
        }
    }
}