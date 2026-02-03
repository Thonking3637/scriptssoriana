using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// ReceiptActivity - Actividad de Servicio Físico (Recibo de Luz)
/// 
/// FLUJO:
/// 1. Cliente llega con recibo de luz
/// 2. Cajero escanea el recibo
/// 3. Recibo se divide en 2 partes
/// 4. Entregar parte grande al cliente, parte pequeña a caja
/// 5. Repetir 3 veces → Completar actividad
/// 
/// TIPO DE EVALUACIÓN: GuidedActivity (tutorial guiado, siempre 3 estrellas)
/// 
/// INSTRUCCIONES:
/// 0 = Bienvenida
/// 1 = Tomar recibo del cliente
/// 2 = Escanear recibo
/// 3 = Mostrar recibo partido
/// 4 = Entregar partes
/// 5 = Siguiente cliente / Competencia
/// </summary>
public class ReceiptActivity : ActivityBase
{
    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - CLIENTE
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Cliente")]
    [SerializeField] private CustomerSpawner customerSpawner;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - RECIBO
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Recibo - Prefabs y Posiciones")]
    [SerializeField] private GameObject receiptPrefab;
    [SerializeField] private Transform receiptSpawnPoint;
    [SerializeField] private Transform scannerPoint;
    [SerializeField] private Transform initialReturnPoint;

    [Header("Recibo - División")]
    [SerializeField] private GameObject splitReceiptBigPrefab;
    [SerializeField] private GameObject splitReceiptSmallPrefab;
    [SerializeField] private Transform bigPartSpawnPoint;
    [SerializeField] private Transform smallPartSpawnPoint;

    [Header("Recibo - Destinos")]
    [SerializeField] private Transform clientTarget;
    [SerializeField] private Transform registerTarget;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - UI
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("UI - Panel de Recibo")]
    [SerializeField] private GameObject receiptPanelUI;
    [SerializeField] private TextMeshProUGUI receiptInfoText;

    [Header("UI - Timer")]
    [SerializeField] private TextMeshProUGUI liveTimerText;
    [SerializeField] private TextMeshProUGUI successTimeText;

    [Header("UI - Panel Success")]
    [SerializeField] private Button continueButton;

    // ══════════════════════════════════════════════════════════════════════════════
    // ESTADO INTERNO
    // ══════════════════════════════════════════════════════════════════════════════

    // Referencias cacheadas del cliente actual
    private GameObject _currentCustomer;
    private CustomerMovement _currentMovement;
    private Client _currentClient;

    // Control de intentos
    private int _currentAttempt = 0;
    private const int MAX_ATTEMPTS = 3;

    // Control de partes entregadas por intento
    private int _deliveredParts = 0;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN DE CÁMARA
    // ══════════════════════════════════════════════════════════════════════════════

    private const string CAM_START = "Iniciando Juego";
    private const string CAM_SHOW_RECEIPT = "Actividad 1 Mostrar Recibo";
    private const string CAM_SHOW_SCREEN = "Actividad 1 Mostrar Pantalla";
    private const string CAM_SHOW_SPLIT = "Actividad 1 Mostrar Recibo Partido";
    private const string CAM_SUCCESS = "Actividad 1 Sucessfull";

    // ══════════════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void Initialize() { }

    public override void StartActivity()
    {
        base.StartActivity();

        // Configurar timer
        activityTimerText = liveTimerText;
        activityTimeText = successTimeText;

        // Iniciar flujo
        UpdateInstructionOnce(0, StartNewAttempt);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        UnsubscribeFromReceiptEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromReceiptEvents();
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FLUJO PRINCIPAL
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Inicia un nuevo intento (cliente nuevo).
    /// </summary>
    private void StartNewAttempt()
    {
        _deliveredParts = 0;

        SpawnAndCacheCustomer();
        MoveCustomerToCheckout();
    }

    /// <summary>
    /// Spawnea cliente y cachea sus componentes.
    /// </summary>
    private void SpawnAndCacheCustomer()
    {
        _currentCustomer = customerSpawner.SpawnCustomer();

        // Intentar usar cache del spawner (optimizado)
        if (customerSpawner.TryGetCachedComponents(_currentCustomer, out var movement, out var client))
        {
            _currentMovement = movement;
            _currentClient = client;
        }
        else
        {
            // Fallback si el cache falla
            _currentMovement = _currentCustomer.GetComponent<CustomerMovement>();
            _currentClient = _currentCustomer.GetComponent<Client>();
        }

        // Generar datos aleatorios para el recibo
        GenerateReceiptData();
    }

    /// <summary>
    /// Genera datos aleatorios para el recibo de luz.
    /// </summary>
    private void GenerateReceiptData()
    {
        int randomNumber = Random.Range(100, 1000);
        _currentClient.address = $"Calle Mexico {randomNumber}";
        _currentClient.paymentAmount = Random.Range(300, 1001);
        _currentClient.receiptType = "SERVICIO DE LUZ";
    }

    /// <summary>
    /// Mueve la cámara y el cliente al checkout.
    /// </summary>
    private void MoveCustomerToCheckout()
    {
        cameraController.MoveToPosition(CAM_START, () =>
        {
            _currentMovement.MoveToCheckout(() =>
            {
                cameraController.MoveToPosition(CAM_SHOW_RECEIPT, () =>
                {
                    UpdateInstructionOnce(1);
                    SpawnReceipt();
                });
            });
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // LÓGICA DEL RECIBO
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Instancia el recibo y configura su comportamiento.
    /// </summary>
    private void SpawnReceipt()
    {
        GameObject receiptGO = Instantiate(
            receiptPrefab,
            receiptSpawnPoint.position,
            receiptPrefab.transform.rotation
        );

        ReceiptBehavior receipt = receiptGO.GetComponent<ReceiptBehavior>();

        receipt.Initialize(
            _currentClient,
            scannerPoint,
            initialReturnPoint,
            receiptPanelUI,
            receiptInfoText,
            splitReceiptBigPrefab,
            splitReceiptSmallPrefab,
            clientTarget,
            registerTarget,
            bigPartSpawnPoint,
            smallPartSpawnPoint
        );

        // Suscribirse al evento de entrega de partes
        SubscribeToReceiptEvents();

        // Configurar callbacks del recibo
        ConfigureReceiptCallbacks(receipt);
    }

    /// <summary>
    /// Configura los callbacks del comportamiento del recibo.
    /// </summary>
    private void ConfigureReceiptCallbacks(ReceiptBehavior receipt)
    {
        // Cuando el recibo regresa después del escaneo
        receipt.onReturnToStartComplete = () =>
        {
            SoundManager.Instance.PlaySound("success");

            cameraController.MoveToPosition(CAM_SHOW_SCREEN, () =>
            {
                UpdateInstructionOnce(2, () =>
                {
                    DOVirtual.DelayedCall(1f, () =>
                    {
                        cameraController.MoveToPosition(CAM_SHOW_SPLIT, () =>
                        {
                            UpdateInstructionOnce(3);
                        });
                    });
                });
            });
        };

        // Cuando el recibo se divide
        receipt.onSplitReceipt = () =>
        {
            SoundManager.Instance.PlaySound("success");
            UpdateInstructionOnce(4);
        };
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // MANEJO DE EVENTOS
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Suscribe al evento estático de entrega de partes.
    /// </summary>
    private void SubscribeToReceiptEvents()
    {
        SplitReceiptPart.OnReceiptPartDelivered += HandlePartDelivered;
    }

    /// <summary>
    /// Desuscribe del evento estático de entrega de partes.
    /// </summary>
    private void UnsubscribeFromReceiptEvents()
    {
        SplitReceiptPart.OnReceiptPartDelivered -= HandlePartDelivered;
    }

    /// <summary>
    /// Maneja la entrega de cada parte del recibo.
    /// </summary>
    private void HandlePartDelivered()
    {
        _deliveredParts++;

        // Necesitamos entregar ambas partes (grande y pequeña)
        if (_deliveredParts < 2) return;

        // Ambas partes entregadas - limpiar y procesar
        UnsubscribeFromReceiptEvents();
        OnAttemptComplete();
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // CONTROL DE INTENTOS
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Procesa la finalización de un intento.
    /// </summary>
    private void OnAttemptComplete()
    {
        // Ocultar panel de recibo
        if (receiptPanelUI != null)
            receiptPanelUI.SetActive(false);

        _currentAttempt++;

        // Mover cliente a la salida
        _currentMovement.MoveToExit();

        // ¿Más intentos o finalizar?
        if (_currentAttempt < MAX_ATTEMPTS)
        {
            PrepareNextAttempt();
        }
        else
        {
            ActivityComplete();
        }
    }

    /// <summary>
    /// Prepara el siguiente intento con instrucción de transición.
    /// </summary>
    private void PrepareNextAttempt()
    {
        UpdateInstructionOnce(5, StartNewAttempt, StartCompetition);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // COMPETENCIA (TIMER)
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Inicia el modo competencia con música y timer.
    /// Llamado después del primer intento tutorial.
    /// </summary>
    private void StartCompetition()
    {
        // Activar música de actividad
        if (activityMusicClip != null)
        {
            SoundManager.Instance.SetActivityMusic(activityMusicClip, 0.2f, false);
        }

        // Mostrar y arrancar timer
        if (liveTimerText != null)
        {
            liveTimerText.gameObject.SetActive(true);
        }

        StartActivityTimer();
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FINALIZACIÓN
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Completa la actividad mostrando el panel de éxito.
    /// </summary>
    private void ActivityComplete()
    {
        StopActivityTimer();

        cameraController.MoveToPosition(CAM_SUCCESS, () =>
        {
            SoundManager.Instance.RestorePreviousMusic();

            // Intentar usar ActivityMetricsAdapter para el sistema de 3 estrellas
            var adapter = GetComponent<ActivityMetricsAdapter>();

            if (adapter != null)
            {
                adapter.NotifyActivityCompleted();
            }
            else
            {
                // Fallback: panel de éxito manual
                ShowManualSuccessPanel();
            }
        });
    }

    /// <summary>
    /// Muestra el panel de éxito manualmente (fallback sin adapter).
    /// </summary>
    private void ShowManualSuccessPanel()
    {
        SoundManager.Instance.PlaySound("win");

        continueButton.onClick.RemoveAllListeners();
        continueButton.onClick.AddListener(() =>
        {
            cameraController.MoveToPosition(CAM_START);
            CompleteActivity();
        });
    }
}