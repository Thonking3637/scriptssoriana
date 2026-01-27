using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using Unity.VisualScripting;
using DG.Tweening;

public class AlertCashWithdrawal : ActivityBase
{
    [Header("Money UI")]
    public MoneySpawner moneySpawner;
    public Transform bagTarget;
    public Transform bagFinal;
    public GameObject alertPanel;
    public GameObject moneyPanel;
    public TextMeshProUGUI targetAmountText;
    public TextMeshProUGUI currentAmountText;
    public Button validateButton;
    public List<Button> enterButtons;
    public List<Button> numberButtons;
    public Vector2 panelStartPosition;
    public Vector2 panelEndPosition;
    public Button continueButton;
    private const int alertAmount = 4000;
    private int currentInputIndex = 0;

    [Header("Inputs por Denominación")]
    public GameObject denominationPanel;
    public List<TMP_InputField> denominationInputs;
    public List<float> denominationsOrdered;

    [Header("Validación")]
    public List<Button> enterValidationButtons;

    [Header("Resumen")]
    public TextMeshProUGUI totalEnteredText;
    private int totalEntered = 0;

    [Header("Ticket")]
    public GameObject ticketPrefab;
    public Transform ticketSpawnPoint;
    public Transform ticketTargetPoint;


    public TMP_InputField amountInputField;


    public override void StartActivity()
    {
        base.StartActivity();
        InitializeCommands();

        UpdateInstructionOnce(0, () =>
        {
            DOVirtual.DelayedCall(0.5f, () =>
            {
                ShowInitialAlert();
            });
        });
    }

    protected override void InitializeCommands()
    {
        base.InitializeCommands();
        foreach(var button in enterButtons) button.gameObject.SetActive(false);
        foreach (var button in numberButtons) button.gameObject.SetActive(false);
        foreach (var button in enterValidationButtons) button.gameObject.SetActive(false);

        commandManager.commandList.Add(new CommandManager.CommandAction
        {
            command = "ENTER_",
            customAction = ShowMoneyPanel,
            requiredActivity = "Day3_AlertaValores",
            commandButtons = enterButtons
        });
    }
    private void ShowInitialAlert()
    {
        alertPanel.SetActive(true);

        cameraController.MoveToPosition("Actividad 5 Inicio", () =>
        {           
            AnimateButtonsSequentiallyWithActivation(enterButtons, ShowMoneyPanel);             
        });
    }

    private void ShowMoneyPanel()
    {
        SoundManager.Instance.PlaySound("success");
        cameraController.MoveToPosition("Actividad 5 Mostrar Bolsa", () =>
        {
            bagTarget.gameObject.SetActive(true);
            UpdateInstructionOnce(1, () =>
            {
                cameraController.MoveToPosition("Actividad 5 Bandeja", () =>
                {
                    UpdateInstructionOnce(2, () =>
                    {
                        alertPanel.SetActive(false);
                        MoneyManager.OpenMoneyPanel(moneyPanel, panelStartPosition, panelEndPosition);

                        moneySpawner.SetPartialWithdrawalTexts(targetAmountText, currentAmountText, alertAmount);
                        moneySpawner.SetCustomDeliveryTarget(bagTarget, OnMoneyDelivered, alertAmount);

                        validateButton.onClick.RemoveAllListeners();
                        validateButton.onClick.AddListener(moneySpawner.ValidateAlertAmount);
                    });                  
                });                    
            });
        });
   
    }


    private void OnMoneyDelivered()
    {
        SoundManager.Instance.PlaySound("success");
        Dictionary<float, int> counts = moneySpawner.GetDenominationCounts();
        denominationPanel.SetActive(true);
        MoneyManager.CloseMoneyPanel(moneyPanel, panelStartPosition);

        cameraController.MoveToPosition("Actividad 5 Mostrar Inputs", () =>
        {
            UpdateInstructionOnce(3, () =>
            {
                StartInputSequence(counts);
            });
        });    
    }

    private void StartInputSequence(Dictionary<float, int> denominationCounts)
    {
        currentInputIndex = 0;
        ProceedToNextInput(denominationCounts);
    }

    private void ProceedToNextInput(Dictionary<float, int> counts)
    {
        if (currentInputIndex >= denominationsOrdered.Count)
        {
            SoundManager.Instance.PlaySound("success");
            OnAllValidationsComplete();
            return;
        }

        float denomination = denominationsOrdered[currentInputIndex];
        int quantity = counts.ContainsKey(denomination) ? counts[denomination] : 0;

        TMP_InputField currentInput = denominationInputs[currentInputIndex];
        amountInputField = currentInput;

        string amountString = quantity.ToString();
        List<Button> buttonsToPress = GetButtonsForAmount(amountString, numberButtons);

        foreach (var btn in numberButtons)
            btn.gameObject.SetActive(false);

        currentInput.text = "";
        currentInput.gameObject.SetActive(true);

        ActivateButtonWithSequence(buttonsToPress, 0, () =>
        {
            ActivateButtonWithSequence(enterValidationButtons, 0, () =>
            {
                if (!int.TryParse(currentInput.text, out int valueEntered))
                {
                    SoundManager.Instance.PlaySound("error");
                    return;
                }

                int denominationValue = (int)denomination;
                totalEntered += valueEntered * denominationValue;
                UpdateTotalEnteredUI();

                currentInputIndex++;
                ProceedToNextInput(counts);
            });
        });
    }


    public void OnNumberButtonPressed(string number)
    {
        if (amountInputField != null && amountInputField.gameObject.activeInHierarchy)
        {
            amountInputField.text += number;
        }
    }

    private void UpdateTotalEnteredUI()
    {
        if (totalEnteredText != null)
            totalEnteredText.text = $"TOTAL INGRESADO: ${totalEntered}";
    }


    private void OnAllValidationsComplete()
    {
        cameraController.MoveToPosition("Actividad 5 Vista General", () =>
        {
            UpdateInstructionOnce(4);
            InstantiateTicket(ticketPrefab, ticketSpawnPoint, ticketTargetPoint, HandleTicketDelivered);
        });
    }

    private void HandleTicketDelivered()
    {
        SoundManager.Instance.PlaySound("success");
        cameraController.MoveToPosition("Actividad 5 Mostrar Bolsa", () =>
        {
            UpdateInstructionOnce(5);
            bagTarget.gameObject.GetComponent<BoxCollider>().enabled = true;
            BagDelivery delivery = bagTarget.GetComponent<BagDelivery>();
            if (delivery == null) delivery = bagTarget.AddComponent<BagDelivery>();

            delivery.Initialize(bagFinal);
            delivery.OnBagDelivered += ActivityComplete;
        });

        
    }

    private void ActivityComplete()
    {
        cameraController.MoveToPosition("Actividad 5 Success", () =>
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
        moneySpawner.ResetMoney();
        moneySpawner.ResetMoneyUI();
    }
}
