using DG.Tweening;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AngryClientPrice : ActivityBase
{
    [Header("Configuración de Productos")]
    public Transform spawnPoint;
    public ProductScanner scanner;

    [Header("UI de Actividad")]
    public TextMeshProUGUI activityProductsText;
    public TextMeshProUGUI activityTotalPriceText;
    public List<Button> subtotalButtons;
    public List<Button> commandChangePrice;
    public List<Button> commandCardButtons;
    public Button continueButton;

    [Header("Cliente y Comentarios")]
    public CustomerSpawner customerSpawner;
    private GameObject currentCustomer;
    private int scannedCount = 0;
    private const int productsToScan = 4;

    [Header("Cambio de precio")]
    public GameObject supervisorPasswordPanel;
    public TextMeshProUGUI passwordText;
    public GameObject priceInputPanel;
    public TMP_InputField amountChangePriceInputField;
    public TMP_InputField amountInputField;
    public List<Button> numberButtons;
    public List<Button> enterButtons;
    public List<Button> enterChangePrice;

    private DragObject lastScannedProduct;

    [Header("Ticket")]
    public GameObject ticketPrefab;
    public Transform ticketSpawnPoint;
    public Transform ticketTargetPoint;

    [Header("Supervisor")]
    public SupervisorButton supervisorButton;
    public GameObject supervisorPrefab;
    public Transform supervisorSpawnPoint;
    public Transform supervisorEntryPoint;
    public List<Transform> supervisorMiddlePath;
    public Transform supervisorExitPoint;
    private GameObject currentSupervisor;

    private Client currentClient;
    private GameObject currentProduct;
    private int lastProductIndex = -1;
    private List<DragObject> scannedProducts = new();

    private TMP_InputField currentInputField;

    public override void StartActivity()
    {
        base.StartActivity();

        if (supervisorButton != null)
        {
            supervisorButton.gameObject.SetActive(false);
            supervisorButton.OnPressed -= OnSupervisorButtonPressed;
            supervisorButton.OnPressed += OnSupervisorButtonPressed;
        }
        else
        {
            Debug.LogError("AngryClientPrice: supervisorButton (SupervisorButton) no está asignado.");
        }

        InitializeCommands();
        scanner.BindUI(this, activityProductsText, activityTotalPriceText, true);

        productNames = ObjectPoolManager.Instance.GetAvailablePrefabNames(PoolTag.Producto).ToArray();

        UpdateInstructionOnce(0, () =>
        {
            DOVirtual.DelayedCall(0.5f, () =>
            {
                currentCustomer = customerSpawner.SpawnCustomer();
                currentClient = currentCustomer.GetComponent<Client>();
                CustomerMovement customerMovement = currentCustomer.GetComponent<CustomerMovement>();

                cameraController.MoveToPosition("Iniciando Juego", () =>
                {
                    customerMovement.MoveToCheckout(() =>
                    {
                        UpdateInstructionOnce(1, () =>
                        {
                            SpawnNextProduct();
                        });
                    });
                });
            });
           
        });
    }

    protected override void InitializeCommands()
    {
        base.InitializeCommands();

        foreach (var button in subtotalButtons) button.gameObject.SetActive(false);
        foreach (var button in enterButtons) button.gameObject.SetActive(false);
        foreach (var button in commandChangePrice) button.gameObject.SetActive(false);
        foreach (var button in enterChangePrice) button.gameObject.SetActive(false);
        foreach (var button in commandCardButtons) button.gameObject.SetActive(false);

        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "D+C+ENTER_",
            customAction = HandleChangePassword,
            requiredActivity = "Day4_ClientePrecioCambiado",
            commandButtons = commandChangePrice
        });

        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "SUBTOTAL",
            customAction = HandleSubTotal,
            requiredActivity = "Day4_ClientePrecioCambiado",
            commandButtons = subtotalButtons
        });

        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "T+B+ENTER____",
            customAction = HandleEnterAmount,
            requiredActivity = "Day4_ClientePrecioCambiado",
            commandButtons = commandCardButtons
        });


    }
    private void SpawnNextProduct()
    {
        if (scannedCount >= productsToScan) return;

        GameObject prefab = ObjectPoolManager.Instance.GetRandomPrefabFromPool(PoolTag.Producto, ref lastProductIndex);
        if (prefab == null) return;

        currentProduct = ObjectPoolManager.Instance.GetFromPool(PoolTag.Producto, prefab.name);
        if (currentProduct == null) return;

        currentProduct.transform.position = spawnPoint.position;
        currentProduct.transform.rotation = prefab.transform.rotation;
        currentProduct.transform.SetParent(null);
        currentProduct.SetActive(true);

        DragObject drag = currentProduct.GetComponent<DragObject>();
        drag?.SetOriginalPoolName(prefab.name);

        BindCurrentProduct();
    }

    public void RegisterProductScanned()
    {
        if (currentProduct == null) return;

        lastScannedProduct = currentProduct.GetComponent<DragObject>();
        scannedProducts.Add(lastScannedProduct);

        string poolName = (lastScannedProduct != null && !string.IsNullOrEmpty(lastScannedProduct.OriginalPoolName))
            ? lastScannedProduct.OriginalPoolName
            : currentProduct.name;

        ObjectPoolManager.Instance.ReturnToPool(PoolTag.Producto, poolName, currentProduct);
        currentProduct = null;

        scannedCount++;

        if (scannedCount < productsToScan) SpawnNextProduct();
        else UpdateInstructionOnce(2, AskCurrentQuestion);
    }



    private void AskCurrentQuestion()
    {
        cameraController.MoveToPosition("Actividad 1 Cliente Camera", () =>
        {
            DialogSystem.Instance.ShowClientDialog(
                currentClient,
                "Hey, ese precio está mal, allá se mostraba $10 menos.",
                () =>
                {
                    DialogSystem.Instance.ShowClientDialogWithOptions(
                        "¿Cómo deberias de responder ante esta duda?",
                        new List<string>
                        {
                            $"Llamaré a mi supervisora, espere un momento por favor.",
                            "Ese es el precio, si no le gusta puede irse.",
                            "Yo no tengo la culpa, así viene en el sistema.",
                            "No sé, pregúntele a otra persona."
                        },
                        $"Llamaré a mi supervisora, espere un momento por favor.",
                        () =>
                        {
                            SoundManager.Instance.PlaySound("success");
                            UpdateInstructionOnce(3, ShowSupervisorButton);
                        },
                        () => SoundManager.Instance.PlaySound("error")
                        );
                });

        });
    }

    private void ShowSupervisorButton()
    {
        cameraController.MoveToPosition("Vista Boton Supervisora", () =>
        {
            if (supervisorButton != null)
            {
                supervisorButton.gameObject.SetActive(true);
            }
        });
    }

    public void OnSupervisorButtonPressed()
    {
        if (supervisorButton != null)
            supervisorButton.gameObject.SetActive(false);

        cameraController.MoveToPosition("Iniciando Juego", () =>
        {
            UpdateInstructionOnce(4, () => { SpawnSupervisor(); });           
        });
    }

    private void SpawnSupervisor()
    {
        GameObject supervisorGO = Instantiate(supervisorPrefab, supervisorSpawnPoint.position, supervisorPrefab.transform.rotation);
        currentSupervisor = supervisorGO;

        SupervisorMovement movement = supervisorGO.GetComponent<SupervisorMovement>();

        movement.entryPoint = supervisorEntryPoint;
        movement.middlePath = supervisorMiddlePath;
        movement.exitPoint = supervisorExitPoint;
        movement.animator = supervisorGO.GetComponent<Animator>();

        movement.GoToEntryPoint(() =>
        {
            cameraController.MoveToPosition("Actividad Supervisor Camera", () =>
            {
                 ShowSupervisorDialog();           
            });
        });

    }

    private void ShowSupervisorDialog()
    {
        DialogSystem.Instance.ShowClientDialog(
            currentSupervisor.GetComponent<Client>(),
            "¿Qué sucedió? Cuéntame.",
            () =>
            {
                DialogSystem.Instance.ShowClientDialogWithOptions(
                    "¿Qué le debes decir a la supervisora?",
                    new List<string>
                    {
                    "El cliente menciona que el precio está mal",
                    "Ese cliente está loco",
                    "No entiendo qué quiere",
                    "No sé para qué vino"
                    },
                    "El cliente menciona que el precio está mal",
                    OnCorrectSupervisorAnswer,
                    OnWrongSupervisorAnswer
                );
            });
    }

    private void OnCorrectSupervisorAnswer()
    {
        SoundManager.Instance.PlaySound("success");

        DOVirtual.DelayedCall(0.5f, () =>
        {
            DialogSystem.Instance.ShowClientDialog(
                currentSupervisor.GetComponent<Client>(),
                "Sí, es correcto, en este momento lo corregiremos.",
                () =>
                {
                    DialogSystem.Instance.HideDialog();
                    SupervisorMovement movement = currentSupervisor.GetComponent<SupervisorMovement>();

                    cameraController.MoveToPosition("Actividad 3 Cajera", () =>
                    {
                        movement.GoThroughMiddlePath(() =>
                        {
                            UpdateInstructionOnce(5);
                            cameraController.MoveToPosition("Actividad 3 Cambiar Precio", () =>
                            {
                                ActivateButtonWithSequence(commandChangePrice, 0, HandleChangePassword);
                            });                          
                        });
                    });
                });
        });
    }
    private void HandleChangePassword()
    {
        UpdateInstructionOnce(6, () =>
        {
            supervisorPasswordPanel.SetActive(true);
            AnimatePasswordEntry(() =>
            {
                supervisorPasswordPanel.SetActive(false);
                ShowPriceInputPanel();
            });
        });      
    }

    private void ShowPriceInputPanel()
    {
        priceInputPanel.SetActive(true);

        if (amountChangePriceInputField != null)
        {
            amountChangePriceInputField.text = "";
            amountChangePriceInputField.gameObject.SetActive(true);
            amountChangePriceInputField.DeactivateInputField();
            amountChangePriceInputField.ActivateInputField();
            currentInputField = amountChangePriceInputField;
        }

        float newPrice = Mathf.Max(0, lastScannedProduct.productData.price - 10);
        string priceString = ((int)newPrice).ToString() + "00";
        print(priceString);
        List<Button> selectedButtons = GetButtonsForAmount(priceString, numberButtons);

        foreach (var button in numberButtons)
        {
            button.gameObject.SetActive(false);
        }
        UpdateInstructionOnce(7, () =>
        {
            ActivateButtonWithSequence(selectedButtons, 0, () =>
            {
                ActivateButtonWithSequence(enterChangePrice, 0, () =>
                {
                    ConfirmPriceChange();
                });
            });
        });      
    }

    private void ConfirmPriceChange()
    {
        if (!float.TryParse(amountChangePriceInputField.text, out float rawValue)) return;

        float newPrice = rawValue / 100f;

        string targetName = lastScannedProduct.productData.productName;

        foreach (var product in scannedProducts)
        {
            if (product.productData.productName == targetName)
            {
                product.productData.price = newPrice;
            }
        }
        SupervisorMovement movement = currentSupervisor.GetComponent<SupervisorMovement>();

        movement.GoToExit(() =>
        {
            priceInputPanel.SetActive(false);
            RebuildScannerUIText();

            cameraController.MoveToPosition("Actividad 3 Subtotal", () =>
            {
                UpdateInstructionOnce(8);
                AnimateButtonsSequentiallyWithActivation(subtotalButtons);
            });
        });        
    }
    private void RebuildScannerUIText()
    {
        float total = 0f;
        activityProductsText.text = "";

        foreach (var product in scannedProducts)
        {
            float subtotal = product.productData.price * product.productData.quantity;
            total += subtotal;

            activityProductsText.text += $"{product.productData.code} - {product.productData.productName} - {product.productData.quantity} - ${subtotal:F2}\n";
        }

        activityTotalPriceText.text = $"${total:F2}";
    }

    public void OnNumberButtonPressed(string number)
    {
        if (currentInputField != null)
        {
            currentInputField.text += number;
        }
    }

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

    private void OnWrongSupervisorAnswer()
    {
        SoundManager.Instance.PlaySound("error");
    }

    public void HandleSubTotal() {

        float totalAmount = GetTotalAmount(activityTotalPriceText);

        if (totalAmount <= 0)
            return;

        SoundManager.Instance.PlaySound("success");

        cameraController.MoveToPosition("Actividad 1 Cliente Camera", () =>
        {
            DialogSystem.Instance.ShowClientDialog(
                "Tu",
                dialog: "Disculpe, ¿Cuál sera su metódo de pago?",
                onComplete: () =>
                {
                    DialogSystem.Instance.ShowClientDialog(
                        currentClient,
                        dialog: "Con tarjeta, apurese",
                        onComplete: () =>
                        {
                            DialogSystem.Instance.HideDialog(false);
                            cameraController.MoveToPosition("Actividad 3 Subtotal", () =>
                            {
                                UpdateInstructionOnce(9);
                                ActivateCommandButtons(commandCardButtons);
                                ActivateButtonWithSequence(commandCardButtons, 0, HandleEnterAmount);
                            });
                        });
                });
        });
    }

    public void HandleEnterAmount() 
    {
        float totalAmount = GetTotalAmount(activityTotalPriceText);
        ActivateAmountInput(totalAmount);
    }
    private void ActivateAmountInput(float amount)
    {
        SoundManager.Instance.PlaySound("success");

        cameraController.MoveToPosition("Actividad 3 Escribir Monto");

        if (amountChangePriceInputField != null)
        {
            amountInputField.text = "";
            amountInputField.gameObject.SetActive(true);
            amountInputField.DeactivateInputField();
            amountInputField.ActivateInputField();

            currentInputField = amountInputField;
        }

        string amountString = ((int)amount).ToString() + "00";
        List<Button> selectedButtons = GetButtonsForAmount(amountString, numberButtons);

        foreach (var button in numberButtons)
        {
            button.gameObject.SetActive(false);
        }
        UpdateInstructionOnce(10);
        ActivateButtonWithSequence(selectedButtons, 0, () =>
        {
            ActivateButtonWithSequence(enterButtons, 0, () =>
            {
                cameraController.MoveToPosition("Actividad 1 Mirar Cliente", () =>
                {
                    CustomerMovement customerMovement = currentCustomer.GetComponent<CustomerMovement>();
                    if (customerMovement != null)
                    {
                        customerMovement.MoveToPinEntry(() =>
                        {
                            UpdateInstructionOnce(11, () =>
                            {
                                InstantiateTicket(ticketPrefab, ticketSpawnPoint, ticketTargetPoint, HandleTicketDelivered);
                            });
                        });
                    }
                });
            });
        });
    }

    private void HandleTicketDelivered()
    {
        SoundManager.Instance.PlaySound("success");

        CustomerMovement customerMovement = currentCustomer.GetComponent<CustomerMovement>();
        customerMovement.MoveToExit();

        EndActivityCheck();
    }

    private void EndActivityCheck()
    {
        ActivityComplete();
    }

    private void ActivityComplete()
    {
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

    protected override void Initialize() { }

    private void ResetValues()
    {
        scannedCount = 0;
        currentProduct = null;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (supervisorButton != null)
            supervisorButton.OnPressed -= OnSupervisorButtonPressed;

        if (scanner != null) scanner.UnbindUI(this);
    }

    private void BindCurrentProduct()
    {
        if (currentProduct == null) return;

        var drag = currentProduct.GetComponent<DragObject>();
        if (drag == null) return;

        drag.OnScanned -= OnProductScanned;
        drag.OnScanned += OnProductScanned;
    }

    private void OnProductScanned(DragObject obj)
    {
        if (obj == null) return;

        obj.OnScanned -= OnProductScanned; // anti x2 pool

        if (scanner != null)
        {
            scanner.RegisterProductScan(obj); // UI + total
            RegisterProductScanned();         // ✅ AVANCE (spawn siguiente / preguntas)
        }
        else
        {
            Debug.LogError("AngryClientPrice: scanner es NULL.");
        }
    }
}
