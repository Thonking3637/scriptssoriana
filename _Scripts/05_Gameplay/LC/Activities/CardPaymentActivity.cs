using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Actividad de Pago con Tarjeta - MIGRADA a LCPaymentActivityBase
/// 
/// ANTES: ~350 líneas
/// DESPUÉS: ~180 líneas
/// REDUCCIÓN: ~48%
/// 
/// Flujo de instrucciones:
/// 0 = Inicio (bienvenida)
/// 1 = Subtotal
/// 2 = Presionar Enter Amount
/// 3 = Escribir monto
/// 4 = Mirar cliente
/// 5 = Ticket
/// 6 = Reiniciar (si aplica)
/// </summary>
public class CardPaymentActivity : LCPaymentActivityBase
{
    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN ESPECÍFICA DE TARJETA
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Card Payment - Botones Específicos")]
    [SerializeField] private List<Button> enterAmountButtons;
    [SerializeField] private List<Button> enterLastClicking;

    [Header("Card Payment - Puntos de Cliente")]
    [SerializeField] private Transform pinEntryPoint;
    [SerializeField] private Transform checkoutPoint;

    // ══════════════════════════════════════════════════════════════════════════════
    // IMPLEMENTACIÓN DE MÉTODOS ABSTRACTOS
    // ══════════════════════════════════════════════════════════════════════════════

    protected override string GetStartCameraPosition() => "Iniciando Juego";
    protected override string GetSubtotalCameraPosition() => "Actividad Tarjeta SubTotal";
    protected override string GetSuccessCameraPosition() => "Actividad Tarjeta Success";
    protected override string GetActivityCommandId() => "Day2_PagoTarjeta";

    // ══════════════════════════════════════════════════════════════════════════════
    // MANEJO DE INSTRUCCIONES
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void ShowInitialInstruction()
    {
        UpdateInstructionOnce(0, StartNewAttempt);
    }

    protected override void OnSubtotalPhaseReady()
    {
        UpdateInstructionOnce(1); // "Presiona SUBTOTAL"
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // INICIALIZACIÓN
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void OnActivityInitialize()
    {
        // Desactivar botones específicos de tarjeta
        foreach (var button in enterAmountButtons)
            button.gameObject.SetActive(false);
        foreach (var button in enterLastClicking)
            button.gameObject.SetActive(false);
    }

    protected override void InitializeCommands()
    {
        // Desactivar botones de subtotal (heredados de la base)
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

        // Comando ENTER AMOUNT (T+B+ENTERR)
        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "T+B+ENTERR",
            customAction = HandleEnterAmount,
            requiredActivity = GetActivityCommandId(),
            commandButtons = enterAmountButtons
        });

        // Comando ENTER LAST
        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "ENTERRR",
            customAction = HandleEnterLast,
            requiredActivity = GetActivityCommandId(),
            commandButtons = enterLastClicking
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // LÓGICA ESPECÍFICA DE PAGO CON TARJETA
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Después de presionar Subtotal, mostrar botón de Enter Amount.
    /// </summary>
    protected override void OnSubtotalPressed(float totalAmount)
    {
        cameraController.MoveToPosition("Actividad Tarjeta SubTotal Pressed", () =>
        {
            UpdateInstructionOnce(2); // "Presiona ENTER AMOUNT"
            ActivateButtonWithSequence(enterAmountButtons, 0, HandleEnterAmount);
        });
    }

    /// <summary>
    /// Maneja el comando Enter Amount - activa input de monto.
    /// </summary>
    public void HandleEnterAmount()
    {
        SoundManager.Instance.PlaySound("success");
        float totalAmount = GetTotalAmount(activityTotalPriceText);
        ActivateCardAmountInput(totalAmount);
    }

    /// <summary>
    /// Activa el input de monto específico para tarjeta.
    /// </summary>
    private void ActivateCardAmountInput(float amount)
    {
        SoundManager.Instance.PlaySound("success");

        cameraController.MoveToPosition("Actividad Tarjeta Escribir Monto");
        UpdateInstructionOnce(3); // "Escribe el monto"

        // Activar input field
        if (amountInputField != null)
        {
            amountInputField.text = "";
            amountInputField.gameObject.SetActive(true);
            amountInputField.DeactivateInputField();
            amountInputField.ActivateInputField();
        }

        // Preparar botones numéricos
        string amountString = ((int)amount).ToString() + "00";
        List<Button> selectedButtons = GetButtonsForAmount(amountString, numberButtons);

        foreach (var button in numberButtons)
        {
            button.gameObject.SetActive(false);
        }

        // Secuencia: números -> enter last -> mover cliente
        ActivateButtonWithSequence(selectedButtons, 0, () =>
        {
            ActivateButtonWithSequence(enterLastClicking, 0, () =>
            {
                UpdateInstructionOnce(4, () => // "Mira al cliente"
                {
                    MoveClientToPinEntry();
                });
            });
        });
    }

    /// <summary>
    /// Mueve el cliente al punto de PIN y genera el ticket.
    /// </summary>
    private void MoveClientToPinEntry()
    {
        cameraController.MoveToPosition("Actividad Tarjeta Mirar Cliente", () =>
        {
            if (currentCustomerMovement != null)
            {
                currentCustomerMovement.MoveToPinEntry(() =>
                {
                    UpdateInstructionOnce(5, () => // "Entrega el ticket"
                    {
                        InstantiateTicket(ticketPrefab, ticketSpawnPoint, ticketTargetPoint, OnTicketDelivered);
                    });
                });
            }
        });
    }

    /// <summary>
    /// Callback para el comando Enter Last (no hace nada especial).
    /// </summary>
    public void HandleEnterLast()
    {
        // No necesita lógica adicional, el flujo continúa en ActivateCardAmountInput
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // MANEJO DE TICKET Y FINALIZACIÓN
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Callback cuando el ticket es entregado.
    /// </summary>
    private void OnTicketDelivered()
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
            ShowActivityCompletePanel();
        }
    }

    /// <summary>
    /// Reinicia la actividad para el siguiente intento.
    /// </summary>
    private void RestartActivity()
    {
        ResetValues();
        RegenerateProductValues();
        UpdateInstructionOnce(6, StartNewAttempt, StartCompetition);
    }

    /// <summary>
    /// Muestra el panel de éxito al completar todos los intentos.
    /// </summary>
    private void ShowActivityCompletePanel()
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

    // ══════════════════════════════════════════════════════════════════════════════
    // RESET
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void ResetValues()
    {
        base.ResetValues();

        // Reset específico de tarjeta (si hay algo adicional)
    }
}