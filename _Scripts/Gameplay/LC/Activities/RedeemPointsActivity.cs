using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using System;
using UnityEngine.UI;

public class RedeemPointsActivity : ActivityBase
{
    [Header("Activity Timer")]
    public TextMeshProUGUI liveTimerText;
    public TextMeshProUGUI successTimeText;

    [Header("Product Configuration")]
    public List<GameObject> productPrefabs;
    public Transform spawnPoint;
    public ProductScanner scanner;

    [Header("Card Configuration")]
    public CardInteraction cardInteraction;

    [Header("UI Elements")]
    public GameObject paymentPanel;
    public GameObject ticketPrefab;
    public Transform ticketSpawnPoint;
    public Transform ticketTargetPoint;
    public TextMeshProUGUI activityProductsText;
    public TextMeshProUGUI activityTotalPriceText;
    public List<Button> subtotalButtons;
    public List<Button> paButtons;
    public List<Button> enterButtons;
    public List<Button> efectivoButtons;

    [Header("Customer Configuration")]
    public CustomerSpawner customerSpawner;
    private GameObject currentCustomer;

    public Button continueButton;

    private GameObject currentProduct;
    private int scannedCount = 0;
    private int productsToScan = 4;
    private int currentAttempt = 0;
    private const int maxAttempts = 3;

    protected override void Start()
    {
        paymentPanel.SetActive(false);
    }

    public override void StartActivity()
    {
        base.StartActivity();

        scanner.BindUI(this, activityProductsText, activityTotalPriceText, true);

        productNames = ObjectPoolManager.Instance.GetAvailablePrefabNames(PoolTag.Producto).ToArray();
        cardInteraction.OnCardMovedToFirstPosition += HandleCardArrived;
        cardInteraction.OnCardMovedToSecondPosition += HandleCardMovedToSecondPosition;
        cardInteraction.OnCardReturned += HandleCardReturned;

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
        foreach (var button in paButtons) button.gameObject.SetActive(false);
        foreach (var button in enterButtons) button.gameObject.SetActive(false);
        foreach (var button in efectivoButtons) button.gameObject.SetActive(false);

        CommandManager.CommandAction subtotalCommand = new CommandManager.CommandAction
        {
            command = "SUBTOTAL",
            customAction = () => {
                ActivateCommandButtons(subtotalButtons);
                HandleSubtotalCommand();
            },
            requiredActivity = "Day2_CanjeoPuntos",
            commandButtons = subtotalButtons
        };
        commandManager.commandList.Add(subtotalCommand);

        CommandManager.CommandAction payCommand = new CommandManager.CommandAction
        {
            command = "P+A",
            customAction = HandlePACommand,
            requiredActivity = "Day2_CanjeoPuntos",
            commandButtons = paButtons
        };
        commandManager.commandList.Add(payCommand);

        CommandManager.CommandAction enterCommand = new CommandManager.CommandAction
        {
            command = "ENTER",
            customAction = () => {
                ActivateCommandButtons(enterButtons);
                HandleEnterCommand();
            },
            requiredActivity = "Day2_CanjeoPuntos",
            commandButtons = enterButtons
        };
        commandManager.commandList.Add(enterCommand);

        CommandManager.CommandAction cashCommand = new CommandManager.CommandAction
        {
            command = "EFECTIVO",
            customAction = HandleEfectivoCommand,
            requiredActivity = "Day2_CanjeoPuntos",
            commandButtons = efectivoButtons
        };
        commandManager.commandList.Add(cashCommand);
    }


    private void StartNewAttempt()
    {
        if (currentAttempt >= maxAttempts)
        {
            ActivityComplete();
            return;
        }

        currentAttempt++;
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
            cameraController.MoveToPosition("Actividad Canjeo SubTotal", () =>
            {
                UpdateInstructionOnce(1);
                ActivateCommandButtons(subtotalButtons);
                AnimateButtonsSequentiallyWithActivation(subtotalButtons);
            });
        }
    }


    public void HandleSubtotalCommand()
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
        cameraController.MoveToPosition("Actividad Omonel Segunda Posicion");
        UpdateInstructionOnce(3);
    }

    private void HandleCardMovedToSecondPosition()
    {
        SoundManager.Instance.PlaySound("success");
        UpdateInstructionOnce(4);
        WaitBeforePay();
    }

    private void WaitBeforePay()
    {
        cameraController.MoveToPosition("Actividad Canjeo PA", () =>
        {
            ActivateCommandButtons(paButtons);
            AnimateButtonsSequentiallyWithActivation(paButtons, () => HandlePACommand());
        });       
    }

    public void HandlePACommand()
    {
        SoundManager.Instance.PlaySound("success");
        cameraController.MoveToPosition("Actividad Canjeo Enter", () =>
        {
            UpdateInstructionOnce(5);
            paymentPanel.SetActive(true);
            ActivateCommandButtons(enterButtons);
            AnimateButtonsSequentiallyWithActivation(enterButtons);
        });
    }

    public void HandleEnterCommand()
    {
        SoundManager.Instance.PlaySound("success");
        UpdateInstructionOnce(6);
        ApplyPurchaseDeduction();
    }

    private void ApplyPurchaseDeduction()
    {
        activityProductsText.text += "\n100% de descuento aplicado";
        activityTotalPriceText.text = $"${0.00}";
        paymentPanel.SetActive(false);
        StartCoroutine(WaitForCashCommand());
    }

    private IEnumerator WaitForCashCommand()
    {
        yield return new WaitForSeconds(1f);
        cameraController.MoveToPosition("Actividad Canjeo PA", () =>
        {           
            ActivateCommandButtons(efectivoButtons);
            AnimateButtonsSequentiallyWithActivation(efectivoButtons);
        });
        
    }

    public void HandleEfectivoCommand()
    {
        cameraController.MoveToPosition("Actividad Canjeo Ticket Instantiante", () =>
        {
            SpawnTicket();
        });
    }

    private void SpawnTicket()
    {
        UpdateInstructionOnce(7);
        SoundManager.Instance.PlaySound("success");
        InstantiateTicket(ticketPrefab, ticketSpawnPoint, ticketTargetPoint, HandleTicketDelivered);
    }
    private void HandleTicketDelivered()
    {
        SoundManager.Instance.PlaySound("success");
        CustomerMovement customerMovement = currentCustomer.GetComponent<CustomerMovement>();
        customerMovement.MoveToExit();
        RestartActivity();
    }

    private void RestartActivity()
    {
        ResetValues();
        cardInteraction.ResetCard();
        RegenerateProductValues();
        UpdateInstructionOnce(8, StartNewAttempt, StartCompetition);
    }

    private void ResetValues()
    {
        scannedCount = 0;

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
        HideInstructionsPanel();
        liveTimerText.GetComponent<TextMeshProUGUI>().enabled = true;
        StartActivityTimer();
    }

    private void HandleCardReturned()
    {
        if (currentCustomer == null)
        {
            Debug.LogError("Error: No customer assigned in HandleCardReturned().");
            return;
        }

        CustomerMovement customerMovement = currentCustomer.GetComponent<CustomerMovement>();
        if (customerMovement == null)
        {
            Debug.LogError("Error: The current customer does not have a 'CustomerMovement' component.");
        }
    }
    public void ActivityComplete()
    {
        StopActivityTimer();
        commandManager.commandList.Clear();
        cameraController.MoveToPosition("Actividad Canjeo Success", () =>
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
            Debug.LogError("RedeemPointsActivity: scanner es NULL.");
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
                UnityEngine.Random.Range(10f, 60f),
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
