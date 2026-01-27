using UnityEngine;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;
using System;
using UnityEngine.UI;

public class BalanceInquiryActivity: ActivityBase
{
    [Header("Tiempo en Actividad")]
    public TextMeshProUGUI liveTimerText;
    public TextMeshProUGUI successTimeText;

    [Header("Card Configuration")]
    public CardInteraction cardInteraction;

    [Header("UI Elements")]
    public GameObject panelDataClient;
    public TextMeshProUGUI clientInfoText;
    public List<Button> consultaPuntosButtons;

    [Header("Customer Configuration")]
    public CustomerSpawner customerSpawner;
    private GameObject currentCustomer;
    private Client currentClient;

    [Header("Panel Success")]
    public Button continueButton;

    private int currentAttempt = 0;
    private const int maxAttempts = 5;

    protected override void Start()
    {
        panelDataClient.gameObject.SetActive(false);
    }
    public override void StartActivity()
    {
        base.StartActivity();

        cardInteraction.OnCardMovedToFirstPosition -= HandleCardArrived;
        cardInteraction.OnCardMovedToSecondPosition -= HandleCardMovedToSecondPosition;
        cardInteraction.OnCardReturned -= HandleCardReturned;

        cardInteraction.OnCardMovedToFirstPosition += HandleCardArrived;
        cardInteraction.OnCardMovedToSecondPosition += HandleCardMovedToSecondPosition;
        cardInteraction.OnCardReturned += HandleCardReturned;

        activityTimerText = liveTimerText;
        activityTimeText = successTimeText;

        InitializeCommands();

        UpdateInstructionOnce(0, () =>
        {
            StartNewAttempt();
        });
    }

    protected override void InitializeCommands()
    {
        foreach (var button in consultaPuntosButtons) button.gameObject.SetActive(false);

        CommandManager.CommandAction consultaPuntosCommand = new CommandManager.CommandAction
        {
            command = "CONSULTA_PUNTOS",
            customAction = HandleConsultaPuntos,
            requiredActivity = "Day2_ConsultaPrecio",
            commandButtons = consultaPuntosButtons
        };
        commandManager.commandList.Add(consultaPuntosCommand);
    }

    private void StartNewAttempt()
    {
        if (currentAttempt >= maxAttempts)
        {
            ActivityComplete();
            return;
        }

        currentAttempt++;
        currentCustomer = customerSpawner.SpawnCustomer();
        currentClient = currentCustomer.GetComponent<Client>();

        panelDataClient.gameObject.SetActive(false);

        clientInfoText.text = ""; 

        CustomerMovement customerMovement = currentCustomer.GetComponent<CustomerMovement>();

            cameraController.MoveToPosition("Iniciando Juego", () =>         
            {
                customerMovement.MoveToCheckout(() =>
                {
                    cameraController.MoveToPosition("Actividad Consulta Press Button CP", () =>
                    {
                        UpdateInstructionOnce(1);
                        ActivateCommandButtons(consultaPuntosButtons);
                        ActivateButtonWithSequence(consultaPuntosButtons, 0);
                    });
                });
            });
    }

    public void HandleConsultaPuntos()
    {
        ShowCard();
    }

    private void ShowCard()
    {
        SoundManager.Instance.PlaySound("success");
        cameraController.MoveToPosition("Actividad Consulta Primera Posicion", () =>
        {
            UpdateInstructionOnce(2);
            cardInteraction.gameObject.SetActive(true);
        });
    }
    private void HandleCardArrived()
    {
        cameraController.MoveToPosition("Actividad Consulta Segunda Posicion");
        UpdateInstructionOnce(3);

        if (currentClient != null)
        {
            panelDataClient.gameObject.SetActive(true);
            clientInfoText.text = $"{currentClient.clientName}\n{currentClient.purchasePoints} puntos";
        }
        else
        {
            clientInfoText.text = "Error: Client not found";
        }        
    }

    private void HandleCardMovedToSecondPosition()
    {
        SoundManager.Instance.PlaySound("success");
        UpdateInstructionOnce(3, () =>
        {
            cameraController.MoveToPosition("Actividad Consulta Final Posicion", () =>
            {
                UpdateInstructionOnce(4, () =>
                {
                    DOVirtual.DelayedCall(1f, () =>
                    {
                        cameraController.MoveToPosition("Iniciando Juego", () =>
                        {
                            RestartActivity();
                            UpdateInstructionOnce(5, StartNewAttempt, StartCompetition);
                        });
                    });
                });
            });
        });
    }

    public void RestartActivity()
    {
        cardInteraction.ResetCard();
        CustomerMovement customerMovement = currentCustomer?.GetComponent<CustomerMovement>();
        if (customerMovement != null)
        {
            customerMovement.MoveToExit();
        }
        
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
    }

    public void ActivityComplete()
    {
        StopActivityTimer();
        commandManager.commandList.Clear();

        cameraController.MoveToPosition("Actividad Consulta Success", () =>
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
    public void StartCompetition()
    {
        SoundManager.Instance.SetActivityMusic(activityMusicClip, 0.2f, false);
        liveTimerText.GetComponent<TextMeshProUGUI>().enabled = true;
        StartActivityTimer();
    }

    protected override void Initialize()
    {
        throw new NotImplementedException();
    }
}
