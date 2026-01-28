using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Actividad de Pago con Tarjeta Omonel - MIGRADA a LCPaymentActivityBase
/// 
/// ANTES: ~400 líneas
/// DESPUÉS: ~220 líneas
/// REDUCCIÓN: ~45%
/// 
/// Flujo de instrucciones:
/// 0 = Inicio (bienvenida)
/// 1 = Subtotal
/// 2 = Mostrar tarjeta (primera posición)
/// 3 = Tarjeta segunda posición
/// 4 = Tarjeta posición final
/// 5 = Escribir monto
/// 6 = Cliente sale (si hay más intentos)
/// 7 = Reiniciar
/// </summary>
public class OmonelCardPaymentActivity : LCPaymentActivityBase
{
    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN ESPECÍFICA DE OMONEL
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Omonel - Card Interaction")]
    [SerializeField] private CardInteraction cardInteraction;

    [Header("Omonel - Botones Específicos")]
    [SerializeField] private List<Button> enterLastClicking;

    [Header("Omonel - Puntos de Cliente")]
    [SerializeField] private Transform pinEntryPoint;
    [SerializeField] private Transform checkoutPoint;

    // ══════════════════════════════════════════════════════════════════════════════
    // IMPLEMENTACIÓN DE MÉTODOS ABSTRACTOS
    // ══════════════════════════════════════════════════════════════════════════════

    protected override string GetStartCameraPosition() => "Iniciando Juego";
    protected override string GetSubtotalCameraPosition() => "Actividad Omonel SubTotal";
    protected override string GetSuccessCameraPosition() => "Actividad Omonel Success";
    protected override string GetActivityCommandId() => "Day2_PagoOmonel";

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
        ActivateCommandButtons(subtotalButtons);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // INICIALIZACIÓN
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void OnActivityInitialize()
    {
        // Inicializar texto de total si está vacío
        if (activityTotalPriceText != null && string.IsNullOrWhiteSpace(activityTotalPriceText.text))
            activityTotalPriceText.text = "$0.00";

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
        foreach (var button in enterLastClicking)
            button.gameObject.SetActive(false);
    }

    protected override void InitializeCommands()
    {
        // Desactivar botones de subtotal
        foreach (var button in subtotalButtons)
            button.gameObject.SetActive(false);

        // Comando ENTER LAST
        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "ENTERR",
            customAction = HandleEnterLast,
            requiredActivity = GetActivityCommandId(),
            commandButtons = enterLastClicking
        });

        // Comando SUBTOTAL
        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "SUBTOTAL",
            customAction = HandleSubTotal,
            requiredActivity = GetActivityCommandId(),
            commandButtons = subtotalButtons
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
    // LÓGICA ESPECÍFICA DE OMONEL - FLUJO DE TARJETA
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Después de presionar Subtotal, mostrar la tarjeta.
    /// </summary>
    protected override void OnSubtotalPressed(float totalAmount)
    {
        ShowCard();
    }

    /// <summary>
    /// Muestra la tarjeta Omonel y activa la interacción.
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
        UpdateInstructionOnce(3); // "Continúa deslizando"
        cameraController.MoveToPosition("Actividad Omonel Segunda Posicion");
    }

    /// <summary>
    /// Callback cuando la tarjeta llega a la segunda posición.
    /// </summary>
    private void HandleCardMovedToSecondPosition()
    {
        SoundManager.Instance.PlaySound("success");
        UpdateInstructionOnce(4); // "Retira la tarjeta"
        cameraController.MoveToPosition("Actividad Omonel Final Posicion");
    }

    /// <summary>
    /// Callback cuando la tarjeta es devuelta al cliente.
    /// </summary>
    private void HandleCardReturned()
    {
        if (currentCustomer == null)
        {
            Debug.LogError("OmonelCardPaymentActivity: No hay cliente asignado en HandleCardReturned().");
            return;
        }

        if (currentCustomerMovement == null)
        {
            Debug.LogError("OmonelCardPaymentActivity: El cliente no tiene CustomerMovement.");
            return;
        }

        // Mover cliente al PIN entry y activar input
        currentCustomerMovement.MoveToPinEntry(ActivatePinInput);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // LÓGICA DE INPUT DE MONTO
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Activa el input para escribir el monto.
    /// </summary>
    private void ActivatePinInput()
    {
        cameraController.MoveToPosition("Actividad Omonel Escribir Monto", () =>
        {
            UpdateInstructionOnce(5); // "Escribe el monto"
            float totalAmount = GetTotalAmount(activityTotalPriceText);
            ActivateOmonelAmountInput(totalAmount);
        });
    }

    /// <summary>
    /// Activa el input de monto específico para Omonel.
    /// </summary>
    private void ActivateOmonelAmountInput(float amount)
    {
        // Activar input field
        if (amountInputField != null)
        {
            amountInputField.gameObject.SetActive(true);
            amountInputField.text = "";
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

        // Secuencia: números -> enter last
        ActivateButtonWithSequence(selectedButtons, 0, () =>
        {
            ActivateButtonWithSequence(enterLastClicking, 0);
        });
    }

    /// <summary>
    /// Maneja el comando Enter Last - valida el monto.
    /// </summary>
    public void HandleEnterLast()
    {
        SoundManager.Instance.PlaySound("success");
        ValidateAmount();
    }

    /// <summary>
    /// Valida el monto y decide si continuar o completar.
    /// </summary>
    private void ValidateAmount()
    {
        currentAttempt++;

        if (currentCustomer == null || currentCustomerMovement == null)
        {
            Invoke(nameof(RestartActivity), 1f);
            return;
        }

        // Cliente sale
        currentCustomerMovement.MoveToExit();

        if (currentAttempt < maxAttempts)
        {
            // Más intentos disponibles
            cameraController.MoveToPosition(GetStartCameraPosition(), () =>
            {
                UpdateInstructionOnce(6); // "Preparando siguiente cliente"
                Invoke(nameof(RestartActivity), 1f);
            });
        }
        else
        {
            // Completar actividad
            ShowActivityCompletePanel();
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // RESTART Y FINALIZACIÓN
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Reinicia la actividad para el siguiente intento.
    /// </summary>
    private void RestartActivity()
    {
        ResetValues();
        cardInteraction.ResetCard();

        DOVirtual.DelayedCall(0.1f, () =>
        {
            RegenerateProductValues();
        });

        DOVirtual.DelayedCall(0.3f, () =>
        {
            UpdateInstructionOnce(7, StartNewAttempt, StartCompetition);
        });
    }

    /// <summary>
    /// Muestra el panel de éxito.
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
                cardInteraction.ResetCard();
                CompleteActivity();
            });
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // RESET Y LIMPIEZA
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void ResetValues()
    {
        base.ResetValues();

        // Reset específico de Omonel
        if (amountInputField != null)
            amountInputField.text = "";
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