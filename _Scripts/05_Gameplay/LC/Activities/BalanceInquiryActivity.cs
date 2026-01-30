using UnityEngine;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;
using UnityEngine.UI;

/// <summary>
/// Actividad de Consulta de Saldo - NO hereda de LCPaymentActivityBase
/// (No tiene productos, escaneo, ni pago)
/// 
/// CONFIGURACIÓN ActivityMetricsAdapter:
/// - successesFieldName: "currentAttempt"
/// - errorsFieldName: (vacío)
/// - expectedTotal: 5
/// - evaluationType: TimeBased
/// - idealTimeSeconds: 90
/// 
/// Flujo de instrucciones:
/// 0 = Inicio
/// 1 = Consulta puntos
/// 2 = Deslizar tarjeta
/// 3 = Segunda posición
/// 4 = Final posición
/// 5 = Reiniciar
/// </summary>
public class BalanceInquiryActivity : ActivityBase
{
    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Timer")]
    [SerializeField] private TextMeshProUGUI liveTimerText;
    [SerializeField] private TextMeshProUGUI successTimeText;

    [Header("Card Configuration")]
    [SerializeField] private CardInteraction cardInteraction;

    [Header("UI Elements")]
    [SerializeField] private GameObject panelDataClient;
    [SerializeField] private TextMeshProUGUI clientInfoText;
    [SerializeField] private List<Button> consultaPuntosButtons;

    [Header("Customer Configuration")]
    [SerializeField] private CustomerSpawner customerSpawner;

    [Header("Panel Success")]
    [SerializeField] private Button continueButton;

    // Estado
    private GameObject currentCustomer;
    private Client currentClient;
    private CustomerMovement currentCustomerMovement;

    [HideInInspector] public int currentAttempt = 0;
    private const int maxAttempts = 5;

    // ══════════════════════════════════════════════════════════════════════════════
    // INICIALIZACIÓN
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void Start()
    {
        base.Start();
        if (panelDataClient != null)
            panelDataClient.SetActive(false);
    }

    public override void StartActivity()
    {
        base.StartActivity();

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

        // Configurar timer
        activityTimerText = liveTimerText;
        activityTimeText = successTimeText;

        // Ocultar timer inicialmente
        if (liveTimerText != null)
            liveTimerText.gameObject.SetActive(false);

        InitializeCommands();

        // Resetear intentos
        currentAttempt = 0;

        UpdateInstructionOnce(0, StartNewAttempt);
    }

    protected override void InitializeCommands()
    {
        foreach (var button in consultaPuntosButtons)
            button.gameObject.SetActive(false);

        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "CONSULTA_PUNTOS",
            customAction = HandleConsultaPuntos,
            requiredActivity = "Day2_ConsultaPrecio",
            commandButtons = consultaPuntosButtons
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FLUJO PRINCIPAL
    // ══════════════════════════════════════════════════════════════════════════════

    private void StartNewAttempt()
    {
        // Verificar si ya completamos todos los intentos
        if (currentAttempt >= maxAttempts)
        {
            OnAllAttemptsComplete();
            return;
        }

        // Spawnear cliente
        currentCustomer = customerSpawner.SpawnCustomer();
        currentClient = currentCustomer.GetComponent<Client>();
        currentCustomerMovement = currentCustomer.GetComponent<CustomerMovement>();

        // Resetear UI
        if (panelDataClient != null)
            panelDataClient.SetActive(false);
        if (clientInfoText != null)
            clientInfoText.text = "";

        // Mover cliente al checkout
        cameraController.MoveToPosition("Iniciando Juego", () =>
        {
            currentCustomerMovement.MoveToCheckout(() =>
            {
                cameraController.MoveToPosition("Actividad Consulta Press Button CP", () =>
                {
                    UpdateInstructionOnce(1);
                    ActivateCommandButtons(consultaPuntosButtons);
                    ActivateButtonWithSequence(consultaPuntosButtons, 0);
                });
            });
        });
    }

    public void HandleConsultaPuntos()
    {
        ShowCard();
    }

    private void ShowCard()
    {
        SoundManager.Instance.PlaySound("success");
        cameraController.MoveToPosition("Actividad Consulta Primera Posicion", () =>
        {
            UpdateInstructionOnce(2);
            cardInteraction.gameObject.SetActive(true);
        });
    }

    private void HandleCardArrived()
    {
        cameraController.MoveToPosition("Actividad Consulta Segunda Posicion");
        UpdateInstructionOnce(3);

        // Mostrar datos del cliente
        if (panelDataClient != null)
        {
            panelDataClient.SetActive(true);
            if (currentClient != null && clientInfoText != null)
            {
                clientInfoText.text = $"{currentClient.clientName}\n{currentClient.purchasePoints} puntos";
            }
        }
    }

    private void HandleCardMovedToSecondPosition()
    {
        SoundManager.Instance.PlaySound("success");

        cameraController.MoveToPosition("Actividad Consulta Final Posicion", () =>
        {
            UpdateInstructionOnce(4, () =>
            {
                DOVirtual.DelayedCall(1f, () =>
                {
                    OnAttemptComplete();
                });
            });
        });
    }

    private void HandleCardReturned()
    {
        // No se usa directamente, el flujo continúa en HandleCardMovedToSecondPosition
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FINALIZACIÓN DE INTENTO
    // ══════════════════════════════════════════════════════════════════════════════

    private void OnAttemptComplete()
    {
        currentAttempt++;

        // Resetear tarjeta y mover cliente
        cardInteraction?.ResetCard();
        currentCustomerMovement?.MoveToExit();

        // Ocultar panel de datos
        if (panelDataClient != null)
            panelDataClient.SetActive(false);

        if (currentAttempt < maxAttempts)
        {
            // Más intentos
            cameraController.MoveToPosition("Iniciando Juego", () =>
            {
                // StartCompetition solo después del primer intento
                if (currentAttempt == 1)
                {
                    UpdateInstructionOnce(5, StartNewAttempt, StartCompetition);
                }
                else
                {
                    UpdateInstructionOnce(5, StartNewAttempt);
                }
            });
        }
        else
        {
            // Todos los intentos completados
            OnAllAttemptsComplete();
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // COMPETENCIA Y FINALIZACIÓN
    // ══════════════════════════════════════════════════════════════════════════════

    private void StartCompetition()
    {
        if (activityMusicClip != null)
        {
            SoundManager.Instance.SetActivityMusic(activityMusicClip, 0.2f, true);
        }

        if (liveTimerText != null)
        {
            liveTimerText.gameObject.SetActive(true);
        }

        StartActivityTimer();
    }

    private void OnAllAttemptsComplete()
    {
        StopActivityTimer();
        commandManager.commandList.Clear();

        // Restaurar música
        SoundManager.Instance.RestorePreviousMusic();

        // Usar el sistema de 3 estrellas
        var adapter = GetComponent<ActivityMetricsAdapter>();
        if (adapter != null)
        {
            adapter.NotifyActivityCompleted();
        }
        else
        {
            Debug.LogWarning("[BalanceInquiryActivity] No hay ActivityMetricsAdapter. " +
                           "Agrega el componente para usar UnifiedSummaryPanel.");
            SoundManager.Instance.PlaySound("win");
            CompleteActivity();
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // LIMPIEZA
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void OnDisable()
    {
        base.OnDisable();

        if (cardInteraction != null)
        {
            cardInteraction.OnCardMovedToFirstPosition -= HandleCardArrived;
            cardInteraction.OnCardMovedToSecondPosition -= HandleCardMovedToSecondPosition;
            cardInteraction.OnCardReturned -= HandleCardReturned;
        }
    }

    protected override void Initialize() { }
}