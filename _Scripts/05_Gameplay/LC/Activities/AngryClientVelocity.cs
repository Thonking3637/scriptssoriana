using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AngryClientVelocity : ActivityBase
{
    [Header("Timer")]
    [SerializeField] private TextMeshProUGUI liveTimerText;
    [SerializeField] private TextMeshProUGUI successTimeText;

    [Header("Client & Product")]
    public CustomerSpawner customerSpawner;
    public Transform productSpawnPoint;
    public ProductScanner scanner;

    [Header("UI")]
    public TextMeshProUGUI activityProductsText;
    public TextMeshProUGUI activityTotalPriceText;
    public TextMeshProUGUI scannedCountText;
    public TextMeshProUGUI remainingTimeText;
    public GameObject failPanel;
    public Button retryButton;
    public Button continueButton;

    [Header("Payment")]
    public TMP_InputField amountInputField;
    public List<Button> numberButtons;
    public List<Button> commandCardButtons;
    public List<Button> enterButtons;
    public List<Button> enterLastClicking;
    public List<Button> subtotalButtons;
    public GameObject ticketPrefab;
    public Transform ticketSpawnPoint;
    public Transform ticketTargetPoint;

    private GameObject currentCustomer;
    private Client currentClient;
    private GameObject currentProduct;
    private int scannedCount = 0;
    private int totalToScan;
    private int currentAttempt = 0;
    private const int maxAttempts = 3;
    private float currentTime;
    private float maxTime;
    private bool timerActive = false;
    private bool challengeStarted = false;

    private Coroutine timerCoroutine;

    public override void StartActivity()
    {
        base.StartActivity();

        if (scanner != null)
        {
            scanner.UnbindUI(this);
            scanner.ClearUI();
            scanner.BindUI(this, activityProductsText, activityTotalPriceText, true);
        }

        InitializeCommands();

        RegisterAngryVelocityDialogs();

        productNames = ObjectPoolManager.Instance.GetAvailablePrefabNames(PoolTag.Producto).ToArray();

        activityTimerText = liveTimerText;
        activityTimeText = successTimeText;

        UpdateInstructionOnce(0, StartNewAttemp);
    }

    protected override void InitializeCommands()
    {
        base.InitializeCommands();

        foreach (var button in subtotalButtons) button.gameObject.SetActive(false);
        foreach (var button in enterButtons) button.gameObject.SetActive(false);
        foreach (var button in enterLastClicking) button.gameObject.SetActive(false);
        foreach (var button in commandCardButtons) button.gameObject.SetActive(false);

        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "SUBTOTAL",
            customAction = HandleSubTotal,
            requiredActivity = "Day4_ClienteMolestoTiempo",
            commandButtons = subtotalButtons
        });

           commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "T+B+ENTER_",
            customAction = HandleEnterAmount,
            requiredActivity = "Day4_ClienteMolestoTiempo",
            commandButtons = commandCardButtons
        });
    }

    private void StartNewAttemp()
    {
        scannedCount = 0;
        currentCustomer = customerSpawner.SpawnCustomer();
        currentClient = currentCustomer.GetComponent<Client>();

        cameraController.MoveToPosition("Iniciando Juego", () =>
        {
            currentCustomer.GetComponent<CustomerMovement>().MoveToCheckout(() =>
            {
                SpawnNextInitialProduct();
            });
        });
    }

    private void SpawnNextInitialProduct()
    {
        if (scannedCount >= 3)
        {
            ShowAngryDialog();
            return;
        }

        GameObject next = GetPooledProduct(scannedCount, productSpawnPoint);
        if (next != null)
        {
            next.SetActive(true);
            currentProduct = next;
            BindCurrentProduct();
        }
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

        if (!challengeStarted)
        {
            SpawnNextInitialProduct();
            return;
        }

        scannedCountText.text = $"{scannedCount} / {totalToScan}";

        if (scannedCount < totalToScan) SpawnNextChallengeProduct();
        else EndChallenge();
    }

    private void ShowAngryDialog()
    {
        var entry = DialogSystem.Instance.GetNextComment("angry_velocity");
        if (entry == null) return;

        UpdateInstructionOnce(1, () =>
        {
            cameraController.MoveToPosition("Actividad 1 Cliente Camera", () =>
            {
                DialogSystem.Instance.ShowClientDialog(
                    currentClient,
                    entry.clientText,
                    () =>
                    {
                        if (!string.IsNullOrEmpty(entry.question))
                        {
                            DialogSystem.Instance.ShowClientDialogWithOptions(
                                entry.question,
                                entry.options,
                                entry.correctAnswer,
                                () => BeginTimedChallenge(),
                                () => SoundManager.Instance.PlaySound("error")
                            );
                        }
                        else
                        {
                            BeginTimedChallenge();
                        }
                    });
            });
        });       
    }

    private void BeginTimedChallenge()
    {
        cameraController.MoveToPosition("Iniciando Juego", () =>
        {
            UpdateInstructionOnce(2, () =>
            {
                challengeStarted = true;
                scannedCount = 0;
                totalToScan = 10 + (currentAttempt * 5);
                maxTime = 15 + (currentAttempt * 5);
                scannedCountText.enabled = true;
                remainingTimeText.enabled = true;
                scannedCountText.text = $"0 / {totalToScan}";
                remainingTimeText.text = $"{maxTime:F0}s";
                StartTimer();
                SpawnNextChallengeProduct();
            });
        });
    }

    private void SpawnNextChallengeProduct()
    {
        if (scannedCount < totalToScan)
        {
            GameObject p = GetPooledProduct(scannedCount % productNames.Length, productSpawnPoint);

            if (p != null)
            {
                p.SetActive(true);
                currentProduct = p;
                BindCurrentProduct();
            }
        }
    }

    private void StartTimer()
    {
        timerActive = true;
        currentTime = maxTime;

        if (timerCoroutine != null)
            StopCoroutine(timerCoroutine);

        timerCoroutine = StartCoroutine(UpdateChallengeTimer());
    }

    private IEnumerator UpdateChallengeTimer()
    {
        while (currentTime > 0 && timerActive)
        {
            currentTime -= Time.deltaTime;
            remainingTimeText.text = $"{currentTime:F0}s";
            yield return null;
        }

        if (scannedCount < totalToScan)
        {
            timerActive = false;
            HandleFail();
        }
    }

    private void EndChallenge()
    {
        timerActive = false;

        scannedCountText.enabled = false;
        remainingTimeText.enabled = false;

        cameraController.MoveToPosition("Actividad 1 Subtotal", () =>
        {
            UpdateInstructionOnce(3, () =>
            {
                AnimateButtonsSequentiallyWithActivation(subtotalButtons);
            });
        });
    }
    public void HandleSubTotal()
    {
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
                            cameraController.MoveToPosition("Actividad 1 Subtotal", () =>
                            {
                                UpdateInstructionOnce(4);
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

        cameraController.MoveToPosition("Actividad 1 Escribir Monto");

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
        UpdateInstructionOnce(5);
        ActivateButtonWithSequence(selectedButtons, 0, () =>
        {
            ActivateButtonWithSequence(enterLastClicking, 0, () =>
            {
                cameraController.MoveToPosition("Actividad 1 Mirar Cliente", () =>
                {
                    CustomerMovement customerMovement = currentCustomer.GetComponent<CustomerMovement>();
                    if (customerMovement != null)
                    {
                        customerMovement.MoveToPinEntry(() =>
                        {
                            UpdateInstructionOnce(6, () =>
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

        currentAttempt++;

        CustomerMovement customerMovement = currentCustomer.GetComponent<CustomerMovement>();
        customerMovement.MoveToExit();
        
        if (currentAttempt < maxAttempts)
        {
            RestartActivityWithCheck();
        }
        else
        {
            ActivityComplete();
        }
    }

    private void RestartActivityWithCheck()
    {
        ResetValues();
        RegenerateProductValues();

        if (currentAttempt > 0)
            UpdateInstructionOnce(7, StartNewAttemp, StartCompetition);
        else
            StartNewAttemp();
    }

    private void ActivityComplete()
    {
        StopActivityTimer();
        commandManager.commandList.Clear();
        scanner.ClearUI();
        cameraController.MoveToPosition("Actividad 1 Success", () =>
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

    private void HandleFail()
    {
        failPanel.SetActive(true);
        SoundManager.Instance.PlaySound("tryagain");

        retryButton.onClick.RemoveAllListeners();
        retryButton.onClick.AddListener(() =>
        {
            failPanel.SetActive(false);
            RestartActivityWithCheck();
        });
    }


    private void ResetValues()
    {
        scannedCountText.enabled = false;
        remainingTimeText.enabled = false;

        scannedCount = 0;
        challengeStarted = false;
        scannedCountText.text = "0 / 0";
        remainingTimeText.text = "0s";
        amountInputField.text = "";

        if (scanner != null) scanner.ClearUI();

        if (currentProduct != null)
        {
            ObjectPoolManager.Instance.ReturnToPool(PoolTag.Producto, currentProduct.name, currentProduct);
            currentProduct = null;
        }

        if (currentCustomer != null)
        {
            CustomerMovement movement = currentCustomer.GetComponent<CustomerMovement>();
            if (movement != null)
            {
                movement.MoveToExit();
            }
            currentCustomer = null;
        }
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

    private void RegisterAngryVelocityDialogs()
    {
        DialogSystem.Instance.customerComments.AddRange(new List<CustomerComment>
        {
            new CustomerComment
            {
                category = "angry_velocity",
                clientText = "Tengo prisa, ¿puedes atenderme más rápido?",
                question = "¿Cómo deberías responder?",
                options = new List<string>
                {
                    "Sí, lo haré en seguida",
                    "No, espera como todos",
                    "Estoy en lo mío",
                    "¿Y qué si no?"
                },
                correctAnswer = "Sí, lo haré en seguida"
            },
            new CustomerComment
            {
                category = "angry_velocity",
                clientText = "¿Siempre se tardan así? Esto es desesperante.",
                question = "¿Cómo deberías responder?",
                options = new List<string>
                {
                    "Haré lo posible por agilizar",
                    "¿Vienes a quejarte o a comprar?",
                    "No es mi problema",
                    "Entonces vete"
                },
                correctAnswer = "Haré lo posible por agilizar"
            },
            new CustomerComment
            {
                category = "angry_velocity",
                clientText = "¿Puedes darte prisa? No tengo todo el día.",
                question = "¿Cómo deberías responder?",
                options = new List<string>
                {
                    "Sí, enseguida",
                    "Si no te gusta, ve a otra caja",
                    "Aguanta como todos",
                    "Yo tampoco tengo todo el día"
                },
                correctAnswer = "Sí, enseguida"
            },
            new CustomerComment
            {
                category = "angry_velocity",
                clientText = "¡Qué lentitud! Esto debería ser más rápido.",
                question = "¿Cómo deberías responder?",
                options = new List<string>
                {
                    "Disculpa, voy a acelerar el proceso",
                    "No soy robot",
                    "A mí también me frustra",
                    "Eso no depende de mí"
                },
                correctAnswer = "Disculpa, voy a acelerar el proceso"
            }
        });
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
            scanner.RegisterProductScan(obj);

        RegisterProductScanned();
    }

    protected override void Initialize() { }
}
