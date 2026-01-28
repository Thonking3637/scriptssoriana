using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Clase base para actividades de Línea de Cajas (LC) que comparten flujos comunes:
/// - Spawn de cliente → checkout → escaneo de productos → subtotal → pago → ticket → exit
/// 
/// SCRIPTS QUE PUEDEN HEREDAR DE ESTA CLASE:
/// ─────────────────────────────────────────
/// ✅ CashPaymentActivity      - Pago en efectivo
/// ✅ CardPaymentActivity      - Pago con tarjeta
/// ✅ OmonelCardPaymentActivity - Pago con tarjeta Omonel
/// ✅ RoundUpCentsActivity     - Redondeo de centavos
/// ✅ RedeemPointsActivity     - Canje de puntos
/// ✅ AngryClientVelocity      - Cliente molesto (velocidad)
/// 
/// SCRIPTS QUE REQUIEREN EVALUACIÓN (flujos más especializados):
/// ─────────────────────────────────────────────────────────────
/// ⚠️ AngryClientCalmDown     - Tiene sistema de emociones único
/// ⚠️ AngryClientPrice        - Tiene cambio de precio y supervisor
/// ⚠️ SuspensionReactivacion  - Flujo de suspensión/reactivación muy diferente
/// ⚠️ PriceCheckActivity      - No tiene flujo de pago, solo consulta
/// ⚠️ ScanActivity            - Solo escaneo masivo, sin pago
/// ⚠️ AlertCashWithdrawal     - Flujo de retiro de efectivo diferente
/// </summary>
public abstract class LCPaymentActivityBase : ActivityBase
{
    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN COMÚN (antes duplicada en cada actividad)
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("LC Base - Timer")]
    [SerializeField] protected TextMeshProUGUI liveTimerText;
    [SerializeField] protected TextMeshProUGUI successTimeText;

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

    [Header("LC Base - Panel Success")]
    [SerializeField] protected Button continueButton;

    // ══════════════════════════════════════════════════════════════════════════════
    // ESTADO COMÚN (antes duplicado en cada actividad)
    // ══════════════════════════════════════════════════════════════════════════════

    protected GameObject currentProduct;
    protected GameObject currentCustomer;
    protected CustomerMovement currentCustomerMovement;
    protected Client currentClient;

    protected int scannedCount = 0;
    protected int currentAttempt = 0;
    protected int maxAttempts = 3;

    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN ABSTRACTA (cada actividad define)
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>Posición de cámara al iniciar (ej: "Iniciando Juego")</summary>
    protected abstract string GetStartCameraPosition();

    /// <summary>Posición de cámara para subtotal (ej: "Actividad Billete SubTotal")</summary>
    protected abstract string GetSubtotalCameraPosition();

    /// <summary>Posición de cámara para success (ej: "Actividad Billete Success")</summary>
    protected abstract string GetSuccessCameraPosition();

    /// <summary>ID de la actividad para comandos (ej: "Day2_PagoEfectivo")</summary>
    protected abstract string GetActivityCommandId();

    // ══════════════════════════════════════════════════════════════════════════════
    // HOOKS VIRTUALES (las hijas pueden sobrescribir si necesitan)
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>Llamado después de que el cliente llega al checkout</summary>
    protected virtual void OnCustomerReady() { }

    /// <summary>Llamado después de escanear todos los productos</summary>
    protected virtual void OnAllProductsScanned()
    {
        MoveToSubtotalPhase();
    }

    /// <summary>Llamado cuando se debe reiniciar el intento</summary>
    protected virtual void OnRestartAttempt()
    {
        ResetValues();
        RegenerateProductValues();
        StartNewAttempt();
    }

    /// <summary>Llamado cuando se completan todos los intentos</summary>
    protected virtual void OnAllAttemptsComplete()
    {
        ShowActivityComplete();
    }

    /// <summary>Llamado antes de spawnear el producto (para modificaciones)</summary>
    protected virtual void OnBeforeProductSpawn() { }

    /// <summary>Llamado después de spawnear el producto (para EnsureProductData, etc.)</summary>
    protected virtual void OnAfterProductSpawn(GameObject product) { }

    // ══════════════════════════════════════════════════════════════════════════════
    // INICIALIZACIÓN (StartActivity común)
    // ══════════════════════════════════════════════════════════════════════════════

    public override void StartActivity()
    {
        base.StartActivity();

        // Configurar scanner
        SetupScanner();

        // Obtener nombres de productos del pool
        productNames = ObjectPoolManager.Instance.GetAvailablePrefabNames(PoolTag.Producto).ToArray();

        // Configurar timer
        activityTimerText = liveTimerText;
        activityTimeText = successTimeText;

        // Inicializar comandos específicos de la actividad hija
        InitializeCommands();

        // Hook para inicialización adicional de la hija
        OnActivityInitialize();

        // ⚠️ IMPORTANTE: La base NO llama UpdateInstructionOnce aquí
        // Cada actividad tiene diferentes índices de instrucción
        // La hija debe llamar a ShowInitialInstruction() que internamente 
        // decide qué instrucción mostrar y cuándo llamar a StartNewAttempt
        ShowInitialInstruction();
    }

    /// <summary>
    /// Hook para mostrar la instrucción inicial. Las hijas DEBEN sobrescribir
    /// para llamar UpdateInstructionOnce con su índice correcto.
    /// Por defecto llama a StartNewAttempt directamente.
    /// </summary>
    protected virtual void ShowInitialInstruction()
    {
        // Default: ir directo a StartNewAttempt
        // Las hijas sobrescriben: UpdateInstructionOnce(0, StartNewAttempt);
        StartNewAttempt();
    }

    /// <summary>Hook para inicialización adicional en clases hijas</summary>
    protected virtual void OnActivityInitialize() { }

    protected virtual void SetupScanner()
    {
        if (scanner != null)
        {
            scanner.UnbindUI(this);
            scanner.ClearUI();
            scanner.BindUI(this, activityProductsText, activityTotalPriceText, true);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FLUJO PRINCIPAL: Cliente → Checkout → Productos → Subtotal
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Inicia un nuevo intento: spawna cliente y lo mueve al checkout.
    /// Antes estaba duplicado en ~8 actividades.
    /// </summary>
    protected virtual void StartNewAttempt()
    {
        scannedCount = 0;

        // Spawnear cliente
        currentCustomer = customerSpawner.SpawnCustomer();
        currentCustomerMovement = currentCustomer.GetComponent<CustomerMovement>();
        currentClient = currentCustomer.GetComponent<Client>();

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

    /// <summary>
    /// Spawna el siguiente producto y lo bindea para escaneo.
    /// Antes estaba duplicado en ~8 actividades.
    /// </summary>
    protected virtual void SpawnAndBindProduct()
    {
        OnBeforeProductSpawn();

        currentProduct = GetPooledProduct(scannedCount, spawnPoint);

        if (currentProduct != null)
        {
            OnAfterProductSpawn(currentProduct);
            BindCurrentProduct();
        }
    }

    /// <summary>
    /// Bindea el producto actual al evento OnScanned.
    /// Antes estaba duplicado EXACTAMENTE IGUAL en ~8 actividades.
    /// </summary>
    protected void BindCurrentProduct()
    {
        if (currentProduct == null) return;

        var drag = currentProduct.GetComponent<DragObject>();
        if (drag == null) return;

        // Anti-duplicados por reuse de pool
        drag.OnScanned -= OnProductScanned;
        drag.OnScanned += OnProductScanned;
    }

    /// <summary>
    /// Callback cuando un producto es escaneado.
    /// Antes estaba duplicado en ~8 actividades.
    /// </summary>
    protected virtual void OnProductScanned(DragObject obj)
    {
        if (obj == null) return;

        obj.OnScanned -= OnProductScanned;

        if (scanner != null)
        {
            scanner.RegisterProductScan(obj);
            RegisterProductScanned();
        }
        else
        {
            Debug.LogError($"{GetType().Name}: scanner es NULL.");
        }
    }

    /// <summary>
    /// Registra el producto escaneado y decide si spawnear otro o avanzar a subtotal.
    /// Antes estaba duplicado en ~8 actividades (¡y con bugs como el scannedCount++ faltante!).
    /// </summary>
    protected virtual void RegisterProductScanned()
    {
        scannedCount++; // ✅ IMPORTANTE: Esto faltaba en CashPaymentActivity

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

    /// <summary>
    /// Devuelve el producto actual al pool de objetos.
    /// Antes estaba duplicado con variaciones menores en ~8 actividades.
    /// </summary>
    protected void ReturnCurrentProductToPool()
    {
        if (currentProduct == null) return;

        var drag = currentProduct.GetComponent<DragObject>();
        string poolName = drag?.GetOriginalPoolNameSafe() ?? currentProduct.name;

        ObjectPoolManager.Instance.ReturnToPool(PoolTag.Producto, poolName, currentProduct);
        currentProduct = null;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FASE SUBTOTAL
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Mueve a la fase de subtotal con animación de cámara y botones.
    /// ⚠️ NO llama UpdateInstructionOnce - eso lo hace OnSubtotalPhaseReady()
    /// </summary>
    protected virtual void MoveToSubtotalPhase()
    {
        cameraController.MoveToPosition(GetSubtotalCameraPosition(), () =>
        {
            // Hook para que la hija muestre la instrucción correcta
            OnSubtotalPhaseReady();
            AnimateButtonsSequentiallyWithActivation(subtotalButtons);
        });
    }

    /// <summary>
    /// Hook llamado cuando la cámara llega a la posición de subtotal.
    /// Las hijas sobrescriben para llamar UpdateInstructionOnce con su índice.
    /// </summary>
    protected virtual void OnSubtotalPhaseReady() { }

    /// <summary>
    /// Maneja el botón de Subtotal. Las hijas sobrescriben para lógica específica.
    /// </summary>
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

    /// <summary>Hook llamado después de presionar subtotal con el monto total</summary>
    protected virtual void OnSubtotalPressed(float totalAmount) { }

    // ══════════════════════════════════════════════════════════════════════════════
    // INPUT DE MONTO (compartido por efectivo, tarjeta, etc.)
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Activa el input de monto con los botones numéricos.
    /// Antes estaba duplicado en ~6 actividades.
    /// </summary>
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

    /// <summary>
    /// Callback para botones numéricos.
    /// Antes estaba duplicado EXACTAMENTE IGUAL en ~6 actividades.
    /// </summary>
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

    /// <summary>
    /// Genera el ticket y maneja la entrega.
    /// </summary>
    protected virtual void SpawnTicket(Action onDelivered = null)
    {
        InstantiateTicket(ticketPrefab, ticketSpawnPoint, ticketTargetPoint, () =>
        {
            onDelivered?.Invoke();
            HandleTicketDelivered();
        });
    }

    /// <summary>
    /// Maneja la entrega del ticket: sonido, cliente sale, decide si reiniciar o completar.
    /// Antes estaba duplicado en ~6 actividades.
    /// </summary>
    protected virtual void HandleTicketDelivered()
    {
        SoundManager.Instance.PlaySound("success");
        currentAttempt++;

        // Cliente sale
        currentCustomerMovement?.MoveToExit();

        // Decidir siguiente paso
        if (currentAttempt < maxAttempts)
        {
            OnRestartAttempt();
        }
        else
        {
            OnAllAttemptsComplete();
        }
    }

    /// <summary>
    /// Muestra el panel de éxito y configura el botón de continuar.
    /// Antes estaba duplicado en ~8 actividades.
    /// </summary>
    protected virtual void ShowActivityComplete()
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
    // RESET Y LIMPIEZA
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Resetea los valores para un nuevo intento.
    /// Antes estaba duplicado con variaciones menores en ~8 actividades.
    /// </summary>
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
    /// Inicia la fase de competencia con música y timer.
    /// Antes estaba duplicado en ~5 actividades.
    /// </summary>
    protected virtual void StartCompetition()
    {
        if (activityMusicClip != null)
        {
            SoundManager.Instance.SetActivityMusic(activityMusicClip, 0.2f, false);
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

    /// <summary>
    /// Asegura que el producto tenga ProductData (útil para productos del pool sin datos).
    /// Movido aquí porque se usa en varias actividades.
    /// </summary>
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

    /// <summary>
    /// Obtiene el total como float para cálculos.
    /// </summary>
    public float GetTotalAmountForDisplay()
    {
        return GetTotalAmount(activityTotalPriceText);
    }

    /// <summary>
    /// Initialize vacío por defecto (requerido por ActivityBase).
    /// Las hijas pueden sobrescribir si necesitan.
    /// </summary>
    protected override void Initialize() { }
}