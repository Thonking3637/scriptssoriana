using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// LCPaymentActivityBase OPTIMIZADO
/// - Removido GameObject.Find() de SetupTimer()
/// - Precarga de AudioClips para evitar spike
/// - Uso de cache de componentes del CustomerSpawner
/// </summary>
public abstract class LCPaymentActivityBase : ActivityBase
{
    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN COMÚN
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("LC Base - Timer")]
    [SerializeField] protected TextMeshProUGUI liveTimerText;
    //[SerializeField] protected TextMeshProUGUI successTimeText;

    [Header("LC Base - Productos")]
    [SerializeField] protected Transform spawnPoint;
    [SerializeField] protected ProductScanner scanner;
    [SerializeField] protected int productsToScan = 4;

    [Header("LC Base - UI de Productos")]
    [SerializeField] protected TextMeshProUGUI activityProductsText;
    [SerializeField] protected TextMeshProUGUI activityTotalPriceText;

    [Header("LC Base - Botones Comunes")]
    [SerializeField] protected List<Button> subtotalButtons;
    [SerializeField] protected List<Button> numberButtons;
    [SerializeField] protected TMP_InputField amountInputField;

    [Header("LC Base - Cliente")]
    [SerializeField] protected CustomerSpawner customerSpawner;

    [Header("LC Base - Ticket")]
    [SerializeField] protected GameObject ticketPrefab;
    [SerializeField] protected Transform ticketSpawnPoint;
    [SerializeField] protected Transform ticketTargetPoint;

    // ══════════════════════════════════════════════════════════════════════════════
    // ESTADO COMÚN
    // ══════════════════════════════════════════════════════════════════════════════

    protected GameObject currentProduct;
    protected GameObject currentCustomer;
    protected CustomerMovement currentCustomerMovement;
    protected Client currentClient;

    protected int scannedCount = 0;
    protected int currentAttempt = 0;
    protected int maxAttempts = 3;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN ABSTRACTA
    // ══════════════════════════════════════════════════════════════════════════════

    protected abstract string GetStartCameraPosition();
    protected abstract string GetSubtotalCameraPosition();
    protected abstract string GetSuccessCameraPosition();
    protected abstract string GetActivityCommandId();

    // ══════════════════════════════════════════════════════════════════════════════
    // HOOKS VIRTUALES
    // ══════════════════════════════════════════════════════════════════════════════

    protected virtual void OnCustomerReady() { }

    protected virtual void OnAllProductsScanned()
    {
        MoveToSubtotalPhase();
    }

    protected virtual void OnRestartAttempt()
    {
        ResetValues();
        RegenerateProductValues();
        StartNewAttempt();
    }

    protected virtual void OnAllAttemptsComplete()
    {
        ShowActivityComplete();
    }

    protected virtual void OnBeforeProductSpawn() { }
    protected virtual void OnAfterProductSpawn(GameObject product) { }

    // ══════════════════════════════════════════════════════════════════════════════
    // LIFECYCLE - PRECARGA DE AUDIO
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Precarga el AudioClip de la actividad para evitar spike al iniciar competencia.
    /// Las hijas pueden sobrescribir para precargar clips adicionales.
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        PreloadAudioClips();
    }

    /// <summary>
    /// Precarga clips de audio para evitar spikes de carga.
    /// </summary>
    protected virtual void PreloadAudioClips()
    {
        // Precargar música de actividad
        if (activityMusicClip != null)
        {
            // LoadAudioData() carga los datos en memoria de forma síncrona
            // pero al hacerlo en Awake, el spike ocurre durante la carga de escena
            // en vez de durante el gameplay
            if (activityMusicClip.loadState == AudioDataLoadState.Unloaded)
            {
                activityMusicClip.LoadAudioData();
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // INICIALIZACIÓN
    // ══════════════════════════════════════════════════════════════════════════════

    public override void StartActivity()
    {
        base.StartActivity();

        // ✅ FIX M-11: Resetear intentos al iniciar actividad
        currentAttempt = 0;

        SetupScanner();
        SetupTimer();

        productNames = ObjectPoolManager.Instance.GetAvailablePrefabNames(PoolTag.Producto).ToArray();

        activityTimerText = liveTimerText;
        //activityTimeText = successTimeText;

        InitializeCommands();
        OnActivityInitialize();
        ShowInitialInstruction();
    }

    protected virtual void ShowInitialInstruction()
    {
        StartNewAttempt();
    }

    protected virtual void OnActivityInitialize() { }

    protected override void InitializeCommands() { }

    protected virtual void SetupScanner()
    {
        if (scanner != null)
        {
            scanner.UnbindUI(this);
            scanner.ClearUI();
            scanner.BindUI(this, activityProductsText, activityTotalPriceText, true);
        }
    }

    /// <summary>
    /// Configura el timer. 
    /// ⚠️ OPTIMIZADO: Ya NO usa GameObject.Find()
    /// Asigna liveTimerText desde el Inspector.
    /// </summary>
    protected virtual void SetupTimer()
    {
        if (liveTimerText != null)
        {
            activityTimerText = liveTimerText;
            liveTimerText.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning($"[{GetType().Name}] liveTimerText no asignado. Asígnalo en el Inspector.");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FLUJO PRINCIPAL (OPTIMIZADO)
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Inicia un nuevo intento con cliente cacheado.
    /// </summary>
    protected virtual void StartNewAttempt()
    {
        scannedCount = 0;

        // Spawnear cliente
        currentCustomer = customerSpawner.SpawnCustomer();

        // ✅ OPTIMIZADO: Usar cache del spawner en vez de GetComponent
        if (customerSpawner.TryGetCachedComponents(currentCustomer, out var movement, out var client))
        {
            currentCustomerMovement = movement;
            currentClient = client;
        }
        else
        {
            // Fallback si el cache falla
            currentCustomerMovement = currentCustomer.GetComponent<CustomerMovement>();
            currentClient = currentCustomer.GetComponent<Client>();
        }

        // Mover cámara y cliente
        cameraController.MoveToPosition(GetStartCameraPosition(), () =>
        {
            currentCustomerMovement.MoveToCheckout(() =>
            {
                SpawnAndBindProduct();
                OnCustomerReady();
            });
        });
    }

    protected virtual void SpawnAndBindProduct()
    {
        OnBeforeProductSpawn();

        currentProduct = GetPooledProduct(scannedCount, spawnPoint);

        if (currentProduct != null)
        {
            OnAfterProductSpawn(currentProduct);
            BindCurrentProduct(currentProduct);
        }
    }

    protected virtual void BindCurrentProduct()
    {
        if (currentProduct != null)
        {
            BindCurrentProduct(currentProduct);
        }
    }

    protected virtual void BindCurrentProduct(GameObject product)
    {
        if (product == null) return;

        var drag = product.GetComponent<DragObject>();
        if (drag != null)
        {
            drag.OnScanned -= OnProductScannedHandler;
            drag.OnScanned += OnProductScannedHandler;
        }
    }

    private void OnProductScannedHandler(DragObject dragObj)
    {
        if (dragObj == null) return;

        dragObj.OnScanned -= OnProductScannedHandler;

        if (scanner != null)
        {
            scanner.RegisterProductScan(dragObj);
        }

        RegisterProductScanned();
    }

    protected virtual void RegisterProductScanned()
    {
        scannedCount++;
        ReturnCurrentProductToPool();

        if (scannedCount < productsToScan)
        {
            SpawnAndBindProduct();
        }
        else
        {
            OnAllProductsScanned();
        }
    }

    protected virtual void ReturnCurrentProductToPool()
    {
        if (currentProduct == null) return;

        var drag = currentProduct.GetComponent<DragObject>();
        string poolName = (drag != null && !string.IsNullOrEmpty(drag.OriginalPoolName))
            ? drag.OriginalPoolName
            : currentProduct.name;

        ObjectPoolManager.Instance.ReturnToPool(PoolTag.Producto, poolName, currentProduct);
        currentProduct = null;
    }

    protected virtual void OnProductScanned(DragObject dragObj) { }

    protected virtual void MoveToSubtotalPhase()
    {
        cameraController.MoveToPosition(GetSubtotalCameraPosition(), () =>
        {
            foreach (var button in subtotalButtons)
            {
                button.interactable = true;
            }

            AnimateButtonsSequentiallyWithActivation(subtotalButtons);
            OnSubtotalPhaseReady();
        });
    }

    protected virtual void OnSubtotalPhaseReady() { }

    public virtual void HandleSubTotal()
    {
        float totalAmount = GetTotalAmount(activityTotalPriceText);

        if (totalAmount <= 0)
        {
            Debug.LogError($"{GetType().Name}: El total de la compra es inválido.");
            return;
        }

        SoundManager.Instance.PlaySound("success");
        OnSubtotalPressed(totalAmount);
    }

    protected virtual void OnSubtotalPressed(float totalAmount) { }

    // ══════════════════════════════════════════════════════════════════════════════
    // INPUT DE MONTO
    // ══════════════════════════════════════════════════════════════════════════════

    protected virtual void ActivateAmountInput(float amount, Action onInputComplete = null)
    {
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
        {
            button.gameObject.SetActive(false);
        }

        ActivateButtonWithSequence(selectedButtons, 0, () => onInputComplete?.Invoke());
    }

    public virtual void OnNumberButtonPressed(string number)
    {
        if (amountInputField != null)
        {
            amountInputField.text += number;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // TICKET Y FINALIZACIÓN
    // ══════════════════════════════════════════════════════════════════════════════

    protected virtual void SpawnTicket(Action onDelivered = null)
    {
        InstantiateTicket(ticketPrefab, ticketSpawnPoint, ticketTargetPoint, () =>
        {
            onDelivered?.Invoke();
            HandleTicketDelivered();
        });
    }

    protected virtual void HandleTicketDelivered()
    {
        SoundManager.Instance.PlaySound("success");
        currentAttempt++;

        currentCustomerMovement?.MoveToExit();

        if (currentAttempt < maxAttempts)
        {
            OnRestartAttempt();
        }
        else
        {
            OnAllAttemptsComplete();
        }
    }

    protected virtual void ShowActivityComplete()
    {
        StopActivityTimer();
        ResetValues();
        commandManager.commandList.Clear();

        SoundManager.Instance.RestorePreviousMusic();

        var adapter = GetComponent<ActivityMetricsAdapter>();
        if (adapter != null)
        {
            adapter.NotifyActivityCompleted();
        }
        else
        {
            Debug.LogWarning($"[{GetType().Name}] No hay ActivityMetricsAdapter.");
            SoundManager.Instance.PlaySound("win");
            CompleteActivity();
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // RESET Y LIMPIEZA
    // ══════════════════════════════════════════════════════════════════════════════

    protected virtual void ResetValues()
    {
        scannedCount = 0;

        if (amountInputField != null)
            amountInputField.text = "";

        if (scanner != null)
            scanner.ClearUI();

        if (activityProductsText != null)
            activityProductsText.text = "";

        if (activityTotalPriceText != null)
            activityTotalPriceText.text = "$0.00";
    }

    /// <summary>
    /// Inicia la competencia con música y timer.
    /// ✅ El AudioClip ya está precargado desde Awake()
    /// </summary>
    protected virtual void StartCompetition()
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

    protected override void OnDisable()
    {
        base.OnDisable();

        if (scanner != null)
            scanner.UnbindUI(this);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // UTILIDADES
    // ══════════════════════════════════════════════════════════════════════════════

    protected void EnsureProductData(GameObject go)
    {
        if (go == null) return;

        var drag = go.GetComponent<DragObject>();
        if (drag == null) return;

        if (drag.productData == null)
        {
            var p = ScriptableObject.CreateInstance<Product>();
            p.Initialize(
                UnityEngine.Random.Range(1000, 9999).ToString(),
                go.name,
                UnityEngine.Random.Range(20f, 50f),
                1
            );
            drag.productData = p;
        }
    }

    public float GetTotalAmountForDisplay()
    {
        return GetTotalAmount(activityTotalPriceText);
    }

    protected override void Initialize() { }
}