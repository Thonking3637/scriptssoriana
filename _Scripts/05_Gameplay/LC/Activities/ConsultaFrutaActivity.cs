using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Actividad de Consulta de Frutas
/// CAMBIOS: Solo se agregó tracking (correctAnswers/wrongAnswers) y adapter
/// TODO lo demás está IGUAL al código original
/// </summary>
public class ConsultaFrutaActivity : ActivityBase
{
    public AudioClip InstructionsMusic;

    [Header("Tiempo en Actividad")]
    public TextMeshProUGUI liveTimerText;
    public TextMeshProUGUI successTimeText;

    [Header("Configuración de Frutas")]
    public List<GameObject> fruitPrefabs;
    public Transform spawnPoint;
    public Transform fruitPosition;
    public ProductScanner scanner;

    [Header("UI de la Actividad")]
    public TextMeshProUGUI activityProductsText;
    public TextMeshProUGUI activityTotalPriceText;
    public TextMeshProUGUI fruitInfoText;
    public GameObject fruitHUD;
    public GameObject nameInputPanel;
    public TMP_InputField nameInputField;
    public List<Button> nameInputButtons;
    public GameObject fruitSelectionPanel;
    public List<Button> fruitSelectionButtons;
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationText;

    [Header("Panel de Preguntas")]
    public GameObject questionPanel;
    public GameObject successPanel;
    public TextMeshProUGUI questionText;
    public List<Button> answerButtons;
    public Button continueButton;

    [Header("Comandos")]
    public List<Button> consultaFrutaButtons;
    public List<Button> enterButton;
    public List<Button> codeInputButtons;
    public List<Button> subTotalButton;

    [Header("Efecto de Parpadeo")]
    public Image screenFlash;
    public Color correctColor = new Color(0, 1, 0, 0.5f);
    public Color wrongColor = new Color(1, 0, 0, 0.5f);
    public float flashDuration = 0.5f;

    [Header("Configuración del Cliente")]
    public CustomerSpawner customerSpawner;
    private GameObject currentCustomer;

    // ✅ NUEVO: Tracking para el adapter
    [HideInInspector] public int correctAnswers = 0;
    [HideInInspector] public int wrongAnswers = 0;

    private GameObject currentFruit;
    private bool canSpawnFruit = true;
    private Fruit lastScannedFruit;
    private List<Fruit> usedFruits = new List<Fruit>();
    private int scanCount = 0;
    private float totalSum = 0f;

    protected override void Start()
    {
        DisableAllPanels();
    }

    private void DisableAllPanels()
    {
        fruitHUD.SetActive(false);
        nameInputPanel.SetActive(false);
        fruitSelectionPanel.SetActive(false);
        confirmationPanel.SetActive(false);
    }

    public override void StartActivity()
    {
        base.StartActivity();

        correctAnswers = 0;
        wrongAnswers = 0;

        RegisterCommands();

        currentCustomer = customerSpawner.SpawnCustomer();
        CustomerMovement customerMovement = currentCustomer.GetComponent<CustomerMovement>();

        UpdateInstructionOnce(0, () =>
        {
            customerMovement.MoveToCheckout(() =>
            {
                cameraController.MoveToPosition("Actividad Consulta Fruta Inicio", () =>
                {
                    canSpawnFruit = true;
                    ActivateConsultaFruta();
                });
            });
        });

        activityTimerText = liveTimerText;
        activityTimeText = successTimeText;
    }

    private void RegisterCommands()
    {
        CommandManager.CommandAction consultaFrutaCommand = new CommandManager.CommandAction
        {
            command = "CONSULTA_FRUTA",
            customAction = HandleConsultaFruta,
            requiredActivity = "Day1_ConsultaFruta",
            commandButtons = consultaFrutaButtons
        };
        commandManager.commandList.Add(consultaFrutaCommand);

        CommandManager.CommandAction enterCommand = new CommandManager.CommandAction
        {
            command = "ENTER",
            customAction = HandleEnterPressed,
            requiredActivity = "Day1_ConsultaFruta",
            commandButtons = enterButton
        };
        commandManager.commandList.Add(enterCommand);

        CommandManager.CommandAction subTotalCommand = new CommandManager.CommandAction
        {
            command = "SUB_TOTAL",
            customAction = HandleSubTotal,
            requiredActivity = "Day1_ConsultaFruta",
            commandButtons = subTotalButton
        };
        commandManager.commandList.Add(subTotalCommand);
    }

    private void ActivateConsultaFruta()
    {
        activityTotalPriceText.text = "";
        foreach (var button in consultaFrutaButtons)
        {
            button.gameObject.SetActive(true);
        }
        AnimateButtonsSequentially(consultaFrutaButtons);
    }

    private void HandleConsultaFruta()
    {
        if (!canSpawnFruit || cameraController.isMoving) return;

        SoundManager.Instance.PlaySound("success");

        canSpawnFruit = false;

        cameraController.MoveToPosition("Actividad Escaneo", () =>
        {
            UpdateInstructionOnce(1);
            SpawnNextFruit();
        });
    }

    public void HandleSubTotal()
    {
        foreach (var button in subTotalButton)
        {
            button.gameObject.SetActive(true);
        }

        SoundManager.Instance.PlaySound("success");
    }

    private void SpawnNextFruit()
    {
        if (currentFruit != null)
        {
            var oldDrag = currentFruit.GetComponent<DragFruit>() ?? currentFruit.GetComponentInChildren<DragFruit>();
            if (oldDrag != null) oldDrag.OnScanned -= RegisterFruitScanned;

            Destroy(currentFruit);
            currentFruit = null;
        }

        if (fruitPrefabs == null || fruitPrefabs.Count == 0) return;

        int fruitIndex = Mathf.Clamp(scanCount, 0, fruitPrefabs.Count - 1);
        GameObject prefab = fruitPrefabs[fruitIndex];
        if (prefab == null)
        {
            Debug.LogError($"❌ fruitPrefabs[{fruitIndex}] es NULL.");
            return;
        }

        if (spawnPoint == null)
        {
            Debug.LogError("❌ spawnPoint es NULL.");
            return;
        }

        currentFruit = Instantiate(prefab, spawnPoint.position, prefab.transform.rotation);

        Vector3 adjustedPosition = new Vector3(currentFruit.transform.position.x, 1.06f, currentFruit.transform.position.z);
        currentFruit.transform.position = adjustedPosition;

        DragFruit dragFruit = currentFruit.GetComponent<DragFruit>()
                           ?? currentFruit.GetComponentInChildren<DragFruit>();

        if (dragFruit != null)
        {
            dragFruit.OnScanned -= RegisterFruitScanned;
            dragFruit.OnScanned += RegisterFruitScanned;
        }
        else
        {
            Debug.LogError($"ERROR: El objeto instanciado {currentFruit.name} no tiene DragFruit.");
        }
    }

    public void RegisterFruitScanned(DragFruit scannedFruit)
    {
        if (scannedFruit == null) return;

        lastScannedFruit = scannedFruit.fruitData;

        if (lastScannedFruit != null && !usedFruits.Contains(lastScannedFruit))
            usedFruits.Add(lastScannedFruit);

        scannedFruit.transform.position = fruitPosition.position;
        fruitInfoText.text = $"{lastScannedFruit.weight}kg";

        if (scanCount == 3)
        {
            cameraController.MoveToPosition("Actividad Consulta Fruta Manzana", () =>
            {
                StartCodeInput();
                scannedFruit.gameObject.SetActive(false);
            });
        }
        else
        {
            nameInputPanel.SetActive(true);
            cameraController.MoveToPosition("Actividad Consulta Fruta Manzana", () =>
            {
                UpdateInstructionOnce(2);
                ShowNameInputPanel();
                scannedFruit.gameObject.SetActive(false);
            });
        }

        SoundManager.Instance.PlaySound("success");
    }

    private void ShowNameInputPanel()
    {
        nameInputField.text = "";
        commandManager.navigationManager.SetActiveInputField(nameInputField);

        AnimateButtonsSequentiallyWithActivation(nameInputButtons, ShowFruitSelectionPanel);
    }

    private void ShowFruitSelectionPanel()
    {
        UpdateInstructionOnce(3);

        SoundManager.Instance.PlaySound("success");

        cameraController.MoveToPosition("Actividad Consulta Fruta Botones");

        fruitSelectionPanel.SetActive(true);

        List<Fruit> selectionOptions = new List<Fruit> { lastScannedFruit };

        List<Fruit> otherFruits = new List<Fruit>(usedFruits);
        otherFruits.Remove(lastScannedFruit);
        ShuffleList(otherFruits);

        while (selectionOptions.Count < 4 && otherFruits.Count > 0)
        {
            selectionOptions.Add(otherFruits[0]);
            otherFruits.RemoveAt(0);
        }

        while (selectionOptions.Count < 4)
        {
            Fruit randomFruit;
            do
            {
                randomFruit = fruitPrefabs[Random.Range(0, fruitPrefabs.Count)].GetComponent<DragFruit>().fruitData;
            } while (randomFruit == lastScannedFruit || selectionOptions.Contains(randomFruit));

            selectionOptions.Add(randomFruit);
        }

        ShuffleList(selectionOptions);

        for (int i = 0; i < fruitSelectionButtons.Count; i++)
        {
            Button button = fruitSelectionButtons[i];

            button.gameObject.SetActive(true);
            button.interactable = true;

            Fruit fruit = selectionOptions[i];

            TextMeshProUGUI[] texts = button.GetComponentsInChildren<TextMeshProUGUI>();
            Image image = button.GetComponentInChildren<Image>();

            if (texts.Length >= 3 && image != null)
            {
                texts[0].text = fruit.fruitName;
                texts[1].text = $"Código: {fruit.code}";
                texts[2].text = $"Precio/kg: ${fruit.pricePerKilo}";
                image.sprite = fruit.image;
            }

            button.onClick.RemoveAllListeners();

            if (fruit == lastScannedFruit)
            {
                button.onClick.AddListener(HandleCorrectFruitSelection);
            }
            else
            {
                button.onClick.AddListener(HandleWrongFruitSelection);
            }
        }
    }

    private void HandleCorrectFruitSelection()
    {
        correctAnswers++;
        UpdateInstructionOnce(4);
        SoundManager.Instance.PlaySound("success");
        fruitSelectionPanel.SetActive(false);
        ShowConfirmationPanel();
    }

    private void ShowConfirmationPanel()
    {
        cameraController.MoveToPosition("Actividad Consulta Fruta Manzana");
        fruitHUD.SetActive(false);
        confirmationPanel.SetActive(true);
        confirmationText.text = $"{lastScannedFruit.fruitName}";

        foreach (var button in enterButton)
        {
            button.interactable = true;
        }

        AnimateButtonsSequentially(enterButton);
    }

    private void HandleEnterPressed()
    {
        nameInputField.text = "";

        SoundManager.Instance.PlaySound("bip");

        float resultado = lastScannedFruit.weight * lastScannedFruit.pricePerKilo;

        totalSum += resultado;

        activityProductsText.text += $"{lastScannedFruit.fruitName.ToUpper()} ({lastScannedFruit.code}) XT. KG\n" +
                              $"{lastScannedFruit.weight:0.00} KG * ${lastScannedFruit.pricePerKilo:0.00} " +
                              $"                                ${resultado:0.00}\n";

        activityTotalPriceText.text = $"${totalSum:0.00}";

        scanCount++;

        if (scanCount < 3)
        {
            UpdateInstructionOnce(5, RestartActivity, StartCompetition);
        }
        else if (scanCount == 3)
        {
            Invoke(nameof(RestartActivity), 0.9f);
        }
    }

    public void StartCompetition()
    {
        SoundManager.Instance.SetActivityMusic(activityMusicClip, 0.5f, false);
        HideInstructionsPanel();
        liveTimerText.GetComponent<TextMeshProUGUI>().enabled = true;
        StartActivityTimer();
    }

    private void AskFinalQuestion()
    {
        cameraController.MoveToPosition("Actividad Consulta Fruta Pregunta");

        if (usedFruits == null || usedFruits.Count == 0)
        {
            Debug.LogError("ERROR: No hay frutas escaneadas para generar la pregunta. Forzando spawn + esperando escaneo.");
            SpawnNextFruit();
            return;
        }

        SoundManager.Instance.SetActivityMusic(InstructionsMusic, 0.5f, false);
        questionPanel.SetActive(true);
        questionPanel.transform.localScale = Vector3.zero;
        questionPanel.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);

        Fruit questionFruit = usedFruits[Random.Range(0, usedFruits.Count)];
        questionText.text = $"¿Cuál es el código de la {questionFruit.fruitName}?";

        int correctAnswerIndex = Random.Range(0, answerButtons.Count);
        List<int> usedCodes = new List<int> { int.Parse(questionFruit.code) };

        for (int i = 0; i < answerButtons.Count; i++)
        {
            Button button = answerButtons[i];
            TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();

            button.onClick.RemoveAllListeners();

            if (i == correctAnswerIndex)
            {
                buttonText.text = questionFruit.code;
                button.onClick.AddListener(HandleCorrectAnswer);
            }
            else
            {
                int fakeCode;
                do
                {
                    fakeCode = Random.Range(1000, 9999);
                } while (usedCodes.Contains(fakeCode));

                usedCodes.Add(fakeCode);
                buttonText.text = fakeCode.ToString();
                button.onClick.AddListener(HandleWrongAnswer);
            }
        }
    }

    private void HandleCorrectAnswer()
    {
        correctAnswers++;

        CustomerMovement movement = currentCustomer.GetComponent<CustomerMovement>();
        movement?.MoveToExit();

        SoundManager.Instance.PlaySound("success");
        FlashScreen(correctColor);

        cameraController.MoveToPosition("Actividad Pregunta Correcta", () =>
        {
            StopActivityTimer();
            SoundManager.Instance.RestorePreviousMusic();
            successPanel.transform.localScale = Vector3.one;

            var adapter = GetComponent<ActivityMetricsAdapter>();
            if (adapter != null)
            {
                if (wrongAnswers == 0)
                {
                    adapter.customMessage = "¡AHORA SABES CÓMO BUSCAR FRUTAS Y VERDURAS";
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
                    adapter.customMessage = "Necesitas repasar un poco mas.";
                }

                Debug.Log($"[ConsultaFruta] Correctas: {correctAnswers}, Incorrectas: {wrongAnswers}");
                adapter.NotifyActivityCompleted();
            }
            else
            {
                Debug.LogError("[ConsultaFruta] NO se encontró ActivityMetricsAdapter");
                CompleteActivity();
            }
        });
    }

    private void HandleWrongAnswer()
    {
        wrongAnswers++;

        FlashScreen(wrongColor);
        SoundManager.Instance.PlaySound("error");
    }

    private void StartCodeInput()
    {
        ShowInstructionsPanel();
        UpdateInstructionOnce(6);

        if (fruitPrefabs.Count < 4)
        {
            Debug.LogError("ERROR: No hay suficientes frutas en la lista de prefabs para el cuarto intento.");
            return;
        }

        DragFruit dragFruit = fruitPrefabs[3].GetComponent<DragFruit>() ?? fruitPrefabs[3].GetComponentInChildren<DragFruit>();

        if (dragFruit != null && dragFruit.fruitData != null)
        {
            lastScannedFruit = dragFruit.fruitData;
            Debug.Log($"Última fruta asignada para ingreso de código: {lastScannedFruit.fruitName}, Código: {lastScannedFruit.code}");
        }
        else
        {
            Debug.LogError("ERROR: La cuarta fruta en la lista no tiene datos válidos.");
            return;
        }

        AnimateButtonsSequentiallyWithActivation(codeInputButtons, ShowProductInfoImmediately);
    }

    private void ShowProductInfoImmediately()
    {
        HideInstructionsPanel();
        fruitHUD.SetActive(false);

        if (lastScannedFruit == null)
        {
            Debug.LogError("ERROR: No se ha escaneado ninguna fruta. lastScannedFruit es null.");
            return;
        }

        Debug.Log($"Código correcto ingresado: {lastScannedFruit.code}");

        float resultado = lastScannedFruit.weight * lastScannedFruit.pricePerKilo;

        activityProductsText.text += $"{lastScannedFruit.fruitName.ToUpper()} ({lastScannedFruit.code}) XT. KG\n" +
                             $"{lastScannedFruit.weight:0.00} KG * ${lastScannedFruit.pricePerKilo:0.00} " +
                             $"                                ${resultado:0.00}\n";

        activityTotalPriceText.text = $"{resultado:0.00}";

        cameraController.MoveToPosition("Actividad Consulta Fruta Subtotal", () =>
        {
            AnimateButtonsSequentiallyWithActivation(subTotalButton, AskFinalQuestion);
        });
    }

    private void RestartActivity()
    {
        if (currentFruit != null)
        {
            var drag = currentFruit.GetComponent<DragFruit>();
            if (drag != null)
                drag.OnScanned -= RegisterFruitScanned;

            Destroy(currentFruit);
            currentFruit = null;
        }

        fruitHUD.SetActive(false);
        nameInputPanel.SetActive(false);
        fruitSelectionPanel.SetActive(false);
        confirmationPanel.SetActive(false);

        canSpawnFruit = true;

        if (scanCount < 3)
        {
            lastScannedFruit = null;
        }

        cameraController.MoveToPosition("Actividad Consulta Fruta Inicio", ActivateConsultaFruta);
    }

    private void HandleWrongFruitSelection()
    {
        wrongAnswers++;
        SoundManager.Instance.PlaySound("error");
    }

    private List<T> ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int randomIndex = Random.Range(0, list.Count);
            T temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
        return list;
    }

    private void FlashScreen(Color color)
    {
        if (screenFlash == null) return;

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

    protected override void Initialize() { }
}