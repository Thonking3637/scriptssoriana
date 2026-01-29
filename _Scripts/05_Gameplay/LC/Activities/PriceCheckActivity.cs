using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Actividad de Consulta de Precios
/// Flujo: Usuario consulta 4 productos → Pregunta final sobre precio de uno
/// Sistema de tracking: correctAnswers/wrongAnswers para el adapter
/// </summary>
public class PriceCheckActivity : ActivityBase
{
    // ══════════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN
    // ══════════════════════════════════════════════════════════════════════════════

    [Header("Audio")]
    public AudioClip scanActivityMusic;

    [Header("Tiempo en Actividad")]
    public TextMeshProUGUI liveTimerText;
    public TextMeshProUGUI successTimeText;

    [Header("Configuración de Productos")]
    public Transform spawnPoint;
    public ProductScanner scanner;

    [Header("UI - Consulta de Precio")]
    public GameObject priceCheckPanel;
    public GameObject productHUD;
    public TextMeshProUGUI productInfoText;

    [Header("UI - Pregunta Final")]
    public GameObject questionPanel;
    public TextMeshProUGUI questionpanelText;
    public Button[] priceOptions;

    [Header("Comandos")]
    public List<Button> consultaPrecioButtons;
    public List<Button> borrarButtons;

    [Header("Efecto Visual")]
    public Image screenFlash;
    public Color correctColor = new Color(0, 1, 0, 0.5f);
    public Color wrongColor = new Color(1, 0, 0, 0.5f);
    public float flashDuration = 0.5f;

    [Header("Configuración del Cliente")]
    public CustomerSpawner customerSpawner;

    // ══════════════════════════════════════════════════════════════════════════════
    // TRACKING PARA EL ADAPTER (públicos para reflection)
    // ══════════════════════════════════════════════════════════════════════════════

    [HideInInspector] public int correctAnswers = 0;
    [HideInInspector] public int wrongAnswers = 0;

    // ══════════════════════════════════════════════════════════════════════════════
    // VARIABLES PRIVADAS
    // ══════════════════════════════════════════════════════════════════════════════

    private GameObject currentCustomer;
    private GameObject currentProduct;
    private Product lastScannedProduct;
    private List<Product> usedProducts = new();

    private int currentAttempt = 0;
    private const int maxAttempts = 4;
    private int currentProductIndex = 0;
    private int lastProductIndex = -1;
    private int correctAnswerIndex;

    private bool isActivityActive = true;
    private bool isQuestionPhase = false;
    private bool canSpawnProduct = true;

    // ══════════════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void Start()
    {
        priceCheckPanel.SetActive(false);
        productHUD.SetActive(false);
    }

    public override void StartActivity()
    {
        base.StartActivity();

        // Configurar pool de productos
        productNames = ObjectPoolManager.Instance.GetAvailablePrefabNames(PoolTag.Producto).ToArray();

        // Configurar UI de timer
        activityTimerText = liveTimerText;
        activityTimeText = successTimeText;

        // Resetear contadores para el adapter
        correctAnswers = 0;
        wrongAnswers = 0;

        // Inicializar comandos
        InitializeCommands();

        // Comenzar primera consulta
        UpdateInstructionOnce(0, StartNewAttempt);
    }

    protected override void Initialize() { }

    // ══════════════════════════════════════════════════════════════════════════════
    // COMANDOS
    // ══════════════════════════════════════════════════════════════════════════════

    protected override void InitializeCommands()
    {
        base.InitializeCommands();

        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "CONSULTA_PRECIO",
            customAction = HandleConsultaPrecio,
            requiredActivity = "Day1_ConsultaPrecio",
            commandButtons = consultaPrecioButtons
        });

        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "BORRAR",
            customAction = HandleBorrar,
            requiredActivity = "Day1_ConsultaPrecio",
            commandButtons = borrarButtons
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FLUJO PRINCIPAL - CONSULTAS
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Inicia un nuevo intento de consulta (máximo 4)
    /// </summary>
    public void StartNewAttempt()
    {
        // Si ya completó 4 consultas, pasar a la pregunta final
        if (currentAttempt >= maxAttempts)
        {
            AskFinalQuestion();
            return;
        }

        currentAttempt++;

        // Spawnear cliente
        currentCustomer = customerSpawner.SpawnCustomer();
        CustomerMovement customerMovement = currentCustomer.GetComponent<CustomerMovement>();

        // Mover cliente a caja
        cameraController.MoveToPosition("Iniciando Juego", () =>
        {
            customerMovement.MoveToCheckout(() =>
            {
                cameraController.MoveToPosition("Actividad Consulta Botones", () =>
                {
                    UpdateInstructionOnce(1); // "Presiona CONSULTA PRECIO"
                    canSpawnProduct = true;
                    ActivateConsultaPrecio();
                });
            });
        });
    }

    /// <summary>
    /// Activa los botones de CONSULTA PRECIO
    /// </summary>
    private void ActivateConsultaPrecio()
    {
        foreach (var button in consultaPrecioButtons)
        {
            button.interactable = true;
        }

        AnimateButtonsSequentially(consultaPrecioButtons);
    }

    /// <summary>
    /// Maneja el comando CONSULTA_PRECIO
    /// </summary>
    private void HandleConsultaPrecio()
    {
        if (!isActivityActive || isQuestionPhase || !canSpawnProduct || cameraController.isMoving)
            return;

        canSpawnProduct = false;
        SoundManager.Instance.PlaySound("success");

        cameraController.MoveToPosition("Actividad Escaneo", () =>
        {
            UpdateInstructionOnce(2); // "Escanea el producto"
            SpawnNextProductFromPool();
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // MANEJO DE PRODUCTOS
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Genera un producto aleatorio del pool
    /// </summary>
    private void SpawnNextProductFromPool()
    {
        if (!isActivityActive || currentProduct != null)
            return;

        // Obtener prefab aleatorio
        GameObject prefab = ObjectPoolManager.Instance.GetRandomPrefabFromPool(
            PoolTag.Producto,
            ref lastProductIndex
        );

        if (prefab == null)
        {
            Debug.LogWarning("[PriceCheck] No se encontró prefab válido");
            return;
        }

        // Obtener instancia del pool
        currentProduct = ObjectPoolManager.Instance.GetFromPool(PoolTag.Producto, prefab.name);

        if (currentProduct == null)
        {
            Debug.LogWarning($"[PriceCheck] No hay producto disponible: {prefab.name}");
            return;
        }

        // Posicionar producto
        currentProduct.transform.position = spawnPoint.position;
        currentProduct.transform.rotation = prefab.transform.rotation;
        currentProduct.SetActive(true);

        // Configurar DragObject
        DragObject drag = currentProduct.GetComponent<DragObject>();
        drag?.SetOriginalPoolName(prefab.name);

        // Guardar datos del producto para la pregunta final
        if (drag?.productData != null)
        {
            usedProducts.Add(drag.productData);
        }

        // Suscribirse al evento de escaneo
        if (drag != null)
        {
            drag.OnScanned -= HandleProductScanned;
            drag.OnScanned += HandleProductScanned;
        }

        currentProductIndex++;
    }

    /// <summary>
    /// Callback cuando se escanea un producto
    /// </summary>
    private void HandleProductScanned(DragObject obj)
    {
        if (!isActivityActive || obj == null)
            return;

        obj.OnScanned -= HandleProductScanned;
        currentProduct = obj.gameObject;

        RegisterProductScanned();
    }

    /// <summary>
    /// Registra que el producto fue escaneado y muestra su info
    /// </summary>
    public void RegisterProductScanned()
    {
        if (currentProduct == null)
            return;

        UpdateInstructionOnce(3); // "Revisa la información"

        // Obtener datos del producto
        DragObject drag = currentProduct.GetComponent<DragObject>() ??
                         currentProduct.GetComponentInChildren<DragObject>();

        if (drag?.productData != null)
        {
            lastScannedProduct = ScriptableObject.CreateInstance<Product>();
            lastScannedProduct.Initialize(
                drag.productData.code,
                drag.productData.productName,
                drag.productData.price,
                drag.productData.quantity
            );
        }

        // Devolver producto al pool
        ObjectPoolManager.Instance.ReturnToPool(PoolTag.Producto, drag.OriginalPoolName, currentProduct);
        currentProduct = null;

        // Mostrar información en pantalla
        ShowProductInfo();

        // Mover cámara y activar botón BORRAR
        cameraController.MoveToPosition("Vista Monitor Consulta Precio", () =>
        {
            productHUD.SetActive(true);
            AnimateButtonsSequentially(borrarButtons);
        });
    }

    /// <summary>
    /// Muestra la información del producto escaneado
    /// </summary>
    private void ShowProductInfo()
    {
        if (lastScannedProduct != null)
        {
            productInfoText.text = $"Código: {lastScannedProduct.code}\n" +
                                  $"Producto: {lastScannedProduct.productName}\n" +
                                  $"Precio: ${lastScannedProduct.price}";
        }
        else
        {
            productInfoText.text = "No hay producto escaneado.";
        }
    }

    /// <summary>
    /// Maneja el comando BORRAR (limpiar pantalla y siguiente cliente)
    /// </summary>
    private void HandleBorrar()
    {
        SoundManager.Instance.PlaySound("success");
        productHUD.SetActive(false);
        RestartActivity();
        UpdateInstructionOnce(4, StartNewAttempt, StartCompetition); // "Siguiente cliente"
    }

    /// <summary>
    /// Cliente sale y limpia estado
    /// </summary>
    public void RestartActivity()
    {
        if (currentCustomer != null)
        {
            CustomerMovement movement = currentCustomer.GetComponent<CustomerMovement>();
            movement?.MoveToExit();
        }
    }

    /// <summary>
    /// Inicia el cronómetro de competición
    /// </summary>
    public void StartCompetition()
    {
        SoundManager.Instance.SetActivityMusic(activityMusicClip, 0.2f, false);
        liveTimerText.enabled = true;
        StartActivityTimer();
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // PREGUNTA FINAL
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Muestra la pregunta final sobre el precio de un producto
    /// </summary>
    private void AskFinalQuestion()
    {
        isQuestionPhase = true;

        cameraController.MoveToPosition("Actividad Pregunta Precio", () =>
        {
            SoundManager.Instance.SetActivityMusic(scanActivityMusic, 0.2f, false);

            // Animar panel de pregunta
            questionPanel.SetActive(true);
            questionPanel.transform.localScale = Vector3.zero;
            questionPanel.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);

            // Seleccionar producto aleatorio de los consultados
            Product questionProduct = usedProducts[Random.Range(0, usedProducts.Count)];
            questionpanelText.text = $"¿Cuál es el costo de: {questionProduct.productName}?";

            // Configurar opciones de respuesta
            GenerateAnswerOptions(questionProduct);
        });
    }

    /// <summary>
    /// Genera las opciones de respuesta (1 correcta + 3 incorrectas)
    /// </summary>
    private void GenerateAnswerOptions(Product correctProduct)
    {
        correctAnswerIndex = Random.Range(0, priceOptions.Length);
        List<int> usedPrices = new() { (int)correctProduct.price };

        for (int i = 0; i < priceOptions.Length; i++)
        {
            if (i == correctAnswerIndex)
            {
                // Respuesta correcta
                priceOptions[i].GetComponentInChildren<TextMeshProUGUI>().text =
                    $"${correctProduct.price}";
                priceOptions[i].onClick.RemoveAllListeners();
                priceOptions[i].onClick.AddListener(HandleCorrectAnswer);
            }
            else
            {
                // Respuestas incorrectas
                int fakePrice;
                do
                {
                    fakePrice = Random.Range(50, 101);
                } while (usedPrices.Contains(fakePrice));

                usedPrices.Add(fakePrice);
                priceOptions[i].GetComponentInChildren<TextMeshProUGUI>().text = $"${fakePrice}";
                priceOptions[i].onClick.RemoveAllListeners();
                priceOptions[i].onClick.AddListener(HandleWrongAnswer);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // MANEJO DE RESPUESTAS
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Usuario selecciona la respuesta CORRECTA
    /// </summary>
    private void HandleCorrectAnswer()
    {
        correctAnswers++;

        SoundManager.Instance.RestorePreviousMusic();
        SoundManager.Instance.PlaySound("success");
        FlashScreen(correctColor);

        foreach (var button in priceOptions)
        {
            button.onClick.RemoveAllListeners();
        }

        cameraController.MoveToPosition("Actividad Pregunta Precio Final", () =>
        {
            StopActivityTimer();
            ContinueToNextActivity();
        });
    }

    /// <summary>
    /// Usuario selecciona una respuesta INCORRECTA
    /// </summary>
    private void HandleWrongAnswer()
    {
        // ✅ REGISTRAR ERROR para el adapter
        wrongAnswers++;

        FlashScreen(wrongColor);
        SoundManager.Instance.PlaySound("error");

        // OPCIONAL: Limitar intentos
        if (wrongAnswers >= 3)
        {
            Debug.Log("[PriceCheck] Máximo de errores alcanzado");
            // Aquí podrías forzar continuar o mostrar la respuesta correcta
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FINALIZACIÓN
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Completa la actividad y muestra el panel del adapter
    /// </summary>
    private void ContinueToNextActivity()
    {
        cameraController.MoveToPosition("Iniciando Vista Monitor", () =>
        {
            var adapter = GetComponent<ActivityMetricsAdapter>();

            if (adapter != null)
            {
                // ✅ MENSAJE DINÁMICO según errores cometidos
                if (wrongAnswers == 0)
                {
                    adapter.customMessage = "AHORA SABES CÓMO CONSULTAR EL PRECIO DE UN ARTíCULO.";
                }
                else if (wrongAnswers == 1)
                {
                    adapter.customMessage = "¡Bien hecho! Acertaste después de 1 intento.";
                }
                else if (wrongAnswers <= 3)
                {
                    adapter.customMessage = $"Acertaste después de {wrongAnswers} intentos.";
                }
                else
                {
                    adapter.customMessage = "Necesitas repasar los precios de productos.";
                }

                Debug.Log($"[PriceCheck] Correctas: {correctAnswers}, Incorrectas: {wrongAnswers}");
                adapter.NotifyActivityCompleted();
            }
            else
            {
                Debug.LogError("[PriceCheck] NO se encontró ActivityMetricsAdapter");
                base.CompleteActivity();
            }
        });
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // EFECTOS VISUALES
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Efecto de parpadeo de pantalla (correcto/incorrecto)
    /// </summary>
    private void FlashScreen(Color color)
    {
        if (screenFlash == null)
            return;

        screenFlash.gameObject.SetActive(true);
        screenFlash.color = color;
        screenFlash.canvasRenderer.SetAlpha(1f);

        screenFlash.DOFade(0, flashDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                screenFlash.gameObject.SetActive(false);
                screenFlash.canvasRenderer.SetAlpha(0f);
            });
    }
}