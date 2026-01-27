using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class RoundUpCentsActivity : ActivityBase
{
    [Header("Timer")]
    [SerializeField] private TextMeshProUGUI liveTimerText;
    [SerializeField] private TextMeshProUGUI successTimeText;

    [Header("Product Configuration")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private ProductScanner scanner;

    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI activityProductsText;
    [SerializeField] private TextMeshProUGUI activityTotalPriceText;
    [SerializeField] private TextMeshProUGUI roundUpQuestionText;
    [SerializeField] private GameObject roundUpPanel;
    [SerializeField] private List<Button> subtotalButtons;
    [SerializeField] private List<Button> enterButtons;
    [SerializeField] private List<Button> commandCardButtons;
    [SerializeField] private List<Button> numberButtons;
    [SerializeField] private List<Button> enterLastClicking;
    [SerializeField] private TMP_InputField amountInputField;

    [Header("Configuración del Cliente")]
    [SerializeField] private CustomerSpawner customerSpawner;

    [Header("Payment")]
    [SerializeField] private GameObject ticketPrefab;
    [SerializeField] private Transform ticketSpawnPoint;
    [SerializeField] private Transform ticketTargetPoint;

    [Header("Panel Button")]
    public Button continueButton;

    private GameObject currentProduct;
    private GameObject currentCustomer;
    private Client currentClient;
    private int scannedCount = 0;
    private int productsToScan = 4;
    private float totalAmount;

    private int currentAttempt = 0;
    private const int maxAttempts = 3;

    public override void StartActivity()
    {
        base.StartActivity();

        productNames = ObjectPoolManager.Instance.GetAvailablePrefabNames(PoolTag.Producto).ToArray();

        activityTimerText = liveTimerText;
        activityTimeText = successTimeText;

        InitializeCommands();

        scanner.BindUI(this, activityProductsText, activityTotalPriceText, true);

        UpdateInstructionOnce(0, StartNewAttempt);
    }

    protected override void InitializeCommands()
    {
        foreach (var button in subtotalButtons) button.gameObject.SetActive(false);
        foreach (var button in enterButtons) button.gameObject.SetActive(false);
        foreach (var button in enterLastClicking) button.gameObject.SetActive(false);
        foreach (var button in commandCardButtons) button.gameObject.SetActive(false);

        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "SUBTOTAL",
            customAction = HandleSubTotal,
            requiredActivity = "Day3_RedondeoCentavos",
            commandButtons = subtotalButtons
        });

        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "ENTER",
            customAction = HandleEnter,
            requiredActivity = "Day3_RedondeoCentavos",
            commandButtons = enterButtons
        });
    }

    private void StartNewAttempt()
    {
        scannedCount = 0;

        currentCustomer = customerSpawner.SpawnCustomer();
        currentClient = currentCustomer.GetComponent<Client>();

        cameraController.MoveToPosition("Iniciando Juego", () =>
        {
            currentCustomer.GetComponent<CustomerMovement>().MoveToCheckout(() =>
            {
                currentProduct = GetPooledProduct(scannedCount, spawnPoint);
            });
        });
    }

    public void RegisterProductScanned()
    {
        if (currentProduct == null) return;

        ObjectPoolManager.Instance.ReturnToPool(PoolTag.Producto, currentProduct.name, currentProduct);
        currentProduct = null;
        scannedCount++;

        if (scannedCount < productsToScan)
        {
            currentProduct = GetPooledProduct(scannedCount, spawnPoint);
            ApplyRandomDecimalToPrice(currentProduct);
        }
        else
        {
            cameraController.MoveToPosition("Actividad 3 Subtotal", () =>
            {
                UpdateInstructionOnce(1);
                AnimateButtonsSequentiallyWithActivation(subtotalButtons);
            });
        }
    }

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


    public void HandleSubTotal()
    {
        SoundManager.Instance.PlaySound("success");
        totalAmount = GetTotalAmount(activityTotalPriceText);
        float rounded = Mathf.Ceil(totalAmount);
        float roundUpAmount = Mathf.Round((rounded - totalAmount) * 100f) / 100f;

        roundUpPanel.SetActive(true);
        roundUpQuestionText.text = $"¿Redondear ${roundUpAmount:F2}?";

        UpdateInstructionOnce(2, () =>
        {
            Invoke(nameof(AskClientAboutRounding), 1f);
        });
    }

    private void AskClientAboutRounding()
    {
        float rounded = Mathf.Ceil(totalAmount);
        float roundUpAmount = Mathf.Round((rounded - totalAmount) * 100f) / 100f;
        string roundUpText = roundUpAmount.ToString("F2");

        cameraController.MoveToPosition("Actividad 3 Cliente Camera", () =>
        {
            DialogSystem.Instance.ShowClientDialog(
                currentClient,
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
                            DialogSystem.Instance.HideDialog(false); // Desactiva panel sin reactivar instrucciones
                            ActionEnterBeforeClient();
                        },
                        () => SoundManager.Instance.PlaySound("error")
                    );
                });
        });

    }

    public void ActionEnterBeforeClient()
    {
        cameraController.MoveToPosition("Actividad 3 Presionar Enter", () =>
        {
            UpdateInstructionOnce(3);
            ActivateCommandButtons(enterButtons);
            ActivateButtonWithSequence(enterButtons, 0);
        });

    }
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
                        currentClient,
                        dialog: "Con tarjeta",
                        onComplete: () =>
                        {
                            DialogSystem.Instance.HideDialog(false);
                            ActivateAmountInput(roundedTotal);
                        });
                });
        });

    }


    private void ActivateAmountInput(float amount)
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
                            cameraController.MoveToPosition("Actividad 3 Tarjeta Mirar Cliente", () =>
                            {
                                CustomerMovement customerMovement = currentCustomer.GetComponent<CustomerMovement>();

                                if (customerMovement != null)
                                {
                                    customerMovement.MoveToPinEntry(() =>
                                    {
                                        UpdateInstructionOnce(6, () =>
                                        {
                                            SoundManager.Instance.PlaySound("success");
                                            InstantiateTicket(ticketPrefab, ticketSpawnPoint, ticketTargetPoint, HandleTicketDelivered);
                                        });
                                    });
                                }
                            });
                        });
                    });
                });              
            });
        });
        
    }
    private void HandleTicketDelivered()
    {
        SoundManager.Instance.PlaySound("success");

        currentAttempt++;

        CustomerMovement customerMovement = currentCustomer.GetComponent<CustomerMovement>();
        customerMovement.MoveToExit();

        if (currentAttempt < maxAttempts)
        {
            RestartActivity();
        }
        else
        {
            ActivityComplete();
        }
    }
    private void RestartActivity()
    {
        ResetValues();
        RegenerateProductValues();
        UpdateInstructionOnce(7, StartNewAttempt, StartCompetition);
    }
    private void ResetValues()
    {
        scannedCount = 0;
        amountInputField.text = "";

        if (scanner != null)
        {
            scanner.ClearUI();
        }

        if (activityProductsText != null) activityProductsText.text = "";
        if (activityTotalPriceText != null) activityTotalPriceText.text = "$0.00";
    }

    public void ActivityComplete()
    {
        StopActivityTimer();
        ResetValues();
        commandManager.commandList.Clear();
        cameraController.MoveToPosition("Actividad 3 Success", () =>
        {
            continueButton.onClick.RemoveAllListeners();
            SoundManager.Instance.RestorePreviousMusic();
            SoundManager.Instance.PlaySound("win");

            continueButton.onClick.AddListener(() =>
            {
                cameraController.MoveToPosition("Iniciando Juego");
                CompleteActivity();
            });
        });
    }

    public void StartCompetition()
    {
        SoundManager.Instance.SetActivityMusic(activityMusicClip, 0.2f, false);
        liveTimerText.GetComponent<TextMeshProUGUI>().enabled = true;
        StartActivityTimer();
    }

    public void OnNumberButtonPressed(string number)
    {
        if (amountInputField != null)
        {
            amountInputField.text += number;
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (scanner != null) scanner.UnbindUI(this);
    }

    protected override void Initialize()
    {
        
    }
}
