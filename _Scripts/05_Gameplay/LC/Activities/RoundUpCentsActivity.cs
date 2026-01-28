using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Actividad de Redondeo de Centavos - MIGRADA a LCPaymentActivityBase
/// 
/// ANTES: ~380 líneas
/// DESPUÉS: ~280 líneas
/// REDUCCIÓN: ~26% (menos que otras porque tiene mucha lógica específica de diálogos)
/// 
/// Flujo de instrucciones:
/// 0 = Inicio (bienvenida)
/// 1 = Subtotal
/// 2 = Mostrar panel de redondeo
/// 3 = Presionar Enter
/// 4 = Escribir monto
/// 5 = Card command
/// 6 = Ticket
/// 7 = Reiniciar
/// </summary>
public class RoundUpCentsActivity : LCPaymentActivityBase
{
    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN ESPECÍFICA DE REDONDEO
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("RoundUp - UI Específica")]
    [SerializeField] private TextMeshProUGUI roundUpQuestionText;
    [SerializeField] private GameObject roundUpPanel;

    [Header("RoundUp - Botones Específicos")]
    [SerializeField] private List<Button> enterButtons;
    [SerializeField] private List<Button> commandCardButtons;
    [SerializeField] private List<Button> enterLastClicking;

    // Estado específico
    private Client currentClientComponent;
    private float totalAmount;

    // ══════════════════════════════════════════════════════════════════════════════
    // IMPLEMENTACIÓN DE MÉTODOS ABSTRACTOS
    // ══════════════════════════════════════════════════════════════════════════════

    protected override string GetStartCameraPosition() => "Iniciando Juego";
    protected override string GetSubtotalCameraPosition() => "Actividad 3 Subtotal";
    protected override string GetSuccessCameraPosition() => "Actividad 3 Success";
    protected override string GetActivityCommandId() => "Day3_RedondeoCentavos";

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
        // Desactivar botones específicos
        foreach (var button in enterButtons)
            button.gameObject.SetActive(false);
        foreach (var button in enterLastClicking)
            button.gameObject.SetActive(false);
        foreach (var button in commandCardButtons)
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
            customAction = HandleSubTotal,
            requiredActivity = GetActivityCommandId(),
            commandButtons = subtotalButtons
        });

        // Comando ENTER (para confirmar redondeo)
        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "ENTER",
            customAction = HandleEnter,
            requiredActivity = GetActivityCommandId(),
            commandButtons = enterButtons
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // OVERRIDE DE SPAWN - Aplicar precio decimal aleatorio
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void OnCustomerReady()
    {
        // Guardar referencia al componente Client
        currentClientComponent = currentCustomer.GetComponent<Client>();
    }

    protected override void OnAfterProductSpawn(GameObject product)
    {
        ApplyRandomDecimalToPrice(product);
    }

    /// <summary>
    /// Aplica un precio decimal aleatorio al producto para simular centavos.
    /// </summary>
    private void ApplyRandomDecimalToPrice(GameObject product)
    {
        if (product == null) return;

        DragObject drag = product.GetComponent<DragObject>();
        if (drag != null && drag.productData != null)
        {
            float randomDecimal = 0f;

            while (randomDecimal == 0f)
            {
                randomDecimal = Mathf.Round(Random.Range(0.01f, 0.99f) * 100f) / 100f;
            }

            drag.productData.price += randomDecimal;
            drag.productData.price = Mathf.Round(drag.productData.price * 100f) / 100f;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // LÓGICA ESPECÍFICA DE REDONDEO
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Después de presionar Subtotal, mostrar panel de redondeo y preguntar al cliente.
    /// </summary>
    protected override void OnSubtotalPressed(float amount)
    {
        totalAmount = amount;
        float rounded = Mathf.Ceil(totalAmount);
        float roundUpAmount = Mathf.Round((rounded - totalAmount) * 100f) / 100f;

        // Mostrar panel de redondeo
        roundUpPanel.SetActive(true);
        roundUpQuestionText.text = $"¿Redondear ${roundUpAmount:F2}?";

        UpdateInstructionOnce(2, () =>
        {
            Invoke(nameof(AskClientAboutRounding), 1f);
        });
    }

    /// <summary>
    /// Pregunta al cliente sobre el redondeo usando DialogSystem.
    /// </summary>
    private void AskClientAboutRounding()
    {
        float rounded = Mathf.Ceil(totalAmount);
        float roundUpAmount = Mathf.Round((rounded - totalAmount) * 100f) / 100f;
        string roundUpText = roundUpAmount.ToString("F2");

        cameraController.MoveToPosition("Actividad 3 Cliente Camera", () =>
        {
            DialogSystem.Instance.ShowClientDialog(
                currentClientComponent,
                "Hola!, ¿Cuánto es el total de mi compra?",
                () =>
                {
                    DialogSystem.Instance.ShowClientDialogWithOptions(
                        "¿Cómo deberías preguntar por el redondeo de centavos?",
                        new List<string>
                        {
                            $"¿Desea donar la cantidad de ${roundUpText} a una fundación?",
                            "¿Quiere redondear su total?",
                            "¿Desea agregar más dinero a su cuenta?",
                            "¿Redondeamos como siempre?"
                        },
                        $"¿Desea donar la cantidad de ${roundUpText} a una fundación?",
                        () =>
                        {
                            // Respuesta correcta
                            DialogSystem.Instance.HideDialog(false);
                            ActionEnterBeforeClient();
                        },
                        () =>
                        {
                            // Respuesta incorrecta
                            SoundManager.Instance.PlaySound("error");
                        }
                    );
                });
        });
    }

    /// <summary>
    /// Muestra el botón Enter para confirmar el redondeo.
    /// </summary>
    private void ActionEnterBeforeClient()
    {
        cameraController.MoveToPosition("Actividad 3 Presionar Enter", () =>
        {
            UpdateInstructionOnce(3);
            ActivateCommandButtons(enterButtons);
            ActivateButtonWithSequence(enterButtons, 0);
        });
    }

    /// <summary>
    /// Maneja el comando Enter - actualiza el total redondeado y pregunta método de pago.
    /// </summary>
    public void HandleEnter()
    {
        roundUpPanel.SetActive(false);
        float roundedTotal = Mathf.Ceil(totalAmount);
        activityTotalPriceText.text = $"${roundedTotal:F2}";

        cameraController.MoveToPosition("Actividad 3 Cliente Camera", () =>
        {
            DialogSystem.Instance.ShowClientDialog(
                "Tu",
                dialog: "¿Con qué desea pagar?",
                onComplete: () =>
                {
                    DialogSystem.Instance.ShowClientDialog(
                        currentClientComponent,
                        dialog: "Con tarjeta",
                        onComplete: () =>
                        {
                            DialogSystem.Instance.HideDialog(false);
                            ActivateRoundUpAmountInput(roundedTotal);
                        });
                });
        });
    }

    /// <summary>
    /// Activa el input de monto para el pago con tarjeta.
    /// </summary>
    private void ActivateRoundUpAmountInput(float amount)
    {
        SoundManager.Instance.PlaySound("success");

        cameraController.MoveToPosition("Actividad Tarjeta Escribir Monto");

        if (amountInputField != null)
        {
            amountInputField.text = string.Empty;
            amountInputField.gameObject.SetActive(true);
            amountInputField.DeactivateInputField();
            amountInputField.ActivateInputField();
        }

        string amountString = ((int)amount).ToString() + "00";
        List<Button> selectedButtons = GetButtonsForAmount(amountString, numberButtons);

        foreach (var button in numberButtons)
            button.gameObject.SetActive(false);

        UpdateInstructionOnce(4, () =>
        {
            ActivateButtonWithSequence(commandCardButtons, 0, () =>
            {
                SoundManager.Instance.PlaySound("success");
                UpdateInstructionOnce(5, () =>
                {
                    ActivateButtonWithSequence(selectedButtons, 0, () =>
                    {
                        ActivateButtonWithSequence(enterLastClicking, 0, () =>
                        {
                            SoundManager.Instance.PlaySound("success");
                            MoveClientAndGenerateTicket();
                        });
                    });
                });
            });
        });
    }

    /// <summary>
    /// Mueve el cliente al PIN entry y genera el ticket.
    /// </summary>
    private void MoveClientAndGenerateTicket()
    {
        cameraController.MoveToPosition("Actividad 3 Tarjeta Mirar Cliente", () =>
        {
            if (currentCustomerMovement != null)
            {
                currentCustomerMovement.MoveToPinEntry(() =>
                {
                    UpdateInstructionOnce(6, () =>
                    {
                        SoundManager.Instance.PlaySound("success");
                        InstantiateTicket(ticketPrefab, ticketSpawnPoint, ticketTargetPoint, OnTicketDelivered);
                    });
                });
            }
        });
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
        currentAttempt++;

        currentCustomerMovement?.MoveToExit();

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
        UpdateInstructionOnce(7, StartNewAttempt, StartCompetition);
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

        // Reset específico de redondeo
        totalAmount = 0f;
        if (roundUpPanel != null)
            roundUpPanel.SetActive(false);
    }
}