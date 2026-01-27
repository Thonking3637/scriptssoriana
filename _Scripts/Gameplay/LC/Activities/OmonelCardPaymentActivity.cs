using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;
using System;

public class OmonelCardPaymentActivity: ActivityBase
{
    [Header("Tiempo en Actividad")]
    public TextMeshProUGUI liveTimerText;
    public TextMeshProUGUI successTimeText;

    [Header("Configuración de Productos")]
    public List<GameObject> productPrefabs;
    public Transform spawnPoint;
    public ProductScanner scanner;

    [Header("Configuración de la Tarjeta")]
    public CardInteraction cardInteraction;

    [Header("UI Elements")]
    public TextMeshProUGUI activityProductsText;
    public TextMeshProUGUI activityTotalPriceText;
    public List<Button> numberButtons;
    public List<Button> enterLastClicking;
    public List<Button> subtotalButtons;
    public TMP_InputField amountInputField;

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

        if (activityTotalPriceText != null && string.IsNullOrWhiteSpace(activityTotalPriceText.text))
            activityTotalPriceText.text = "$0.00";

        productNames = ObjectPoolManager.Instance.GetAvailablePrefabNames(PoolTag.Producto).ToArray();

        InitializeCommands();

        cardInteraction.OnCardMovedToFirstPosition -= HandleCardArrived;
        cardInteraction.OnCardMovedToSecondPosition -= HandleCardMovedToSecondPosition;
        cardInteraction.OnCardReturned -= HandleCardReturned;

        cardInteraction.OnCardMovedToFirstPosition += HandleCardArrived;
        cardInteraction.OnCardMovedToSecondPosition += HandleCardMovedToSecondPosition;
        cardInteraction.OnCardReturned += HandleCardReturned;

        activityTimerText = liveTimerText;
        activityTimeText = successTimeText;

        UpdateInstructionOnce(0, () =>
        {
            StartNewAttempt();
        });
    }

    protected override void InitializeCommands()
    {
        foreach (var button in enterLastClicking) button.gameObject.SetActive(false);
        foreach (var button in subtotalButtons) button.gameObject.SetActive(false);
        CommandManager.CommandAction enterlastCommand = new CommandManager.CommandAction
        {
            command = "ENTERR",
            customAction = HandleEnterLast,
            requiredActivity = "Day2_PagoOmonel",
            commandButtons = enterLastClicking
        };
        commandManager.commandList.Add(enterlastCommand);

        CommandManager.CommandAction subtotalCommand = new CommandManager.CommandAction
        {
            command = "SUBTOTAL",
            customAction = HandleSubTotal,
            requiredActivity = "Day2_PagoOmonel",
            commandButtons = subtotalButtons
        };
        commandManager.commandList.Add(subtotalCommand);

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
                EnsureProductData(currentProduct);
                BindCurrentProduct();
            });

        });
    }

    public void RegisterProductScanned()
    {

        if (currentProduct != null)
        {
            var drag = currentProduct.GetComponent<DragObject>();

            string poolName = (drag != null && !string.IsNullOrEmpty(drag.OriginalPoolName))
                ? drag.OriginalPoolName
                : currentProduct.name;

            ObjectPoolManager.Instance.ReturnToPool(PoolTag.Producto, poolName, currentProduct);
            currentProduct = null;
        }

        scannedCount++;

        if (scannedCount < productsToScan)
        {
            currentProduct = GetPooledProduct(scannedCount, spawnPoint);
            EnsureProductData(currentProduct);
            BindCurrentProduct();
        }
        else
        {
            cameraController.MoveToPosition("Actividad Omonel SubTotal", () =>
            {
                UpdateInstructionOnce(1);
                ActivateCommandButtons(subtotalButtons);
                AnimateButtonsSequentiallyWithActivation(subtotalButtons);
            });
        }
    }

    public void HandleSubTotal()
    {
        ShowCard();
    }

    private void ShowCard()
    {
        SoundManager.Instance.PlaySound("success");
        cameraController.MoveToPosition("Actividad Omonel Primera Posicion", () =>
        {            
            UpdateInstructionOnce(2);
            cardInteraction.gameObject.SetActive(true);
        });       
    }

    private void HandleCardArrived()
    {
        UpdateInstructionOnce(3);
        cameraController.MoveToPosition("Actividad Omonel Segunda Posicion");
    }

    private void HandleCardMovedToSecondPosition()
    {
        SoundManager.Instance.PlaySound("success");
        UpdateInstructionOnce(4);
        cameraController.MoveToPosition("Actividad Omonel Final Posicion");
    }

    private void HandleCardReturned()
    {
        if (currentCustomer == null)
        {
            Debug.LogError("Error: No hay un cliente asignado en HandleCardReturned().");
            return;
        }

        CustomerMovement customerMovement = currentCustomer.GetComponent<CustomerMovement>();

        if (customerMovement == null)
        {
            Debug.LogError("Error: El cliente actual no tiene un componente 'CustomerMovement'.");
            return;
        }

        customerMovement.MoveToPinEntry(() => ActivatePinInput());
    }

    private void ActivatePinInput()
    {
        cameraController.MoveToPosition("Actividad Omonel Escribir Monto", () =>
        {          
            UpdateInstructionOnce(5);
            float totalAmount = GetTotalAmount(activityTotalPriceText);
            ActivateAmountInput(totalAmount);
        });      
    }

    private void ActivateAmountInput(float amount)
    {
        amountInputField.gameObject.SetActive(true);
        amountInputField.text = "";
        amountInputField.DeactivateInputField();
        amountInputField.ActivateInputField();

        string amountString = ((int)amount).ToString() + "00";

        List<Button> selectedButtons = GetButtonsForAmount(amountString, numberButtons);

        foreach (var button in numberButtons)
        {
            button.gameObject.SetActive(false);
        }

        ActivateButtonWithSequence(selectedButtons, 0, () => ActivateButtonWithSequence(enterLastClicking,0));
    }

    public void HandleEnterLast()
    {
        SoundManager.Instance.PlaySound("success");
        ValidateAmount();
    }

    private void ValidateAmount()
    {
        currentAttempt++;

        if (currentCustomer == null)
        {
            Invoke(nameof(RestartActivity), 1f);
            return;
        }

        CustomerMovement customerMovement = currentCustomer.GetComponent<CustomerMovement>();
        if (customerMovement == null)
        {
            Invoke(nameof(RestartActivity), 1f);
            return;
        }

        customerMovement.MoveToExit();

        if (currentAttempt < maxAttempts)
        {
            cameraController.MoveToPosition("Iniciando Juego", () =>
            {
                UpdateInstructionOnce(6);
                Invoke(nameof(RestartActivity), 1f);
            });
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
        cameraController.MoveToPosition("Actividad Omonel Success", () =>
        {
            continueButton.onClick.RemoveAllListeners();
            SoundManager.Instance.RestorePreviousMusic();
            SoundManager.Instance.PlaySound("win");

            continueButton.onClick.AddListener(() =>
            {
                cameraController.MoveToPosition("Iniciando Juego");              
                cardInteraction.ResetCard();
                CompleteActivity();
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

    private void RestartActivity()
    {
        ResetValues();
        cardInteraction.ResetCard();

        DOVirtual.DelayedCall(0.1f, () =>
        {
            RegenerateProductValues();
        });

        DOVirtual.DelayedCall(0.3f, () =>
        {
            UpdateInstructionOnce(7, StartNewAttempt, StartCompetition);
        });
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

    }
    protected override void Initialize()
    {
        
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

        if (scanner != null)
        {
            scanner.RegisterProductScan(obj);
            RegisterProductScanned();
        }
        else
        {
            Debug.LogError("OmonelCardPaymentActivity: scanner es NULL.");
        }
    }



    private void EnsureProductData(GameObject go)
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

    protected override void OnDisable()
    {
        base.OnDisable();
        if (scanner != null) scanner.UnbindUI(this);
    }

}
