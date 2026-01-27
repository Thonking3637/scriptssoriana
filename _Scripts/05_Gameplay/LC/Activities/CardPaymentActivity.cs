using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using System;

public class CardPaymentActivity : ActivityBase
{
    [Header("Tiempo en Actividad")]
    public TextMeshProUGUI liveTimerText;
    public TextMeshProUGUI successTimeText;

    [Header("Product Configuration")]
    public List<GameObject> productPrefabs;
    public Transform spawnPoint;
    public ProductScanner scanner;

    [Header("UI Elements")]
    public TextMeshProUGUI activityProductsText;
    public TextMeshProUGUI activityTotalPriceText;
    public List<Button> subtotalButtons;
    public List<Button> numberButtons;
    public List<Button> enterAmountButtons;
    public List<Button> enterlastclicking;
    public TMP_InputField amountInputField;

    [Header("Ticket Configuration")]
    public GameObject ticketPrefab;
    public Transform ticketSpawnPoint;
    public Transform ticketTargetPoint;

    [Header("Configuración del Cliente")]
    public CustomerSpawner customerSpawner;
    private GameObject currentCustomer;
    public Transform pinEntryPoint;
    public Transform checkoutPoint;

    [Header("Panel Success")]
    public Button continueButton;

    private GameObject currentProduct;
    private int scannedCount = 0;
    private int productsToScan = 4;
    private int currentAttempt = 0;
    private const int maxAttempts = 3;


    protected override void Initialize() { }

    public override void StartActivity()
    {
        base.StartActivity();

        if (scanner != null)
        {
            scanner.UnbindUI(this);
            scanner.ClearUI();
            scanner.BindUI(this, activityProductsText, activityTotalPriceText, true);
        }

        productNames = ObjectPoolManager.Instance.GetAvailablePrefabNames(PoolTag.Producto).ToArray();

        InitializeCommands();

        UpdateInstructionOnce(0, () =>
        {
            StartNewAttempt();
        });
    
        activityTimerText = liveTimerText;
        activityTimeText = successTimeText;
    }

    protected override void InitializeCommands()
    {
        foreach (var button in subtotalButtons) button.gameObject.SetActive(false);
        foreach (var button in enterAmountButtons) button.gameObject.SetActive(false);
        foreach(var button in enterlastclicking) button.gameObject.SetActive(false);

        CommandManager.CommandAction subtotalCommand = new CommandManager.CommandAction
        {
            command = "SUBTOTAL",
            customAction = () => {
                ActivateCommandButtons(subtotalButtons);
                HandleSubTotal();
            },
            requiredActivity = "Day2_PagoTarjeta",
            commandButtons = subtotalButtons
        };
        commandManager.commandList.Add(subtotalCommand);

        CommandManager.CommandAction enterAmountCommand = new CommandManager.CommandAction
        {
            command = "T+B+ENTERR",
            customAction = HandleEnterAmount,        
            requiredActivity = "Day2_PagoTarjeta",
            commandButtons = enterAmountButtons
        };
        commandManager.commandList.Add(enterAmountCommand);

        CommandManager.CommandAction enterlastCommand = new CommandManager.CommandAction
        {
            command = "ENTERRR",
            customAction = HandleEnterLast,
            requiredActivity = "Day2_PagoTarjeta",
            commandButtons = enterlastclicking
        };
        commandManager.commandList.Add(enterlastCommand);
    }

    private void StartNewAttempt()
    {
        scannedCount = 0;

        currentCustomer = customerSpawner.SpawnCustomer();
        CustomerMovement customerMovement = currentCustomer.GetComponent<CustomerMovement>();

        cameraController.MoveToPosition("Iniciando Juego", () =>       
        {
            customerMovement.MoveToCheckout(() =>
            {
                currentProduct = GetPooledProduct(scannedCount, spawnPoint);
                BindCurrentProduct();
            });
        });
    }

    public void RegisterProductScanned()
    {
        scannedCount++;

        if (currentProduct != null)
        {
            var drag = currentProduct.GetComponent<DragObject>();
            if (drag != null)
            {
                string poolName = drag.GetOriginalPoolNameSafe();
                ObjectPoolManager.Instance.ReturnToPool(PoolTag.Producto, poolName, currentProduct);
            }
            currentProduct = null;
        }

        if (scannedCount < productsToScan)
        {
            currentProduct = GetPooledProduct(scannedCount, spawnPoint);
            BindCurrentProduct();
        }
        else
        {
            cameraController.MoveToPosition("Actividad Tarjeta SubTotal", () =>
            {
                UpdateInstructionOnce(1);
                AnimateButtonsSequentiallyWithActivation(subtotalButtons);
            });
        }
    }

    public void HandleSubTotal()
    {
        //paymentStarted = true;
        float totalAmount = GetTotalAmount(activityTotalPriceText);

        if (totalAmount <= 0)
        {
            Debug.LogError("Error: El total de la compra es inválido.");
            return;
        }

        SoundManager.Instance.PlaySound("success");

        cameraController.MoveToPosition("Actividad Tarjeta SubTotal Pressed", () =>
        {
            UpdateInstructionOnce(2);
            ActivateButtonWithSequence(enterAmountButtons, 0, () => HandleEnterAmount());
        });
    }

    public void HandleEnterAmount()
    {
        SoundManager.Instance.PlaySound("success");
        float totalAmount = GetTotalAmount(activityTotalPriceText);      
        ActivateAmountInput(totalAmount);
    }

    private void ActivateAmountInput(float amount)
    {
        SoundManager.Instance.PlaySound("success");

        cameraController.MoveToPosition("Actividad Tarjeta Escribir Monto");
        UpdateInstructionOnce(3);

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

        ActivateButtonWithSequence(selectedButtons, 0, () =>
        {
            ActivateButtonWithSequence(enterlastclicking, 0, () =>
            {
                UpdateInstructionOnce(4, () =>
                {
                    cameraController.MoveToPosition("Actividad Tarjeta Mirar Cliente", () =>
                    {
                        CustomerMovement customerMovement = currentCustomer.GetComponent<CustomerMovement>();

                        if (customerMovement != null)
                        {
                            customerMovement.MoveToPinEntry(() =>
                            {
                                UpdateInstructionOnce(5, () =>
                                {
                                    InstantiateTicket(ticketPrefab, ticketSpawnPoint, ticketTargetPoint, HandleTicketDelivered);
                                });                               
                            });
                        }
                    });
                });               
            });
        });
    }
    public void OnNumberButtonPressed(string number)
    {
        if (amountInputField != null)
        {
            amountInputField.text += number;
        }
    }

    public void HandleEnterLast()
    {
        //SoundManager.Instance.PlaySound("success");       
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

    public void ActivityComplete()
    {
        StopActivityTimer();
        ResetValues();
        commandManager.commandList.Clear();
        cameraController.MoveToPosition("Actividad Tarjeta Success", () =>
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

    private void RestartActivity()
    {
        ResetValues();
        RegenerateProductValues();
        UpdateInstructionOnce(6, StartNewAttempt, StartCompetition);
    }

    private void ResetValues()
    {
        scannedCount = 0;
        //paymentStarted = false;
        amountInputField.text = "";

        if (scanner != null)
        {
            scanner.ClearUI();
        }

        if (activityProductsText != null) activityProductsText.text = "";
        if (activityTotalPriceText != null) activityTotalPriceText.text = "$0.00";

    }

    public void StartCompetition()
    {
        SoundManager.Instance.SetActivityMusic(activityMusicClip, 0.2f, false);
        liveTimerText.GetComponent<TextMeshProUGUI>().enabled = true;
        StartActivityTimer();
    }
 
    private void BindCurrentProduct()
    {
        if (currentProduct == null) return;

        var drag = currentProduct.GetComponent<DragObject>();
        if (drag == null) return;

        drag.OnScanned -= OnProductScanned; // anti duplicados por reuse de pool
        drag.OnScanned += OnProductScanned;
    }

    private void OnProductScanned(DragObject obj)
    {
        if (obj == null) return;

        obj.OnScanned -= OnProductScanned;

        scanner.RegisterProductScan(obj);

        RegisterProductScanned();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (scanner != null) scanner.UnbindUI(this);
    }
}
