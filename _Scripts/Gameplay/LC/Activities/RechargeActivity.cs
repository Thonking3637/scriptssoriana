using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UI;

public class RechargeActivity: ActivityBase
{
    [Header("UI General")]
    public TextMeshProUGUI liveTimerText;
    public TextMeshProUGUI successTimeText;

    [Header("Configuración del Cliente")]
    public CustomerSpawner customerSpawner;
    private GameObject currentCustomer;
    private Client currentClient;

    [Header("Paneles de Input")]
    public GameObject phoneInputPanel;
    public TMP_InputField phoneInputField1;
    public TMP_InputField phoneInputField2;

    [Header("Botones para Números")]
    public List<Button> numberButtons;
    public List<Button> enterButtons;

    [Header("Panel de Compañías")]
    public GameObject companyPanel;
    public List<Button> companyButtons;

    [Header("Panel de Montos")]
    public GameObject amountPanel;
    public List<Button> amountButtons;

    [Header("Botones de Comando")]
    public List<Button> recargaCommandButtons;

    [Header("Panel Final")]
    public Button continueButton;

    private int currentAttempt = 0;
    private const int maxAttempts = 3;
    public override void StartActivity()
    {
        base.StartActivity();

        if (customerSpawner == null)
        {
            Debug.LogError("CustomerSpawner no está asignado.");
            return;
        }

        phoneInputPanel.SetActive(false);
        companyPanel.SetActive(false);
        amountPanel.SetActive(false);

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
        foreach (var button in recargaCommandButtons)
            button.gameObject.SetActive(false);

        CommandManager.CommandAction recargaCommand = new CommandManager.CommandAction
        {
            command = "RECARGA_TELEFONICA",
            customAction = StartPhoneInput,
            requiredActivity = "Day3_Recarga",
            commandButtons = recargaCommandButtons
        };

        commandManager.commandList.Add(recargaCommand);
    }

    private void StartNewAttempt()
    {
        currentCustomer = customerSpawner.SpawnCustomer();
        currentClient = currentCustomer.GetComponent<Client>();
        currentClient.GenerateRechargeData();

        cameraController.MoveToPosition("Iniciando Juego", () =>
        {
            cameraController.MoveToPosition("Cliente Camera", () =>
            {
                DialogSystem.Instance.ShowClientDialog(
                    client: currentClient,
                    dialog: $"Quisiera hacer una recarga telefónica al número <color=#FFA500>{currentClient.phoneNumber}</color> de <color=#FFA500>{currentClient.phoneCompany}</color> con el monto de <color=#FFA500>${currentClient.rechargeAmount}</color>.",
                    onComplete: () =>
                    {
                        cameraController.MoveToPosition("Actividad 2 Mostrar Recarga", () =>
                        {
                            UpdateInstructionOnce(1);
                            ActivateButtonWithSequence(recargaCommandButtons, 0);
                        });
                    }
                );

            });
        });
    }

    private void StartPhoneInput()
    {
        SoundManager.Instance.PlaySound("success");
        phoneInputPanel.SetActive(true);
        cameraController.MoveToPosition("Actividad 2 Mostrar Numero", () =>
        {
            UpdateInstructionOnce(2);
            phoneInputPanel.SetActive(true);
            phoneInputField1.text = "";
            phoneInputField2.text = "";
            phoneInputField1.ActivateInputField();
            commandManager.navigationManager.SetActiveInputField(phoneInputField1);
            GenerateNumberButtons(currentClient.phoneNumber, HandleFirstNumberEntered);
        });
    }

    private void HandleFirstNumberEntered()
    {   
        UpdateInstructionOnce(2);
        commandManager.navigationManager.SetActiveInputField(phoneInputField2);
        phoneInputField2.text = "";
        phoneInputField2.DeactivateInputField();
        phoneInputField2.ActivateInputField();
        GenerateNumberButtons(currentClient.phoneNumber, HandleSecondNumberEntered);
    }


    private void HandleSecondNumberEntered()
    {
        companyPanel.SetActive(true);
        if (phoneInputField2.text == currentClient.phoneNumber)
        {
            phoneInputPanel.SetActive(false);
            ShowCompanyButtons();
        }
        else
        {
            SoundManager.Instance.PlaySound("error");
            phoneInputField1.text = "";
            phoneInputField2.text = "";
            StartPhoneInput();
        }
    }

    private void ShowCompanyButtons()
    {
        cameraController.MoveToPosition("Actividad 2 Mostrar Company Buttons", () =>
        {
            UpdateInstructionOnce(3);
            companyPanel.SetActive(true);

            foreach (var button in companyButtons)
            {
                button.gameObject.SetActive(true);
                button.onClick.RemoveAllListeners();

                string company = button.GetComponentInChildren<TextMeshProUGUI>().text;

                if (company == currentClient.phoneCompany)
                {
                    button.onClick.AddListener(() => HandleCorrectCompany());
                }
                else
                {
                    button.onClick.AddListener(() => HandleIncorrectSelection());
                }
            }
        });
    }

    private void HandleCorrectCompany()
    {
        amountPanel.SetActive(true);
        SoundManager.Instance.PlaySound("success");
        companyPanel.SetActive(false);
        ShowAmountButtons();
    }

    private void HandleIncorrectSelection()
    {
        SoundManager.Instance.PlaySound("error");
    }

    private void ShowAmountButtons()
    {
        cameraController.MoveToPosition("Actividad 2 Mostrar Company Buttons", () =>
        {
            UpdateInstructionOnce(4);
            amountPanel.SetActive(true);

            foreach (var button in amountButtons)
            {
                button.gameObject.SetActive(true);
                button.onClick.RemoveAllListeners();

                int amount = int.Parse(button.GetComponentInChildren<TextMeshProUGUI>().text);

                if (amount == currentClient.rechargeAmount)
                {
                    button.onClick.AddListener(() => HandleCorrectAmount());
                }
                else
                {
                    button.onClick.AddListener(() => HandleIncorrectSelection());
                }
            }
        });
    }

    private void HandleCorrectAmount()
    {
        SoundManager.Instance.PlaySound("success");
        amountPanel.SetActive(false);
        FinalizeAttempt();
    }

    private void FinalizeAttempt()
    {
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
        UpdateInstructionOnce(5, StartNewAttempt, StartCompetition);
    }

    public void StartCompetition()
    {
        SoundManager.Instance.SetActivityMusic(activityMusicClip, 0.2f, false);
        liveTimerText.GetComponent<TextMeshProUGUI>().enabled = true;
        StartActivityTimer();
    }

    public void ActivityComplete()
    {
        StopActivityTimer();
        commandManager.commandList.Clear();
        DialogSystem.Instance.HideDialog();
        cameraController.MoveToPosition("Actividad Recharge Success", () =>
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

    private void GenerateNumberButtons(string target, System.Action onComplete)
    {
        List<Button> selectedButtons = GetButtonsForAmount(target, numberButtons);

        foreach (var button in numberButtons)
        {
            button.gameObject.SetActive(false);
        }

        ActivateButtonWithSequence(selectedButtons, 0, () =>
        {
            AnimateButtonsSequentiallyWithActivation(enterButtons, () =>
            {
                SoundManager.Instance.PlaySound("success");
                onComplete.Invoke();
            });
        });
    }

    protected override void Initialize() { }
}
