using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// AngryClientVelocity - Actividad de Cliente Molesto (Velocidad)
/// 
/// FLUJO:
/// 1. Escanear 3 productos iniciales
/// 2. Cliente se queja → Diálogo con opciones
/// 3. Reto cronometrado: escanear N productos en M segundos
/// 4. Si falla: panel de retry
/// 5. Si pasa: subtotal → pago con tarjeta → ticket
/// 
/// TIPO DE EVALUACIÓN: ComboMetric
/// - Precisión: Respuestas correctas en diálogos + retos completados
/// - Velocidad: Tiempo total de la actividad (elapsedTime, corre internamente)
/// - Eficiencia: Penalización por errores
/// 
/// TIMERS:
/// - patienceSlider: Barra de paciencia del cliente (VISIBLE, baja durante el reto)
/// - elapsedTime (ActivityBase): Tiempo total interno (INVISIBLE, para el Adapter)
/// 
/// CONFIGURACIÓN DEL ADAPTER:
/// - evaluationType: ComboMetric
/// - errorsFieldName: "_errorCount"
/// - successesFieldName: "_successCount"
/// - expectedTotal: 0
/// - idealTimeSeconds: 120
/// - maxAllowedErrors: 3
/// - weightAccuracy: 0.5
/// - weightSpeed: 0.3
/// - weightEfficiency: 0.2
/// 
/// INSTRUCCIONES:
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
    // CONFIGURACIÓN - UI DEL RETO
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("AngryVelocity - UI del Reto")]
    [SerializeField] private TextMeshProUGUI scannedCountText;
    [SerializeField] private Slider patienceSlider;
    [SerializeField] private Image patienceSliderFill;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - BOTONES ESPECÍFICOS
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("AngryVelocity - Botones Específicos")]
    [SerializeField] private List<Button> commandCardButtons;
    [SerializeField] private List<Button> enterButtons;
    [SerializeField] private List<Button> enterLastClicking;

    // ══════════════════════════════════════════════════════════════════════════════
    // ESTADO DEL RETO
    // ══════════════════════════════════════════════════════════════════════════════

    private Client _currentClientComponent;
    private int _totalToScan;
    private float _currentTime;
    private float _maxTime;
    private bool _timerActive = false;
    private bool _challengeStarted = false;
    private Coroutine _challengeTimerCoroutine;

    // ══════════════════════════════════════════════════════════════════════════════
    // MÉTRICAS - LEÍDAS POR ADAPTER VÍA REFLECTION
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Contador de errores:
    /// - Respuesta incorrecta al diálogo
    /// - Fallar el reto de tiempo
    /// </summary>
    private int _errorCount = 0;

    /// <summary>
    /// Contador de aciertos:
    /// - Respuesta correcta al diálogo
    /// - Completar el reto de tiempo
    /// </summary>
    private int _successCount = 0;

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
        // Resetear métricas
        ResetMetrics();

        // Registrar diálogos de cliente molesto
        // Cargar 4 preguntas aleatorias de la categoría "impatient"
        DialogPoolLoader.RegisterInDialogSystem("impatient", 4);

        // Desactivar botones específicos
        foreach (var button in commandCardButtons)
            button.gameObject.SetActive(false);
        foreach (var button in enterButtons)
            button.gameObject.SetActive(false);
        foreach (var button in enterLastClicking)
            button.gameObject.SetActive(false);

        // Ocultar UI del reto
        if (scannedCountText != null) scannedCountText.enabled = false;
        if (patienceSlider != null) patienceSlider.gameObject.SetActive(false);

        // ⚠️ IMPORTANTE: Ocultar el timer de LCPaymentActivityBase
        // Solo mostraremos el timer del reto (remainingTimeText)
        // El elapsedTime de ActivityBase corre internamente para el Adapter
        if (liveTimerText != null)
            liveTimerText.gameObject.SetActive(false);
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

    /// <summary>
    /// Override de StartCompetition: inicia timer INTERNO sin mostrar UI.
    /// El usuario solo ve el countdown del reto (remainingTimeText).
    /// </summary>
    protected override void StartCompetition()
    {
        // Iniciar música
        if (activityMusicClip != null)
        {
            SoundManager.Instance.SetActivityMusic(activityMusicClip, 0.2f, true);
        }

        // En modo práctica, mostrar el timer general
        if (liveTimerText != null)
            liveTimerText.gameObject.SetActive(true);

        // Iniciar timer (visible en práctica, interno en normal)
        StartActivityTimer();
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // MÉTRICAS
    // ══════════════════════════════════════════════════════════════════════════════

    private void ResetMetrics()
    {
        _errorCount = 0;
        _successCount = 0;
    }

    private void RegisterError()
    {
        _errorCount++;
        SoundManager.Instance.PlaySound("error");
        Debug.Log($"[AngryClientVelocity] Error registrado. Total: {_errorCount}");
    }

    private void RegisterSuccess()
    {
        _successCount++;
        Debug.Log($"[AngryClientVelocity] Acierto registrado. Total: {_successCount}");
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FLUJO INICIAL - 3 PRODUCTOS ANTES DEL RETO
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Inicia un nuevo intento.
    /// </summary>
    private void StartNewAttemptAngry()
    {
        scannedCount = 0;
        _challengeStarted = false;

        currentCustomer = customerSpawner.SpawnCustomer();
        currentCustomerMovement = currentCustomer.GetComponent<CustomerMovement>();
        _currentClientComponent = currentCustomer.GetComponent<Client>();

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

        if (!_challengeStarted)
        {
            // Fase inicial: spawnar siguiente producto o mostrar diálogo
            SpawnNextInitialProduct();
            return;
        }

        // Fase de reto: actualizar contador
        scannedCountText.text = $"{scannedCount} / {_totalToScan}";

        if (scannedCount < _totalToScan)
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
        var entry = DialogSystem.Instance.GetNextComment("impatient");
        if (entry == null) return;

        UpdateInstructionOnce(1, () =>
        {
            cameraController.MoveToPosition("Actividad 1 Cliente Camera", () =>
            {
                DialogSystem.Instance.ShowClientDialog(
                    _currentClientComponent,
                    entry.clientText,
                    () =>
                    {
                        if (!string.IsNullOrEmpty(entry.question))
                        {
                            DialogSystem.Instance.ShowClientDialogWithOptions(
                                entry.question,
                                entry.options,
                                entry.correctAnswer,
                                OnCorrectDialogAnswer,
                                OnWrongDialogAnswer
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

    /// <summary>
    /// Callback cuando el usuario responde correctamente al diálogo.
    /// </summary>
    private void OnCorrectDialogAnswer()
    {
        RegisterSuccess();
        BeginTimedChallenge();
    }

    /// <summary>
    /// Callback cuando el usuario responde incorrectamente al diálogo.
    /// </summary>
    private void OnWrongDialogAnswer()
    {
        RegisterError();
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
                _challengeStarted = true;
                scannedCount = 0;

                // Dificultad aumenta con cada intento
                _totalToScan = 10 + (currentAttempt * 5);
                _maxTime = 15 + (currentAttempt * 5);

                // Mostrar UI del reto
                scannedCountText.enabled = true;
                scannedCountText.text = $"0 / {_totalToScan}";

                // Configurar y mostrar barra de paciencia
                if (patienceSlider != null)
                {
                    patienceSlider.gameObject.SetActive(true);
                    patienceSlider.maxValue = 1f;
                    patienceSlider.value = 1f;
                    UpdatePatienceColor(1f);
                }

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
        if (scannedCount < _totalToScan)
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
        _timerActive = true;
        _currentTime = _maxTime;

        if (_challengeTimerCoroutine != null)
            StopCoroutine(_challengeTimerCoroutine);

        _challengeTimerCoroutine = StartCoroutine(UpdateChallengeTimer());
    }

    /// <summary>
    /// Coroutine que actualiza la barra de paciencia del cliente.
    /// Verde (100%-60%) → Amarillo (60%-30%) → Rojo (30%-0%)
    /// </summary>
    private IEnumerator UpdateChallengeTimer()
    {
        while (_currentTime > 0 && _timerActive)
        {
            _currentTime -= Time.deltaTime;

            // Actualizar barra de paciencia (0 a 1)
            float normalizedValue = Mathf.Clamp01(_currentTime / _maxTime);

            if (patienceSlider != null)
            {
                patienceSlider.value = normalizedValue;
                UpdatePatienceColor(normalizedValue);
            }

            yield return null;
        }

        if (scannedCount < _totalToScan)
        {
            _timerActive = false;
            HandleChallengeFail();
        }
    }

    /// <summary>
    /// Actualiza el color del fill de la barra de paciencia.
    /// Verde (100%-60%) → Amarillo (60%-30%) → Rojo (30%-0%)
    /// </summary>
    private void UpdatePatienceColor(float normalizedValue)
    {
        if (patienceSliderFill == null) return;

        Color patienceColor;

        if (normalizedValue > 0.6f)
        {
            // Verde → Amarillo (100% - 60%)
            float t = (normalizedValue - 0.6f) / 0.4f;
            patienceColor = Color.Lerp(Color.yellow, Color.green, t);
        }
        else if (normalizedValue > 0.3f)
        {
            // Amarillo → Naranja (60% - 30%)
            float t = (normalizedValue - 0.3f) / 0.3f;
            patienceColor = Color.Lerp(new Color(1f, 0.5f, 0f), Color.yellow, t);
        }
        else
        {
            // Naranja → Rojo (30% - 0%)
            float t = normalizedValue / 0.3f;
            patienceColor = Color.Lerp(Color.red, new Color(1f, 0.5f, 0f), t);
        }

        patienceSliderFill.color = patienceColor;
    }

    /// <summary>
    /// Termina el reto exitosamente.
    /// </summary>
    private void EndChallenge()
    {
        _timerActive = false;

        // Ocultar UI del reto
        scannedCountText.enabled = false;
        if (patienceSlider != null) patienceSlider.gameObject.SetActive(false);

        // Registrar éxito del reto
        RegisterSuccess();

        cameraController.MoveToPosition(GetSubtotalCameraPosition(), () =>
        {
            UpdateInstructionOnce(3, () =>
            {
                AnimateButtonsSequentiallyWithActivation(subtotalButtons);
            });
        });
    }

    /// <summary>
    /// Maneja el fallo del reto (paciencia agotada).
    /// Registra error, cliente se queja, y reinicia el reto.
    /// El jugador SIEMPRE debe completar el escaneo para avanzar.
    /// </summary>
    private void HandleChallengeFail()
    {
        // Ocultar UI del reto
        scannedCountText.enabled = false;
        if (patienceSlider != null) patienceSlider.gameObject.SetActive(false);

        // Limpiar producto activo si quedó uno en escena
        ReturnCurrentProductToPool();
        scanner.ClearUI();

        // Registrar error por fallar el reto
        RegisterError();

        // Cliente se queja y luego reinicia el reto
        cameraController.MoveToPosition("Actividad 1 Cliente Camera", () =>
        {
            DialogSystem.Instance.ShowClientDialog(
                _currentClientComponent,
                "¡Estás tardando demasiado! Apúrate por favor.",
                () =>
                {
                    DialogSystem.Instance.HideDialog();
                    RestartChallenge();
                });
        });
    }

    /// <summary>
    /// Reinicia solo el reto (no toda la actividad).
    /// La barra se rellena y los productos se spawnean de nuevo.
    /// </summary>
    private void RestartChallenge()
    {
        scannedCount = 0;
        _challengeStarted = true;

        cameraController.MoveToPosition(GetStartCameraPosition(), () =>
        {
            // Mostrar UI del reto nuevamente
            scannedCountText.enabled = true;
            scannedCountText.text = $"0 / {_totalToScan}";

            // Resetear barra de paciencia
            if (patienceSlider != null)
            {
                patienceSlider.gameObject.SetActive(true);
                patienceSlider.value = 1f;
                UpdatePatienceColor(1f);
            }

            StartChallengeTimer();
            SpawnNextChallengeProduct();
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
                dialog: "Disculpe, ¿cuál será su método de pago?",
                onComplete: () =>
                {
                    DialogSystem.Instance.ShowClientDialog(
                        _currentClientComponent,
                        dialog: "Con tarjeta, apúrese",
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
            ActivateButtonWithSequence(enterLastClicking, 0, MoveClientAndGenerateTicket);
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
            ShowActivityComplete();
        }
    }

    /// <summary>
    /// Reinicia la actividad para el siguiente intento.
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
    /// Muestra el resultado usando el Adapter/UnifiedSummaryPanel.
    /// </summary>
    protected override void ShowActivityComplete()
    {
        StopActivityTimer();
        commandManager.commandList.Clear();
        scanner.ClearUI();

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
                Debug.LogWarning("[AngryClientVelocity] ActivityMetricsAdapter no encontrado. Completando sin estrellas.");
                CompleteActivity();
            }
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // RESET Y LIMPIEZA
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void ResetValues()
    {
        base.ResetValues();

        // Reset específico de AngryVelocity
        if (scannedCountText != null)
        {
            scannedCountText.enabled = false;
            scannedCountText.text = "0 / 0";
        }

        if (patienceSlider != null)
        {
            patienceSlider.gameObject.SetActive(false);
            patienceSlider.value = 1f;
        }

        _challengeStarted = false;
        _timerActive = false;

        if (_challengeTimerCoroutine != null)
        {
            StopCoroutine(_challengeTimerCoroutine);
            _challengeTimerCoroutine = null;
        }

        // Mover cliente actual a la salida si existe
        if (currentCustomer != null && currentCustomerMovement != null)
        {
            currentCustomerMovement.MoveToExit();
            currentCustomer = null;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // LIMPIEZA
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void OnDisable()
    {
        base.OnDisable();

        // ✅ FIX M-12: Limpiar coroutine del challenge timer al desactivar
        _timerActive = false;
        if (_challengeTimerCoroutine != null)
        {
            StopCoroutine(_challengeTimerCoroutine);
            _challengeTimerCoroutine = null;
        }
    }
}