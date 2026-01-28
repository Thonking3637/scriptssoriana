using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Actividad de Cliente Molesto (Velocidad) - MIGRADA a LCPaymentActivityBase
/// 
/// ANTES: ~450 líneas
/// DESPUÉS: ~380 líneas
/// REDUCCIÓN: ~16% (menos que otras porque tiene mucha lógica específica del reto)
/// 
/// Flujo especial:
/// 1. Escanear 3 productos iniciales
/// 2. Cliente se queja → Diálogo con opciones
/// 3. Reto cronometrado: escanear N productos en M segundos
/// 4. Si falla: panel de retry
/// 5. Si pasa: subtotal → pago con tarjeta → ticket
/// 
/// Flujo de instrucciones:
/// 0 = Inicio
/// 1 = Diálogo cliente molesto
/// 2 = Inicio del reto cronometrado
/// 3 = Subtotal
/// 4 = Card command
/// 5 = Escribir monto
/// 6 = Ticket
/// 7 = Reiniciar
/// </summary>
public class AngryClientVelocity : LCPaymentActivityBase
{
    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN ESPECÍFICA DE ANGRY VELOCITY
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("AngryVelocity - UI del Reto")]
    [SerializeField] private TextMeshProUGUI scannedCountText;
    [SerializeField] private TextMeshProUGUI remainingTimeText;
    [SerializeField] private GameObject failPanel;
    [SerializeField] private Button retryButton;

    [Header("AngryVelocity - Botones Específicos")]
    [SerializeField] private List<Button> commandCardButtons;
    [SerializeField] private List<Button> enterButtons;
    [SerializeField] private List<Button> enterLastClicking;

    // Estado del reto
    private Client currentClientComponent;
    private int totalToScan;
    private float currentTime;
    private float maxTime;
    private bool timerActive = false;
    private bool challengeStarted = false;
    private Coroutine challengeTimerCoroutine;

    // ══════════════════════════════════════════════════════════════════════════════
    // IMPLEMENTACIÓN DE MÉTODOS ABSTRACTOS
    // ══════════════════════════════════════════════════════════════════════════════

    protected override string GetStartCameraPosition() => "Iniciando Juego";
    protected override string GetSubtotalCameraPosition() => "Actividad 1 Subtotal";
    protected override string GetSuccessCameraPosition() => "Actividad 1 Success";
    protected override string GetActivityCommandId() => "Day4_ClienteMolestoTiempo";

    // ══════════════════════════════════════════════════════════════════════════════
    // MANEJO DE INSTRUCCIONES
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void ShowInitialInstruction()
    {
        UpdateInstructionOnce(0, StartNewAttemptAngry);
    }

    protected override void OnSubtotalPhaseReady()
    {
        UpdateInstructionOnce(3); // "Presiona SUBTOTAL"
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // INICIALIZACIÓN
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void OnActivityInitialize()
    {
        // Registrar diálogos de cliente molesto
        RegisterAngryVelocityDialogs();

        // Desactivar botones específicos
        foreach (var button in commandCardButtons)
            button.gameObject.SetActive(false);
        foreach (var button in enterButtons)
            button.gameObject.SetActive(false);
        foreach (var button in enterLastClicking)
            button.gameObject.SetActive(false);

        // Ocultar UI del reto
        if (scannedCountText != null) scannedCountText.enabled = false;
        if (remainingTimeText != null) remainingTimeText.enabled = false;
        if (failPanel != null) failPanel.SetActive(false);
    }

    protected override void InitializeCommands()
    {
        base.InitializeCommands();

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

        // Comando T+B+ENTER_
        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "T+B+ENTER_",
            customAction = HandleEnterAmount,
            requiredActivity = GetActivityCommandId(),
            commandButtons = commandCardButtons
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FLUJO INICIAL - 3 PRODUCTOS ANTES DEL RETO
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Inicia un nuevo intento (override completo porque el flujo es diferente).
    /// </summary>
    private void StartNewAttemptAngry()
    {
        scannedCount = 0;
        challengeStarted = false;

        currentCustomer = customerSpawner.SpawnCustomer();
        currentCustomerMovement = currentCustomer.GetComponent<CustomerMovement>();
        currentClientComponent = currentCustomer.GetComponent<Client>();

        cameraController.MoveToPosition(GetStartCameraPosition(), () =>
        {
            currentCustomerMovement.MoveToCheckout(() =>
            {
                SpawnNextInitialProduct();
            });
        });
    }

    /// <summary>
    /// Spawna productos iniciales (3 antes del reto).
    /// </summary>
    private void SpawnNextInitialProduct()
    {
        if (scannedCount >= 3)
        {
            ShowAngryDialog();
            return;
        }

        GameObject next = GetPooledProduct(scannedCount, spawnPoint);
        if (next != null)
        {
            next.SetActive(true);
            currentProduct = next;
            BindCurrentProduct();
        }
    }

    /// <summary>
    /// Override del registro de producto escaneado para manejar ambas fases.
    /// </summary>
    protected override void RegisterProductScanned()
    {
        ReturnCurrentProductToPool();
        scannedCount++;

        if (!challengeStarted)
        {
            // Fase inicial: spawnar siguiente producto o mostrar diálogo
            SpawnNextInitialProduct();
            return;
        }

        // Fase de reto: actualizar contador
        scannedCountText.text = $"{scannedCount} / {totalToScan}";

        if (scannedCount < totalToScan)
        {
            SpawnNextChallengeProduct();
        }
        else
        {
            EndChallenge();
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // DIÁLOGO DEL CLIENTE MOLESTO
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Muestra el diálogo del cliente molesto con opciones.
    /// </summary>
    private void ShowAngryDialog()
    {
        var entry = DialogSystem.Instance.GetNextComment("angry_velocity");
        if (entry == null) return;

        UpdateInstructionOnce(1, () =>
        {
            cameraController.MoveToPosition("Actividad 1 Cliente Camera", () =>
            {
                DialogSystem.Instance.ShowClientDialog(
                    currentClientComponent,
                    entry.clientText,
                    () =>
                    {
                        if (!string.IsNullOrEmpty(entry.question))
                        {
                            DialogSystem.Instance.ShowClientDialogWithOptions(
                                entry.question,
                                entry.options,
                                entry.correctAnswer,
                                () => BeginTimedChallenge(),
                                () => SoundManager.Instance.PlaySound("error")
                            );
                        }
                        else
                        {
                            BeginTimedChallenge();
                        }
                    });
            });
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // RETO CRONOMETRADO
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Inicia el reto cronometrado.
    /// </summary>
    private void BeginTimedChallenge()
    {
        cameraController.MoveToPosition(GetStartCameraPosition(), () =>
        {
            UpdateInstructionOnce(2, () =>
            {
                challengeStarted = true;
                scannedCount = 0;

                // Dificultad aumenta con cada intento
                totalToScan = 10 + (currentAttempt * 5);
                maxTime = 15 + (currentAttempt * 5);

                // Mostrar UI del reto
                scannedCountText.enabled = true;
                remainingTimeText.enabled = true;
                scannedCountText.text = $"0 / {totalToScan}";
                remainingTimeText.text = $"{maxTime:F0}s";

                StartChallengeTimer();
                SpawnNextChallengeProduct();
            });
        });
    }

    /// <summary>
    /// Spawna el siguiente producto del reto.
    /// </summary>
    private void SpawnNextChallengeProduct()
    {
        if (scannedCount < totalToScan)
        {
            GameObject p = GetPooledProduct(scannedCount % productNames.Length, spawnPoint);
            if (p != null)
            {
                p.SetActive(true);
                currentProduct = p;
                BindCurrentProduct();
            }
        }
    }

    /// <summary>
    /// Inicia el timer del reto.
    /// </summary>
    private void StartChallengeTimer()
    {
        timerActive = true;
        currentTime = maxTime;

        if (challengeTimerCoroutine != null)
            StopCoroutine(challengeTimerCoroutine);

        challengeTimerCoroutine = StartCoroutine(UpdateChallengeTimer());
    }

    /// <summary>
    /// Coroutine que actualiza el timer del reto.
    /// </summary>
    private IEnumerator UpdateChallengeTimer()
    {
        while (currentTime > 0 && timerActive)
        {
            currentTime -= Time.deltaTime;
            remainingTimeText.text = $"{currentTime:F0}s";
            yield return null;
        }

        if (scannedCount < totalToScan)
        {
            timerActive = false;
            HandleFail();
        }
    }

    /// <summary>
    /// Termina el reto exitosamente.
    /// </summary>
    private void EndChallenge()
    {
        timerActive = false;
        scannedCountText.enabled = false;
        remainingTimeText.enabled = false;

        cameraController.MoveToPosition(GetSubtotalCameraPosition(), () =>
        {
            UpdateInstructionOnce(3, () =>
            {
                AnimateButtonsSequentiallyWithActivation(subtotalButtons);
            });
        });
    }

    /// <summary>
    /// Maneja el fallo del reto (tiempo agotado).
    /// </summary>
    private void HandleFail()
    {
        failPanel.SetActive(true);
        SoundManager.Instance.PlaySound("tryagain");

        retryButton.onClick.RemoveAllListeners();
        retryButton.onClick.AddListener(() =>
        {
            failPanel.SetActive(false);
            RestartActivityAngry();
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // PAGO CON TARJETA (DESPUÉS DEL RETO)
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Después de presionar Subtotal, pregunta método de pago.
    /// </summary>
    protected override void OnSubtotalPressed(float totalAmount)
    {
        cameraController.MoveToPosition("Actividad 1 Cliente Camera", () =>
        {
            DialogSystem.Instance.ShowClientDialog(
                "Tu",
                dialog: "Disculpe, ¿Cuál sera su metódo de pago?",
                onComplete: () =>
                {
                    DialogSystem.Instance.ShowClientDialog(
                        currentClientComponent,
                        dialog: "Con tarjeta, apurese",
                        onComplete: () =>
                        {
                            DialogSystem.Instance.HideDialog(false);
                            cameraController.MoveToPosition(GetSubtotalCameraPosition(), () =>
                            {
                                UpdateInstructionOnce(4);
                                ActivateButtonWithSequence(commandCardButtons, 0, HandleEnterAmount);
                            });
                        });
                });
        });
    }

    /// <summary>
    /// Maneja el comando Enter Amount.
    /// </summary>
    public void HandleEnterAmount()
    {
        float totalAmount = GetTotalAmount(activityTotalPriceText);
        ActivateAngryAmountInput(totalAmount);
    }

    /// <summary>
    /// Activa el input de monto.
    /// </summary>
    private void ActivateAngryAmountInput(float amount)
    {
        SoundManager.Instance.PlaySound("success");

        cameraController.MoveToPosition("Actividad 1 Escribir Monto");

        if (amountInputField != null)
        {
            amountInputField.text = "";
            amountInputField.gameObject.SetActive(true);
            amountInputField.DeactivateInputField();
            amountInputField.ActivateInputField();
        }

        string amountString = ((int)amount).ToString() + "00";
        List<Button> selectedButtons = GetButtonsForAmount(amountString, numberButtons);

        foreach (var button in numberButtons)
            button.gameObject.SetActive(false);

        UpdateInstructionOnce(5);
        ActivateButtonWithSequence(selectedButtons, 0, () =>
        {
            ActivateButtonWithSequence(enterLastClicking, 0, () =>
            {
                MoveClientAndGenerateTicket();
            });
        });
    }

    /// <summary>
    /// Mueve el cliente y genera el ticket.
    /// </summary>
    private void MoveClientAndGenerateTicket()
    {
        cameraController.MoveToPosition("Actividad 1 Mirar Cliente", () =>
        {
            if (currentCustomerMovement != null)
            {
                currentCustomerMovement.MoveToPinEntry(() =>
                {
                    UpdateInstructionOnce(6, () =>
                    {
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
            RestartActivityAngry();
        }
        else
        {
            ShowActivityCompletePanel();
        }
    }

    /// <summary>
    /// Reinicia la actividad.
    /// </summary>
    private void RestartActivityAngry()
    {
        ResetValues();
        RegenerateProductValues();

        if (currentAttempt > 0)
            UpdateInstructionOnce(7, StartNewAttemptAngry, StartCompetition);
        else
            StartNewAttemptAngry();
    }

    /// <summary>
    /// Muestra el panel de éxito.
    /// </summary>
    private void ShowActivityCompletePanel()
    {
        StopActivityTimer();
        commandManager.commandList.Clear();
        scanner.ClearUI();

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
    // RESET Y LIMPIEZA
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void ResetValues()
    {
        base.ResetValues();

        // Reset específico de AngryVelocity
        scannedCountText.enabled = false;
        remainingTimeText.enabled = false;
        challengeStarted = false;
        timerActive = false;

        if (scannedCountText != null) scannedCountText.text = "0 / 0";
        if (remainingTimeText != null) remainingTimeText.text = "0s";

        if (challengeTimerCoroutine != null)
        {
            StopCoroutine(challengeTimerCoroutine);
            challengeTimerCoroutine = null;
        }

        // Mover cliente actual a la salida si existe
        if (currentCustomer != null && currentCustomerMovement != null)
        {
            currentCustomerMovement.MoveToExit();
            currentCustomer = null;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // REGISTRO DE DIÁLOGOS
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Registra los diálogos del cliente molesto en DialogSystem.
    /// </summary>
    private void RegisterAngryVelocityDialogs()
    {
        DialogSystem.Instance.customerComments.AddRange(new List<CustomerComment>
        {
            new CustomerComment
            {
                category = "angry_velocity",
                clientText = "Tengo prisa, ¿puedes atenderme más rápido?",
                question = "¿Cómo deberías responder?",
                options = new List<string>
                {
                    "Sí, lo haré en seguida",
                    "No, espera como todos",
                    "Estoy en lo mío",
                    "¿Y qué si no?"
                },
                correctAnswer = "Sí, lo haré en seguida"
            },
            new CustomerComment
            {
                category = "angry_velocity",
                clientText = "¿Siempre se tardan así? Esto es desesperante.",
                question = "¿Cómo deberías responder?",
                options = new List<string>
                {
                    "Haré lo posible por agilizar",
                    "¿Vienes a quejarte o a comprar?",
                    "No es mi problema",
                    "Entonces vete"
                },
                correctAnswer = "Haré lo posible por agilizar"
            },
            new CustomerComment
            {
                category = "angry_velocity",
                clientText = "¿Puedes darte prisa? No tengo todo el día.",
                question = "¿Cómo deberías responder?",
                options = new List<string>
                {
                    "Sí, enseguida",
                    "Si no te gusta, ve a otra caja",
                    "Aguanta como todos",
                    "Yo tampoco tengo todo el día"
                },
                correctAnswer = "Sí, enseguida"
            },
            new CustomerComment
            {
                category = "angry_velocity",
                clientText = "¡Qué lentitud! Esto debería ser más rápido.",
                question = "¿Cómo deberías responder?",
                options = new List<string>
                {
                    "Disculpa, voy a acelerar el proceso",
                    "No soy robot",
                    "A mí también me frustra",
                    "Eso no depende de mí"
                },
                correctAnswer = "Disculpa, voy a acelerar el proceso"
            }
        });
    }
}