using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class CashPaymentActivity : ActivityBase
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
    public List<Button> efectivoButtons;
    public TMP_InputField amountInputField;

    [Header("Payment Configuration")]
    public MoneySpawner moneySpawner;
    public CustomerPayment customerPayment;

    [Header("Money Panel Configuration")]
    public GameObject moneyPanel;
    public Vector2 moneyPanelStartPos;
    public Vector2 moneyPanelEndPos;
    public Vector2 moneyPanelHidePos;

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

        customerPayment.OnAllCustomerMoneyCollected -= ActivateAmountInput;
        customerPayment.OnAllCustomerMoneyCollected += ActivateAmountInput;

        InitializeCommands();

        UpdateInstructionOnce(0, () =>
        {
            StartNewAttempt();
        });

        foreach (var button in efectivoButtons)
        {
            button.interactable = false;

        }

        activityTimerText = liveTimerText;
        activityTimeText = successTimeText;
    }

    protected override void InitializeCommands()
    {
        CommandManager.CommandAction subTotalCommand = new CommandManager.CommandAction
        {
            command = "SUBTOTAL",
            customAction = HandleSubTotal,
            requiredActivity = "Day2_PagoEfectivo",
            commandButtons = subtotalButtons
        };
        commandManager.commandList.Add(subTotalCommand);

        CommandManager.CommandAction efectivoCommand = new CommandManager.CommandAction
        {
            command = "EFECTIVO",
            customAction = HandleEfectivo,
            requiredActivity = "Day2_PagoEfectivo",
            commandButtons = efectivoButtons,
        };
        commandManager.commandList.Add(efectivoCommand);
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
        if (currentProduct != null)
        {
            var drag = currentProduct.GetComponent<DragObject>();
            string poolName = (drag != null) ? drag.GetOriginalPoolNameSafe() : currentProduct.name;

            ObjectPoolManager.Instance.ReturnToPool(PoolTag.Producto, poolName, currentProduct);
            currentProduct = null;
        }

        if (scannedCount < productsToScan)
        {
            currentProduct = GetPooledProduct(scannedCount, spawnPoint);
            BindCurrentProduct();
        }
        else
        {
            cameraController.MoveToPosition("Actividad Billete SubTotal", () =>
            {
                UpdateInstructionOnce(1);
                AnimateButtonsSequentiallyWithActivation(subtotalButtons);
            });
        }
        
    }
    public void HandleSubTotal()
    {
        float totalAmount = GetTotalAmount(activityTotalPriceText);

        if (totalAmount <= 0)
        {
            Debug.LogError("Error: El total de la compra es inválido.");
            return;
        }

        moneySpawner.UpdateTotalPurchaseText(totalAmount);

        SoundManager.Instance.PlaySound("success");

        cameraController.MoveToPosition("Actividad Billete Recoger Efectivo", () =>
        {
            UpdateInstructionOnce(2);
            customerPayment.GenerateCustomerPayment(totalAmount);           
        });
    }

    public void ActivateAmountInput(float amount)
    {
        SoundManager.Instance.PlaySound("success");

        cameraController.MoveToPosition("Actividad Billete Escribir Efectivo", () =>
        {
            UpdateInstructionOnce(3);
        });

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

        if (selectedButtons.Count > 0)
        {
            ActivateButtonWithSequence(selectedButtons, 0, () =>
            {
                foreach (var button in efectivoButtons) button.interactable = true;
                AnimateButtonsSequentiallyWithActivation(efectivoButtons);              
            });
        }
    }

    public void HandleEfectivo()
    {
        SoundManager.Instance.PlaySound("success");
        cameraController.MoveToPosition("Actividad Billete Dar Cambio", () =>
        {
            UpdateInstructionOnce(4);
            MoneyManager.OpenMoneyPanel(moneyPanel, moneyPanelStartPos, moneyPanelEndPos);          
        });      
    }

    public void OnCorrectChangeGiven()
    {
        MoneyManager.CloseMoneyPanel(moneyPanel, moneyPanelHidePos);
        SoundManager.Instance.PlaySound("success");
        cameraController.MoveToPosition("Actividad Billete Dar Ticket", () =>
        {
            UpdateInstructionOnce(5);
            InstantiateTicket(ticketPrefab, ticketSpawnPoint, ticketTargetPoint, HandleTicketDelivered);
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

    public void ActivityComplete()
    {
        StopActivityTimer();
        ResetValues();
        commandManager.commandList.Clear();
        cameraController.MoveToPosition("Actividad Billete Success", () =>
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

    public void StartCompetition()
    {
        SoundManager.Instance.SetActivityMusic(activityMusicClip, 0.2f, false);
        liveTimerText.GetComponent<TextMeshProUGUI>().enabled = true;
        StartActivityTimer();
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

        moneySpawner.ResetMoneyUI();
        customerPayment.ResetCustomerPayment();
    }

    public void OnNumberButtonPressed(string number)
    {
        if (amountInputField != null)
        {
            amountInputField.text += number;
        }
    }

    public float GetTotalAmountForDisplay()
    {
        return GetTotalAmount(activityTotalPriceText);
    }

    protected override void Initialize()
    {
       
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (customerPayment != null)
            customerPayment.OnAllCustomerMoneyCollected -= ActivateAmountInput;

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

        obj.OnScanned -= OnProductScanned;

        if (scanner != null)
        {
            scanner.RegisterProductScan(obj);
            RegisterProductScanned();
        }
        else
        {
            Debug.LogError("CashPaymentActivity: scanner es NULL.");
        }
    }
}