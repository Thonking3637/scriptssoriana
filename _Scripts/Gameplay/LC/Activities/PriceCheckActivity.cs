using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;

public class PriceCheckActivity : ActivityBase
{
    public AudioClip scanActivityMusic;

    [Header("Tiempo en Actividad")]
    public TextMeshProUGUI liveTimerText;
    public TextMeshProUGUI successTimeText;

    [Header("Configuración de Productos")]
    public Transform spawnPoint;
    public ProductScanner scanner;

    [Header("UI de la Actividad")]
    public GameObject priceCheckPanel;
    public GameObject productHUD;
    public TextMeshProUGUI productInfoText;
    public TextMeshProUGUI questionpanelText;
    public TextMeshProUGUI priceQuestionText;
    public Button[] priceOptions;
    public Button continueButton;
    public GameObject successPanel;
    public GameObject questionPanel;

    [Header("Comandos")]
    public List<Button> consultaPrecioButtons;
    public List<Button> borrarButtons;

    [Header("Efecto de Parpadeo")]
    public Image screenFlash;
    public Color correctColor = new Color(0, 1, 0, 0.5f);
    public Color wrongColor = new Color(1, 0, 0, 0.5f);
    public float flashDuration = 0.5f;

    [Header("Configuración del Cliente")]
    public CustomerSpawner customerSpawner;

    private int currentProductIndex = 0;
    private GameObject currentProduct;
    private List<Product> usedProducts = new();
    private int correctAnswerIndex;
    private bool isActivityActive = true;
    private bool isQuestionPhase = false;
    private bool canSpawnProduct = true;

    private int lastProductIndex = -1;
    private Product lastScannedProduct;
    private GameObject currentCustomer;

    private int currentAttempt = 0;
    private const int maxAttempts = 4;

    protected override void Start()
    {
        priceCheckPanel.SetActive(false);
        productHUD.SetActive(false);
        successPanel.SetActive(false);
    }

    public override void StartActivity()
    {
        base.StartActivity();

        productNames = ObjectPoolManager.Instance.GetAvailablePrefabNames(PoolTag.Producto).ToArray();
        activityTimerText = liveTimerText;
        activityTimeText = successTimeText;

        InitializeCommands();

        UpdateInstructionOnce(0, StartNewAttempt);
    }

    public void StartNewAttempt()
    {
        if (currentAttempt >= maxAttempts)
        {
            AskFinalQuestion();
            return;
        }

        currentAttempt++;

        currentCustomer = customerSpawner.SpawnCustomer();
        CustomerMovement customerMovement = currentCustomer.GetComponent<CustomerMovement>();

        cameraController.MoveToPosition("Iniciando Juego", () =>
        {
            customerMovement.MoveToCheckout(() =>
            {
                cameraController.MoveToPosition("Actividad Consulta Botones", () =>
                {
                    UpdateInstructionOnce(1);
                    canSpawnProduct = true;
                    ActivateConsultaPrecio();
                });
            });
        });
    }


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

    private void HandleConsultaPrecio()
    {
        if (!isActivityActive || isQuestionPhase || !canSpawnProduct || cameraController.isMoving) return;

        canSpawnProduct = false;
        SoundManager.Instance.PlaySound("success");

        cameraController.MoveToPosition("Actividad Escaneo", () =>
        {
            UpdateInstructionOnce(2);
            SpawnNextProductFromPool();
        });
    }

    private void SpawnNextProductFromPool()
    {
        if (!isActivityActive || currentProduct != null) return;

        GameObject prefab = ObjectPoolManager.Instance.GetRandomPrefabFromPool(PoolTag.Producto, ref lastProductIndex);
        if (prefab == null) return;

        currentProduct = ObjectPoolManager.Instance.GetFromPool(PoolTag.Producto, prefab.name);
        if (currentProduct == null) return;

        currentProduct.transform.position = spawnPoint.position;
        currentProduct.transform.rotation = prefab.transform.rotation;
        currentProduct.SetActive(true);

        DragObject drag = currentProduct.GetComponent<DragObject>();
        drag?.SetOriginalPoolName(prefab.name);

        if (drag?.productData != null)
        {
            usedProducts.Add(drag.productData);
        }

        if (drag != null)
        {
            drag.OnScanned -= HandleProductScanned;
            drag.OnScanned += HandleProductScanned;
        }

        currentProductIndex++;
    }

    private void HandleProductScanned(DragObject obj)
    {
        if (!isActivityActive || obj == null) return;

        obj.OnScanned -= HandleProductScanned;

        currentProduct = obj.gameObject;

        RegisterProductScanned();
    }

    public void RegisterProductScanned()
    {
        if (currentProduct == null) return;

        UpdateInstructionOnce(3);

        DragObject drag = currentProduct.GetComponent<DragObject>() ?? currentProduct.GetComponentInChildren<DragObject>();

        if (drag?.productData != null)
        {
            lastScannedProduct = ScriptableObject.CreateInstance<Product>();
            lastScannedProduct.Initialize(drag.productData.code, drag.productData.productName, drag.productData.price, drag.productData.quantity);
        }

        ObjectPoolManager.Instance.ReturnToPool(PoolTag.Producto, drag.OriginalPoolName, currentProduct);

        currentProduct = null;

        ShowProductInfo();

        cameraController.MoveToPosition("Vista Monitor Consulta Precio", () =>
        {
            productHUD.SetActive(true);
            AnimateButtonsSequentially(borrarButtons);
        });
    }

    private void ShowProductInfo()
    {
        if (lastScannedProduct != null)
        {
            productInfoText.text = $"Código: {lastScannedProduct.code}\nProducto: {lastScannedProduct.productName}\nPrecio: ${lastScannedProduct.price}";
        }
        else
        {
            productInfoText.text = "No hay producto escaneado.";
        }
    }

    private void HandleBorrar()
    {
        SoundManager.Instance.PlaySound("success");
        productHUD.SetActive(false);
        RestartActivity();
        UpdateInstructionOnce(4, StartNewAttempt, StartCompetition);
    }

    public void StartCompetition()
    {;
        SoundManager.Instance.SetActivityMusic(activityMusicClip, 0.2f, false);
        liveTimerText.enabled = true;
        StartActivityTimer();
    }

    public void RestartActivity()
    {
        if (currentCustomer != null)
        {
            CustomerMovement movement = currentCustomer.GetComponent<CustomerMovement>();
            movement?.MoveToExit();
        }
    }

    private void AskFinalQuestion()
    {
        isQuestionPhase = true;  
        cameraController.MoveToPosition("Actividad Pregunta Precio", () =>
        {
            SoundManager.Instance.SetActivityMusic(scanActivityMusic, 0.2f, false);
            questionPanel.SetActive(true);
            questionPanel.transform.localScale = Vector3.zero;
            questionPanel.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);

            Product questionProduct = usedProducts[Random.Range(0, usedProducts.Count)];
            questionpanelText.text = $"¿Cuál es el costo de: {questionProduct.productName}?";

            correctAnswerIndex = Random.Range(0, priceOptions.Length);
            List<int> usedPrices = new() { (int)questionProduct.price };

            for (int i = 0; i < priceOptions.Length; i++)
            {
                if (i == correctAnswerIndex)
                {
                    priceOptions[i].GetComponentInChildren<TextMeshProUGUI>().text = $"${questionProduct.price}";
                    priceOptions[i].onClick.AddListener(HandleCorrectAnswer);
                }
                else
                {
                    int fakePrice;
                    do
                    {
                        fakePrice = Random.Range(50, 101);
                    } while (usedPrices.Contains(fakePrice));

                    usedPrices.Add(fakePrice);
                    priceOptions[i].GetComponentInChildren<TextMeshProUGUI>().text = $"${fakePrice}";
                    priceOptions[i].onClick.AddListener(HandleWrongAnswer);
                }
            }
        });
    }

    private void HandleCorrectAnswer()
    {
        SoundManager.Instance.RestorePreviousMusic();
        SoundManager.Instance.PlaySound("success");
        FlashScreen(correctColor);

        cameraController.MoveToPosition("Actividad Pregunta Precio Final", () =>
        {
            SoundManager.Instance.PlaySound("win");
            StopActivityTimer();
            successPanel.SetActive(true);

            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(() =>
            {
                successPanel.SetActive(false);
                ContinueToNextActivity();

            });
        });

        foreach (var button in priceOptions) button.onClick.RemoveAllListeners();
    }

    private void ContinueToNextActivity()
    {
        cameraController.MoveToPosition("Iniciando Vista Monitor", () =>
        {
            CompleteActivity();
        });
    }

    private void HandleWrongAnswer()
    {
        FlashScreen(wrongColor);
        SoundManager.Instance.PlaySound("error");
    }

    private void ActivateConsultaPrecio()
    {
        foreach (var button in consultaPrecioButtons)
        {
            button.interactable = true;
        }

        AnimateButtonsSequentially(consultaPrecioButtons);
    }

    private void FlashScreen(Color color)
    {
        if (screenFlash == null) return;

        screenFlash.gameObject.SetActive(true);
        screenFlash.color = color;
        screenFlash.canvasRenderer.SetAlpha(1f);

        screenFlash.DOFade(0, flashDuration).SetEase(Ease.OutQuad).OnComplete(() =>
        {
            screenFlash.gameObject.SetActive(false);
            screenFlash.canvasRenderer.SetAlpha(0f);
        });
    }

    protected override void Initialize() { }
}
