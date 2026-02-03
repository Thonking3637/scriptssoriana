using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// RoundUpCentsActivity - Actividad de Redondeo de Centavos
/// MIGRADA a LCPaymentActivityBase
/// 
/// FLUJO:
/// 1. Cliente llega con productos
/// 2. Escanear productos → Subtotal (con centavos)
/// 3. Mostrar panel de redondeo
/// 4. Pregunta de opción múltiple sobre cómo preguntar el redondeo
/// 5. Presionar Enter para confirmar
/// 6. Pago con tarjeta
/// 7. Ticket → Repetir 3 veces
/// 
/// TIPO DE EVALUACIÓN: ComboMetric
/// - Tiempo: Se mide desde que inicia la competencia
/// - Errores: Se trackean respuestas incorrectas en el diálogo de redondeo
/// - El adapter lee el campo _errorCount por reflection
/// 
/// CONFIGURACIÓN DEL ADAPTER:
/// - evaluationType: ComboMetric
/// - errorsFieldName: "_errorCount"
/// - expectedTotal: 3 (3 intentos × 1 pregunta por intento)
/// - weightAccuracy: 0.5
/// - weightSpeed: 0.3
/// - weightEfficiency: 0.2
/// - idealTimeSeconds: calibrar jugando
/// - maxAllowedErrors: 3
/// 
/// INSTRUCCIONES:
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

    // ══════════════════════════════════════════════════════════════════════════════
    // ESTADO INTERNO
    // ══════════════════════════════════════════════════════════════════════════════

    private Client currentClientComponent;
    private float totalAmount;

    // ══════════════════════════════════════════════════════════════════════════════
    // MÉTRICAS - LEÍDAS POR ADAPTER VÍA REFLECTION
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Contador de errores - El ActivityMetricsAdapter lee este campo.
    /// Configurar en Inspector: errorsFieldName = "_errorCount"
    /// Se incrementa cuando el usuario selecciona una respuesta incorrecta
    /// en el diálogo de redondeo.
    /// </summary>
    private int _errorCount = 0;

    /// <summary>
    /// Contador de aciertos - El ActivityMetricsAdapter puede leer este campo.
    /// Configurar en Inspector: successesFieldName = "_successCount"
    /// </summary>
    private int _successCount = 0;

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
        // Resetear métricas
        ResetMetrics();

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
    /// Registra un error (respuesta incorrecta en el diálogo).
    /// </summary>
    private void RegisterError()
    {
        _errorCount++;
        SoundManager.Instance.PlaySound("error");
        Debug.Log($"[RoundUpCentsActivity] Error registrado. Total errores: {_errorCount}");
    }

    /// <summary>
    /// Registra un acierto (respuesta correcta en el diálogo).
    /// </summary>
    private void RegisterSuccess()
    {
        _successCount++;
        Debug.Log($"[RoundUpCentsActivity] Acierto registrado. Total aciertos: {_successCount}");
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
                            // ✅ Respuesta correcta
                            RegisterSuccess();
                            DialogSystem.Instance.HideDialog(false);
                            ActionEnterBeforeClient();
                        },
                        () =>
                        {
                            // ❌ Respuesta incorrecta
                            RegisterError();
                            // No avanza - el usuario debe seleccionar la correcta
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
            ActivityComplete();
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
    /// Completa la actividad mostrando el resultado con el Adapter/UnifiedSummaryPanel.
    /// </summary>
    private void ActivityComplete()
    {
        StopActivityTimer();
        ResetValues();
        commandManager.commandList.Clear();

        cameraController.MoveToPosition(GetSuccessCameraPosition(), () =>
        {
            SoundManager.Instance.RestorePreviousMusic();

            var adapter = GetComponent<ActivityMetricsAdapter>();

            if (adapter != null)
            {
                adapter.NotifyActivityCompleted();
            }
            else
            {
                Debug.LogWarning("[RoundUpCentsActivity] ActivityMetricsAdapter no encontrado. Completando sin estrellas.");
                CompleteActivity();
            }
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