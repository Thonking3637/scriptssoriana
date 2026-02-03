using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;
using System;

/// <summary>
/// SuspensionReactivacion - Actividad de Suspensión y Reactivación de Cuenta
/// 
/// FLUJO:
/// 1. Cliente llega → Escanear 4 productos
/// 2. Cliente olvida tarjeta → Se va
/// 3. Presionar SUPER → Panel Suspender
/// 4. Supervisor 1 llega → Diálogo → Contraseña → Ticket suspensión
/// 5. Cliente regresa → Diálogo reactivación
/// 6. Presionar SUPER → Panel Reactivar
/// 7. Supervisor 2 llega → Diálogo → Contraseña → Ticket reactivación
/// 8. Restaurar datos → Completar
/// 
/// TIPO DE EVALUACIÓN: ComboMetric
/// - Tiempo: Se mide toda la actividad
/// - Errores: Se trackean respuestas incorrectas en los 3 diálogos
/// 
/// CONFIGURACIÓN DEL ADAPTER:
/// - evaluationType: ComboMetric
/// - errorsFieldName: "_errorCount"
/// - successesFieldName: "_successCount"
/// - expectedTotal: 3 (3 diálogos con opciones)
/// - maxAllowedErrors: 3
/// 
/// INSTRUCCIONES:
/// 0 = Bienvenida
/// 1 = Escanear productos
/// 2 = Cliente se queja (olvida tarjeta)
/// 3 = Presionar SUPER
/// 4 = Presionar Suspender
/// 5 = Supervisor viene
/// 6 = Supervisor camina al teclado
/// 7 = Contraseña
/// 8 = Ticket suspensión
/// 9 = Cliente regresa
/// 10 = Presionar SUPER (reactivar)
/// 11 = Presionar Reactivar
/// 12 = Supervisor 2 viene
/// 13 = Contraseña 2
/// 14 = Ticket reactivación
/// 15 = Restaurar datos
/// 16 = Completar
/// </summary>
public class SuspensionReactivacion : ActivityBase
{
    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - CLIENTE Y PRODUCTOS
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Cliente y Productos")]
    [SerializeField] private CustomerSpawner customerSpawner;
    [SerializeField] private Transform productSpawnPoint;
    [SerializeField] private ProductScanner scanner;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - UI
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI activityProductsText;
    [SerializeField] private TextMeshProUGUI activityTotalPriceText;
    [SerializeField] private TextMeshProUGUI passwordText;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - BOTONES
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Botones")]
    [SerializeField] private List<Button> superPressedButton;
    [SerializeField] private List<Button> superPressedButton2;
    [SerializeField] private Button continueButton;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - PANEL SUSPENDER/REACTIVAR
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Panel Reactivar/Activar")]
    [SerializeField] private GameObject superPanel;
    [SerializeField] private Button suspendButton;
    [SerializeField] private Button reactivateButton;
    [SerializeField] private GameObject supervisorPasswordPanel;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - SUPERVISOR
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Supervisor")]
    [SerializeField] private GameObject supervisorPrefab;
    [SerializeField] private Transform supervisorSpawnPoint;
    [SerializeField] private Transform supervisorEntryPoint;
    [SerializeField] private List<Transform> supervisorMiddlePath;
    [SerializeField] private Transform supervisorExitPoint;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN - TICKETS
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Ticket Suspensión")]
    [SerializeField] private GameObject ticketPrefab;
    [SerializeField] private Transform ticketSpawnPoint;
    [SerializeField] private Transform ticketTargetPoint;

    [Header("Ticket Reactivación")]
    [SerializeField] private GameObject ticketPrefab2;
    [SerializeField] private Transform ticketSFirst;
    [SerializeField] private Transform ticketSLast;

    // ══════════════════════════════════════════════════════════════════════════════
    // ESTADO INTERNO - CLIENTE Y PRODUCTOS
    // ══════════════════════════════════════════════════════════════════════════════

    private GameObject _currentCustomer;
    private Client _currentClient;
    private CustomerMovement _currentMovement;
    private GameObject _currentProduct;

    private int _scannedCount = 0;
    private const int MAX_PRODUCTS = 4;
    private List<DragObject> _scannedProducts = new();
    private int _lastProductIndex = -1;

    // ══════════════════════════════════════════════════════════════════════════════
    // ESTADO INTERNO - SUPERVISOR Y DATOS GUARDADOS
    // ══════════════════════════════════════════════════════════════════════════════

    private GameObject _currentSupervisor;
    private string _savedProductText;
    private string _savedTotalText;
    private string _suspendedCustomerPrefabName;

    // ══════════════════════════════════════════════════════════════════════════════
    // MÉTRICAS - LEÍDAS POR ADAPTER VÍA REFLECTION
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Contador de errores - El ActivityMetricsAdapter lee este campo.
    /// Se incrementa en cada respuesta incorrecta de los diálogos.
    /// </summary>
    private int _errorCount = 0;

    /// <summary>
    /// Contador de aciertos - El ActivityMetricsAdapter puede leer este campo.
    /// </summary>
    private int _successCount = 0;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN DE CÁMARA
    // ══════════════════════════════════════════════════════════════════════════════

    private const string CAM_START = "Iniciando Juego";
    private const string CAM_CLIENT = "Cliente Camera";
    private const string CAM_SUPER = "Actividad 4 Super";
    private const string CAM_SUSPEND = "Actividad 4 Presionar Suspender";
    private const string CAM_SUPERVISOR_1 = "Mirada Supervisor 1";
    private const string CAM_SUPERVISOR_2 = "Mirada Supervisor 2";
    private const string CAM_PASSWORD = "Actividad 4 Supervisor Contraseña";
    private const string CAM_TICKET = "Actividad 4 Mirar Ticket";
    private const string CAM_RESTORE = "Actividad 4 Restauracion";
    private const string CAM_SUCCESS = "Actividad 4 Success";

    // ══════════════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void Initialize() { }

    public override void StartActivity()
    {
        base.StartActivity();

        // Resetear métricas y estado
        ResetMetrics();
        ResetState();

        // Regenerar productos disponibles
        RegenerateProductValues();

        // Configurar scanner
        scanner.BindUI(this, activityProductsText, activityTotalPriceText, true);

        // Obtener nombres de productos del pool
        productNames = ObjectPoolManager.Instance.GetAvailablePrefabNames(PoolTag.Producto).ToArray();

        // Configurar comandos
        InitializeCommands();

        // Iniciar flujo
        UpdateInstructionOnce(0, SpawnCustomer);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (scanner != null) scanner.UnbindUI(this);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // INICIALIZACIÓN
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void InitializeCommands()
    {
        base.InitializeCommands();

        // Desactivar botones de comando
        foreach (var button in superPressedButton)
            button.gameObject.SetActive(false);
        foreach (var button in superPressedButton2)
            button.gameObject.SetActive(false);

        // Comando SUPER (para suspender)
        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "SUPER",
            customAction = HandleSuperPressed,
            requiredActivity = "Day3_ClientePrecioCambiado",
            commandButtons = superPressedButton
        });

        // Comando SUPER2 (para reactivar)
        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "SUPER2",
            customAction = HandleSuperPressed2,
            requiredActivity = "Day3_ClientePrecioCambiado",
            commandButtons = superPressedButton2
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // MÉTRICAS
    // ══════════════════════════════════════════════════════════════════════════════

    private void ResetMetrics()
    {
        _errorCount = 0;
        _successCount = 0;
    }

    private void ResetState()
    {
        _scannedCount = 0;
        _scannedProducts.Clear();
        _lastProductIndex = -1;
        _savedProductText = "";
        _savedTotalText = "";
        _suspendedCustomerPrefabName = "";
    }

    private void RegisterError()
    {
        _errorCount++;
        SoundManager.Instance.PlaySound("error");
        Debug.Log($"[SuspensionReactivacion] Error registrado. Total: {_errorCount}");
    }

    private void RegisterSuccess()
    {
        _successCount++;
        Debug.Log($"[SuspensionReactivacion] Acierto registrado. Total: {_successCount}");
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FASE 1: CLIENTE Y ESCANEO DE PRODUCTOS
    // ══════════════════════════════════════════════════════════════════════════════

    private void SpawnCustomer()
    {
        _currentCustomer = customerSpawner.SpawnCustomer();
        _currentClient = _currentCustomer.GetComponent<Client>();
        _currentMovement = _currentCustomer.GetComponent<CustomerMovement>();

        cameraController.MoveToPosition(CAM_START, () =>
        {
            _currentMovement.MoveToCheckout(() =>
            {
                UpdateInstructionOnce(1, SpawnNextProduct);
            });
        });
    }

    private void SpawnNextProduct()
    {
        if (_scannedCount >= MAX_PRODUCTS) return;

        GameObject prefab = ObjectPoolManager.Instance.GetRandomPrefabFromPool(PoolTag.Producto, ref _lastProductIndex);
        if (prefab == null) return;

        _currentProduct = ObjectPoolManager.Instance.GetFromPool(PoolTag.Producto, prefab.name);
        if (_currentProduct == null) return;

        _currentProduct.transform.position = productSpawnPoint.position;
        _currentProduct.transform.rotation = prefab.transform.rotation;
        _currentProduct.transform.SetParent(null);
        _currentProduct.SetActive(true);

        DragObject drag = _currentProduct.GetComponent<DragObject>();
        drag?.SetOriginalPoolName(prefab.name);

        // ✅ CRÍTICO: Suscribirse al evento OnScanned del producto
        BindCurrentProduct(_currentProduct);
    }

    /// <summary>
    /// Suscribe el evento OnScanned del DragObject para detectar cuando se escanea.
    /// </summary>
    private void BindCurrentProduct(GameObject product)
    {
        if (product == null) return;

        DragObject drag = product.GetComponent<DragObject>();
        if (drag != null)
        {
            drag.OnScanned -= OnProductScannedHandler;
            drag.OnScanned += OnProductScannedHandler;
        }
    }

    /// <summary>
    /// Handler del evento OnScanned - se dispara cuando el producto pasa por el scanner.
    /// </summary>
    private void OnProductScannedHandler(DragObject dragObj)
    {
        if (dragObj == null) return;

        // Desuscribirse para evitar llamadas duplicadas
        dragObj.OnScanned -= OnProductScannedHandler;

        // Registrar en el scanner (actualiza UI de productos y total)
        if (scanner != null)
        {
            scanner.RegisterProductScan(dragObj);
        }

        // Continuar flujo
        RegisterProductScanned();
    }

    /// <summary>
    /// Procesa el producto escaneado y spawnea el siguiente o continúa el flujo.
    /// </summary>
    private void RegisterProductScanned()
    {
        if (_currentProduct == null) return;

        DragObject drag = _currentProduct.GetComponent<DragObject>();
        if (drag != null)
        {
            _scannedProducts.Add(drag);
        }

        // Retornar producto al pool
        string poolName = (drag != null && !string.IsNullOrEmpty(drag.OriginalPoolName))
            ? drag.OriginalPoolName
            : _currentProduct.name;

        ObjectPoolManager.Instance.ReturnToPool(PoolTag.Producto, poolName, _currentProduct);
        _currentProduct = null;
        _scannedCount++;

        if (_scannedCount < MAX_PRODUCTS)
        {
            SpawnNextProduct();
        }
        else
        {
            TriggerClientComplaint();
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FASE 2: CLIENTE SE VA (OLVIDA TARJETA)
    // ══════════════════════════════════════════════════════════════════════════════

    private void TriggerClientComplaint()
    {
        UpdateInstructionOnce(2, () =>
        {
            cameraController.MoveToPosition(CAM_CLIENT, () =>
            {
                DialogSystem.Instance.ShowClientDialog(
                    _currentClient,
                    "Ay no, olvidé mi tarjeta, necesito salir un momento.",
                    () =>
                    {                      
                        _suspendedCustomerPrefabName = _currentCustomer.name.Replace("(Clone)", "").Trim();

                        _currentMovement.MoveToExit(() =>
                        {
                            cameraController.MoveToPosition(CAM_SUPER, () =>
                            {
                                UpdateInstructionOnce(3, () =>
                                {
                                    ActivateButtonWithSequence(superPressedButton, 0, HandleSuperPressed);
                                });
                            });
                        });
                    });
            });
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FASE 3: SUSPENDER CUENTA
    // ══════════════════════════════════════════════════════════════════════════════

    public void HandleSuperPressed()
    {
        superPanel.SetActive(true);
        suspendButton.interactable = false;
        reactivateButton.interactable = false;

        cameraController.MoveToPosition(CAM_SUSPEND, () =>
        {
            UpdateInstructionOnce(4, () =>
            {
                suspendButton.interactable = true;
                reactivateButton.interactable = false;

                suspendButton.onClick.RemoveAllListeners();
                suspendButton.onClick.AddListener(() =>
                {
                    superPanel.SetActive(false);
                    SpawnSupervisor1();
                });
            });
        });
    }

    private void SpawnSupervisor1()
    {
        _currentSupervisor = Instantiate(supervisorPrefab, supervisorSpawnPoint.position, supervisorPrefab.transform.rotation);

        SupervisorMovement movement = _currentSupervisor.GetComponent<SupervisorMovement>();
        ConfigureSupervisorMovement(movement);

        UpdateInstructionOnce(5, () =>
        {
            cameraController.MoveToPosition(CAM_SUPERVISOR_1, () =>
            {
                movement.GoToEntryPoint(() =>
                {
                    cameraController.MoveToPosition(CAM_SUPERVISOR_2, () =>
                    {
                        ShowSupervisorDialog1();
                    });
                });
            });
        });
    }

    private void ConfigureSupervisorMovement(SupervisorMovement movement)
    {
        movement.entryPoint = supervisorEntryPoint;
        movement.middlePath = supervisorMiddlePath;
        movement.exitPoint = supervisorExitPoint;
        movement.animator = _currentSupervisor.GetComponent<Animator>();
    }

    /// <summary>
    /// Diálogo 1: Supervisor pregunta qué pasó (suspensión).
    /// </summary>
    private void ShowSupervisorDialog1()
    {
        DialogSystem.Instance.ShowClientDialog(
            _currentSupervisor.GetComponent<Client>(),
            "¿Qué sucedió? Cuéntame.",
            () =>
            {
                DialogSystem.Instance.ShowClientDialogWithOptions(
                    "¿Qué le debes decir a la supervisora?",
                    new List<string>
                    {
                        "El cliente se fue, necesito suspender la cuenta",
                        "Ese cliente está loco",
                        "No entiendo qué quiere",
                        "No sé para qué vino"
                    },
                    "El cliente se fue, necesito suspender la cuenta",
                    OnCorrectSupervisorAnswer1,
                    RegisterError
                );
            });
    }

    private void OnCorrectSupervisorAnswer1()
    {
        RegisterSuccess();

        DOVirtual.DelayedCall(0.5f, () =>
        {
            DialogSystem.Instance.ShowClientDialog(
                _currentSupervisor.GetComponent<Client>(),
                "Entiendo, lo haremos en este momento.",
                () =>
                {
                    DialogSystem.Instance.HideDialog();
                    SupervisorMovement movement = _currentSupervisor.GetComponent<SupervisorMovement>();

                    cameraController.MoveToPosition(CAM_SUPERVISOR_1, () =>
                    {
                        movement.GoThroughMiddlePath(() =>
                        {
                            UpdateInstructionOnce(6, () =>
                            {
                                cameraController.MoveToPosition(CAM_PASSWORD, () =>
                                {
                                    HandlePassword1();
                                });
                            });
                        });
                    });
                });
        });
    }

    private void HandlePassword1()
    {
        UpdateInstructionOnce(7, () =>
        {
            supervisorPasswordPanel.SetActive(true);

            AnimatePasswordEntry(() =>
            {
                SupervisorMovement movement = _currentSupervisor.GetComponent<SupervisorMovement>();
                movement.GoToExit(() =>
                {
                    movement.gameObject.SetActive(false);
                });

                // Guardar datos antes de limpiar
                SaveData();
                scanner.ClearUI();
                supervisorPasswordPanel.SetActive(false);

                cameraController.MoveToPosition(CAM_TICKET, () =>
                {
                    UpdateInstructionOnce(8, () =>
                    {
                        InstantiateTicket(ticketPrefab, ticketSpawnPoint, ticketTargetPoint, OnSuspensionTicketDelivered);
                    });
                });
            });
        });
    }

    public void SaveData()
    {
        _savedProductText = activityProductsText.text;
        _savedTotalText = activityTotalPriceText.text;
    }

    private void OnSuspensionTicketDelivered()
    {
        SoundManager.Instance.PlaySound("success");

        if (string.IsNullOrEmpty(_suspendedCustomerPrefabName)) return;

        // Respawnear el mismo cliente
        _currentCustomer = customerSpawner.SpawnCustomerByName(_suspendedCustomerPrefabName);
        if (_currentCustomer == null) return;

        _currentClient = _currentCustomer.GetComponent<Client>();
        _currentMovement = _currentCustomer.GetComponent<CustomerMovement>();

        cameraController.MoveToPosition(CAM_START, () =>
        {
            _currentMovement.MoveToCheckout(() =>
            {
                UpdateInstructionOnce(9, ShowReactivationDialog);
            });
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FASE 4: CLIENTE REGRESA - REACTIVAR CUENTA
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Diálogo 2: Cliente regresa y pide reactivar.
    /// </summary>
    private void ShowReactivationDialog()
    {
        cameraController.MoveToPosition(CAM_CLIENT, () =>
        {
            DialogSystem.Instance.ShowClientDialog(
                _currentClient,
                "Hola ya volví, fue super rápido, ¿podemos continuar?",
                () =>
                {
                    DialogSystem.Instance.ShowClientDialogWithOptions(
                        "¿Cómo debes responder?",
                        new List<string>
                        {
                            "Claro, déjeme reactivar su cuenta",
                            "No, debe volver otro día",
                            "Tiene que hablar con un gerente",
                            "Esa información no la manejo"
                        },
                        "Claro, déjeme reactivar su cuenta",
                        OnCorrectReactivationAnswer,
                        RegisterError
                    );
                });
        });
    }

    private void OnCorrectReactivationAnswer()
    {
        RegisterSuccess();

        UpdateInstructionOnce(10, () =>
        {
            cameraController.MoveToPosition(CAM_SUPER, () =>
            {
                ActivateButtonWithSequence(superPressedButton2, 0, HandleSuperPressed2);
            });
        });
    }

    public void HandleSuperPressed2()
    {
        superPanel.SetActive(true);
        reactivateButton.interactable = false;
        suspendButton.interactable = false;

        cameraController.MoveToPosition(CAM_SUSPEND, () =>
        {
            UpdateInstructionOnce(11, () =>
            {
                reactivateButton.interactable = true;
                suspendButton.interactable = false;

                reactivateButton.onClick.RemoveAllListeners();
                reactivateButton.onClick.AddListener(() =>
                {
                    superPanel.SetActive(false);
                    SpawnSupervisor2();
                });
            });
        });
    }

    private void SpawnSupervisor2()
    {
        _currentSupervisor = Instantiate(supervisorPrefab, supervisorSpawnPoint.position, supervisorPrefab.transform.rotation);

        SupervisorMovement movement = _currentSupervisor.GetComponent<SupervisorMovement>();
        ConfigureSupervisorMovement(movement);

        UpdateInstructionOnce(12, () =>
        {
            cameraController.MoveToPosition(CAM_SUPERVISOR_1, () =>
            {
                movement.GoToEntryPoint(() =>
                {
                    cameraController.MoveToPosition(CAM_SUPERVISOR_2, () =>
                    {
                        ShowSupervisorDialog2();
                    });
                });
            });
        });
    }

    /// <summary>
    /// Diálogo 3: Supervisor 2 pregunta qué pasó (reactivación).
    /// </summary>
    private void ShowSupervisorDialog2()
    {
        DialogSystem.Instance.ShowClientDialog(
            _currentSupervisor.GetComponent<Client>(),
            "¿Qué sucedió? Cuéntame.",
            () =>
            {
                DialogSystem.Instance.ShowClientDialogWithOptions(
                    "¿Qué le debes decir a la supervisora?",
                    new List<string>
                    {
                        "El cliente regresó, necesito reactivar la cuenta",
                        "Ese cliente está loco",
                        "No entiendo qué quiere",
                        "No sé para qué vino"
                    },
                    "El cliente regresó, necesito reactivar la cuenta",
                    OnCorrectSupervisorAnswer2,
                    RegisterError
                );
            });
    }

    private void OnCorrectSupervisorAnswer2()
    {
        RegisterSuccess();

        DOVirtual.DelayedCall(0.5f, () =>
        {
            DialogSystem.Instance.ShowClientDialog(
                _currentSupervisor.GetComponent<Client>(),
                "Entiendo, lo haremos en este momento.",
                () =>
                {
                    DialogSystem.Instance.HideDialog();
                    SupervisorMovement movement = _currentSupervisor.GetComponent<SupervisorMovement>();

                    cameraController.MoveToPosition(CAM_SUPERVISOR_1, () =>
                    {
                        movement.GoThroughMiddlePath(() =>
                        {
                            cameraController.MoveToPosition(CAM_PASSWORD, () =>
                            {
                                HandlePassword2();
                            });
                        });
                    });
                });
        });
    }

    private void HandlePassword2()
    {
        UpdateInstructionOnce(13, () =>
        {
            supervisorPasswordPanel.SetActive(true);

            AnimatePasswordEntry(() =>
            {
                SupervisorMovement movement = _currentSupervisor.GetComponent<SupervisorMovement>();
                movement.GoToExit(() => Debug.Log("Supervisor 2 se va"));

                supervisorPasswordPanel.SetActive(false);

                UpdateInstructionOnce(14, SpawnReactivationTicket);
            });
        });
    }

    private void SpawnReactivationTicket()
    {
        cameraController.MoveToPosition(CAM_TICKET, () =>
        {
            GameObject ticket = Instantiate(ticketPrefab2, ticketSFirst.position, ticketPrefab2.transform.rotation);
            ReactivateTicket ticketScript = ticket.GetComponent<ReactivateTicket>();
            ticketScript.Initialize(ticketSLast, ticketSFirst, RestoreSuspendedData);
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FASE 5: RESTAURAR Y COMPLETAR
    // ══════════════════════════════════════════════════════════════════════════════

    private void RestoreSuspendedData()
    {
        cameraController.MoveToPosition(CAM_RESTORE, () =>
        {
            // Restaurar textos guardados
            activityProductsText.text = _savedProductText;
            activityTotalPriceText.text = _savedTotalText;

            UpdateInstructionOnce(15, () =>
            {
                _currentMovement.MoveToExit();

                cameraController.MoveToPosition(CAM_START, () =>
                {
                    ActivityComplete();
                });
            });
        });
    }

    private void ActivityComplete()
    {
        scanner.ClearUI();
        commandManager.commandList.Clear();

        cameraController.MoveToPosition(CAM_SUCCESS, () =>
        {
            SoundManager.Instance.RestorePreviousMusic();

            var adapter = GetComponent<ActivityMetricsAdapter>();

            if (adapter != null)
            {
                adapter.NotifyActivityCompleted();
            }
            else
            {
                ShowManualSuccessPanel();
            }
        });
    }

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

    // ══════════════════════════════════════════════════════════════════════════════
    // UTILIDADES
    // ══════════════════════════════════════════════════════════════════════════════

    private void AnimatePasswordEntry(Action onComplete)
    {
        passwordText.text = "";
        string[] sequence = { "1", "2", "3", "4" };
        int index = 0;

        DOVirtual.DelayedCall(0.3f, () =>
        {
            AddDigit();

            void AddDigit()
            {
                if (index >= sequence.Length)
                {
                    DOVirtual.DelayedCall(0.3f, () => onComplete?.Invoke());
                    return;
                }

                passwordText.text += "*";
                index++;
                DOVirtual.DelayedCall(0.3f, AddDigit);
            }
        });
    }
}