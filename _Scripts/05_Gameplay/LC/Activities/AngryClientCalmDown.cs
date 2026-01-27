using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class AngryClientCalmDown : ActivityBase
{
    [Header("Client & Product")]
    public CustomerSpawner customerSpawner;
    public Transform productSpawnPoint;
    public ProductScanner scanner;

    [Header("UI")]
    public TextMeshProUGUI activityProductsText;
    public TextMeshProUGUI activityTotalPriceText;
    public TMP_InputField amountInputField;
    public List<Button> numberButtons;
    public List<Button> enterButtons;
    public List<Button> enterLastClicking;
    public List<Button> subtotalButtons;
    public List<Button> commandCardButtons;
    public GameObject ticketPrefab;
    public Transform ticketSpawnPoint;
    public Transform ticketTargetPoint;
    public GameObject failPanel;
    public Button retryButton;
    public Button continueButton;
    public Slider emotionSlider;
    public Gradient emotionColorGradient;
    public CanvasGroup emotionContainer;
    public RectTransform emotionPanelTransform;
    public Vector2 hiddenPosition;
    public Vector2 visiblePosition;

    private GameObject currentCustomer;
    private Client currentClient;
    private GameObject currentProduct;
    private int scannedCount = 0;
    private int currentEmotionLevel = 0;
    private int currentQuestionIndex = 0;

    public override void StartActivity()
    {
        base.StartActivity();

        RegisterCalmDownDialogs();

        if (scanner != null)
        {
            scanner.UnbindUI(this);
            scanner.ClearUI();
            scanner.BindUI(this, activityProductsText, activityTotalPriceText, true);
        }

        productNames = ObjectPoolManager.Instance.GetAvailablePrefabNames(PoolTag.Producto).ToArray();

        InitializeCommands();

        currentQuestionIndex = 0;
        emotionContainer.alpha = 0;
        emotionContainer.interactable = false;
        emotionContainer.blocksRaycasts = false;
        emotionPanelTransform.anchoredPosition = hiddenPosition;
        emotionSlider.value = 0;

        UpdateInstructionOnce(0, () =>
        {
            DOVirtual.DelayedCall(0.5f, () =>
            {
                SpawnCustomer();
            });
        });
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
            requiredActivity = "Day4_ClienteMolestoCalmado",
            commandButtons = subtotalButtons
        });

        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "T+B+ENTER_",
            customAction = HandleEnterAmount,
            requiredActivity = "Day4_ClienteMolestoCalmado",
            commandButtons = commandCardButtons
        });
    }
    private void SpawnCustomer()
    {
        currentCustomer = customerSpawner.SpawnCustomer();
        currentClient = currentCustomer.GetComponent<Client>();

        cameraController.MoveToPosition("Iniciando Juego", () =>
        {
            currentCustomer.GetComponent<CustomerMovement>().MoveToCheckout(() =>
            {
                cameraController.MoveToPosition("Actividad 1 Cliente Camera", () =>
                {
                    emotionContainer.alpha = 1;
                    UpdateEmotionUI(0);
                    UpdateInstructionOnce(1, () =>
                    {
                        UpdateInstructionOnce(2, () =>
                        {
                            DOVirtual.DelayedCall(0.5f, () =>
                            {
                                AskCurrentQuestion();
                            });
                        });
                    });
                });
            });
        });
    }

    private void AskCurrentQuestion(System.Action onComplete = null)
    {
        var comments = DialogSystem.Instance.customerComments.FindAll(c => c.category == "calm_down");

        if (currentQuestionIndex >= comments.Count || scannedCount >= 4)
        {
            onComplete?.Invoke();
            return;
        }

        var entry = comments[currentQuestionIndex];

        cameraController.MoveToPosition("Actividad 1 Cliente Camera", () =>
        {
            DialogSystem.Instance.ShowClientDialog(currentClient, entry.clientText, () =>
            {
                AnimateEmotionPanel(true);
                emotionPanelTransform.DOAnchorPos(visiblePosition, 0.35f).SetEase(Ease.InOutQuad);
                DialogSystem.Instance.ShowClientDialogWithOptions(
                    entry.question,
                    entry.options,
                    entry.correctAnswer,
                    () => OnCorrectAnswer(onComplete),
                    OnWrongAnswer
                );
            });
        });
    }
    private void AnimateEmotionPanel(bool visible)
    {
        if (emotionContainer == null || emotionPanelTransform == null) return;
        ShowEmotionContainer(visible);
    }

    private void OnCorrectAnswer(System.Action onComplete = null)
    {
        currentEmotionLevel = Mathf.Min(currentEmotionLevel + 1, 5);
        emotionPanelTransform.DOAnchorPos(hiddenPosition, 0.35f).SetEase(Ease.InOutQuad);
        AnimateEmotionPanel(true);
        UpdateEmotionUI(currentEmotionLevel);
        currentQuestionIndex++;

        if (currentQuestionIndex <= 4 && scannedCount < 4)
        {
            cameraController.MoveToPosition("Iniciando Juego", () =>
            {
                UpdateInstructionOnce(3, () =>
                {
                    SpawnNextProduct();
                    onComplete?.Invoke();
                });
            });
            return;
        }

        onComplete?.Invoke();
    }


    private void OnWrongAnswer()
    {
        currentEmotionLevel = Mathf.Max(0, currentEmotionLevel - 1);
        print(currentEmotionLevel);
        UpdateEmotionUI(currentEmotionLevel);
    }

    private void SpawnNextProduct()
    {
        if (scannedCount < 4)
        {
            GameObject product = GetPooledProduct(scannedCount % productNames.Length, productSpawnPoint);
            if (product != null)
            {
                product.SetActive(true);
                currentProduct = product;

                BindCurrentProduct();
            }
        }
    }

    public void RegisterProductScanned()
    {
        if (currentProduct != null)
        {
            var drag = currentProduct.GetComponent<DragObject>();
            string poolName =
                (drag != null && !string.IsNullOrEmpty(drag.OriginalPoolName))
                    ? drag.OriginalPoolName
                    : currentProduct.name;

            ObjectPoolManager.Instance.ReturnToPool(PoolTag.Producto, poolName, currentProduct);
            currentProduct = null;
        }

        scannedCount++;

        if (scannedCount < 4)
        {
            cameraController.MoveToPosition("Actividad 1 Cliente Camera", () =>
            {
                AskCurrentQuestion();
            });
        }
        else
        {
            cameraController.MoveToPosition("Actividad 2 Subtotal", () =>
            {
                UpdateInstructionOnce(4, () =>
                {
                    ActivateCommandButtons(subtotalButtons);
                    AnimateButtonsSequentiallyWithActivation(subtotalButtons);
                });
            });
        }
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
                dialog: "Disculpe, ¿Cuál sera su metodo de pago?",
                onComplete: () =>
                {
                    DialogSystem.Instance.ShowClientDialog(
                        currentClient,
                        dialog: "Con tarjeta, apurese",
                        onComplete: () =>
                        {
                            DialogSystem.Instance.HideDialog(false);
                            cameraController.MoveToPosition("Actividad 2 Subtotal", () =>
                            {
                                UpdateInstructionOnce(5);
                                ActivateCommandButtons(commandCardButtons);
                                ActivateButtonWithSequence(commandCardButtons, 0, HandleEnterAmount);
                            });
                        });
                });
        });
    }

    public void HandleEnterAmount()
    {
        AskCurrentQuestion(() =>
        {
            cameraController.MoveToPosition("Actividad 2 Subtotal", () =>
            {
                float totalAmount = GetTotalAmount(activityTotalPriceText);
                ActivateAmountInput(totalAmount);
            });
        });
    }

    private void ActivateAmountInput(float amount)
    {
        SoundManager.Instance.PlaySound("success");

        cameraController.MoveToPosition("Actividad 2 Escribir Monto");

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
        UpdateInstructionOnce(6);
        ActivateButtonWithSequence(selectedButtons, 0, () =>
        {
            ActivateButtonWithSequence(enterLastClicking, 0, () =>
            {
                AskCurrentQuestion();
                cameraController.MoveToPosition("Actividad 1 Mirar Cliente", () =>
                {
                    CustomerMovement customerMovement = currentCustomer.GetComponent<CustomerMovement>();
                    if (customerMovement != null)
                    {
                        customerMovement.MoveToPinEntry(() =>
                        {
                            UpdateInstructionOnce(7, () =>
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
        AnimateEmotionPanel(false);

        if (currentEmotionLevel >= 4)
        {
            ActivityComplete();
        }
        else
        {
            ShowFailPanel();
        }
    }

    private void ActivityComplete()
    {
        scanner.ClearUI();
        commandManager.commandList.Clear();
        cameraController.MoveToPosition("Actividad 2 Success", () =>
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

    private void ShowFailPanel()
    {
        AnimateEmotionPanel(false);
        failPanel.SetActive(true);
        retryButton.onClick.RemoveAllListeners();
        retryButton.onClick.AddListener(() => {
            cameraController.MoveToPosition("Iniciando Juego");
            failPanel.SetActive(false);
            RestartActivity();
        });
    }

    private void RestartActivity()
    {
        ResetValues();
        DialogSystem.Instance.customerComments.RemoveAll(c => c.category == "calm_down");
        DialogSystem.Instance.ResetCategoryIndex("calm_down");
        StartActivity();
    }

    private void ResetValues()
    {
        scannedCount = 0;
        currentEmotionLevel = 0;
        currentQuestionIndex = 0;
        UpdateEmotionUI(0);
        scanner.ClearUI();
    }

    private void UpdateEmotionUI(int level)
    {
        if (emotionSlider != null)
        {
            emotionSlider.value = level;
            emotionSlider.fillRect.GetComponent<Image>().color = emotionColorGradient.Evaluate(level / 4f);
        }
    }
    public void OnNumberButtonPressed(string number)
    {
        if (amountInputField != null)
        {
            amountInputField.text += number;
        }
    }

    private void ShowEmotionContainer(bool visible)
    {
        if (emotionContainer == null) return;
        StopAllCoroutines();
        StartCoroutine(FadeEmotionContainer(visible));
    }

    private IEnumerator FadeEmotionContainer(bool visible)
    {
        float targetAlpha = visible ? 1f : 0f;
        float duration = 0.25f;
        float startAlpha = emotionContainer.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            emotionContainer.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        emotionContainer.alpha = targetAlpha;
        emotionContainer.interactable = visible;
        emotionContainer.blocksRaycasts = visible;
    }

    private void RegisterCalmDownDialogs()
    {
        DialogSystem.Instance.customerComments.RemoveAll(c => c.category == "calm_down");

        DialogSystem.Instance.customerComments.AddRange(new List<CustomerComment>
        {
            new CustomerComment
            {
                category = "calm_down",
                clientText = "Hoy todo me ha salido mal, ojalá al menos tú no me falles.",
                question = "¿Cómo deberías responder?",
                options = new List<string>
                {
                    "Haré todo lo posible para atenderle bien",
                    "No es mi problema",
                    "Todos tenemos días malos",
                    "¿Y eso qué tiene que ver conmigo?"
                },
                correctAnswer = "Haré todo lo posible para atenderle bien"
            },
            new CustomerComment
            {
                category = "calm_down",
                clientText = "Oye, ¿por qué no abren más cajas? Están súper lentos.",
                question = "¿Cómo deberías responder?",
                options = new List<string>
                {
                    "No se preocupe, vamos a hacer esto muy rápido.",
                    "Entonces no venga",
                    "Eso no es culpa mía",
                    "A mí tampoco me gusta estar aquí"
                },
                correctAnswer = "No se preocupe, vamos a hacer esto muy rápido."
            },
            new CustomerComment
            {
                category = "calm_down",
                clientText = "¿Por qué siempre me toca el peor día?",
                question = "¿Cómo deberías responder?",
                options = new List<string>
                {
                    "Lamento que esté teniendo un mal día, haré mi parte para ayudar",
                    "Eso no tiene nada que ver conmigo",
                    "Porque así es la vida",
                    "Yo también estoy harto"
                },
                correctAnswer = "Lamento que esté teniendo un mal día, haré mi parte para ayudar"
            },
            new CustomerComment
            {
                category = "calm_down",
                clientText = "Espero que al menos tú hagas bien tu trabajo.",
                question = "¿Cómo deberías responder?",
                options = new List<string>
                {
                    "Por supuesto, haré lo mejor posible",
                    "¿Insinúa que no lo hago?",
                    "Veremos si puedo",
                    "Eso depende de usted"
                },
                correctAnswer = "Por supuesto, haré lo mejor posible"
            },
            new CustomerComment
            {
                category = "calm_down",
                clientText = "Ya estoy cansado de todo esto, apúrate.",
                question = "¿Cómo deberías responder?",
                options = new List<string>
                {
                    "Entiendo, iré lo más rápido que pueda",
                    "Si está cansado, no es mi culpa",
                    "No me presione",
                    "Entonces váyase"
                },
                correctAnswer = "Entiendo, iré lo más rápido que pueda"
            }
        });
    }

    private void BindCurrentProduct()
    {
        if (currentProduct == null) return;

        var drag = currentProduct.GetComponent<DragObject>();
        if (drag == null) return;

        drag.OnScanned -= OnProductScanned; // anti x2 por pool
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