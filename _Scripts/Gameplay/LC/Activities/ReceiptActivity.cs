using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;

public class ReceiptActivity : ActivityBase
{
    [Header("Configuración del Cliente")]
    public CustomerSpawner customerSpawner;
    private GameObject currentCustomer;
    private Client currentClient;

    [Header("Configuración del Recibo")]
    public GameObject receiptPrefab;
    public Transform receiptSpawnPoint;
    public Transform scannerPoint;
    public Transform initialReturnPoint;

    [Header("División del Recibo")]
    public GameObject splitReceiptBigPrefab;
    public GameObject splitReceiptSmallPrefab;
    public Transform clientTarget;
    public Transform registerTarget;

    [Header("Posiciones de Instanciación de Partes del Recibo")]
    public Transform bigPartSpawnPoint;
    public Transform smallPartSpawnPoint;

    [Header("UI del Recibo")]
    public GameObject receiptPanelUI;
    public TextMeshProUGUI receiptInfoText;

    [Header("Panel Success")]
    public Button continueButton;

    [Header("Tiempo en Actividad")]
    public TextMeshProUGUI liveTimerText;
    public TextMeshProUGUI successTimeText;

    private int currentAttempt = 0;
    private const int maxAttempts = 3;
    private int deliveredParts = 0;

    public override void StartActivity()
    {
        base.StartActivity();

        UpdateInstructionOnce(0, () =>
        {
            StartNewAttempt();
        });

        activityTimerText = liveTimerText;
        activityTimeText = successTimeText;
    }

    private void StartNewAttempt()
    {
        deliveredParts = 0;

        currentCustomer = customerSpawner.SpawnCustomer();
        currentClient = currentCustomer.GetComponent<Client>();

        int randomNumber = Random.Range(100, 1000);
        currentClient.address = $"Calle Mexico {randomNumber}";
        currentClient.paymentAmount = Random.Range(300, 1001);
        currentClient.receiptType = "SERVICIO DE LUZ";

        cameraController.MoveToPosition("Iniciando Juego", () =>
        {
            CustomerMovement movement = currentCustomer.GetComponent<CustomerMovement>();
            movement.MoveToCheckout(() =>
            {
                cameraController.MoveToPosition("Actividad 1 Mostrar Recibo", () =>
                {
                    UpdateInstructionOnce(1);
                    SpawnReceipt();
                });
                
            });
        });
    }

    private void SpawnReceipt()
    {
        GameObject receiptGO = Instantiate(receiptPrefab, receiptSpawnPoint.position, receiptPrefab.transform.rotation);

        ReceiptBehavior receipt = receiptGO.GetComponent<ReceiptBehavior>();
        receipt.Initialize(
            currentClient,
            scannerPoint,
            initialReturnPoint,
            receiptPanelUI,
            receiptInfoText,
            splitReceiptBigPrefab,
            splitReceiptSmallPrefab,
            clientTarget,
            registerTarget,
            bigPartSpawnPoint,
            smallPartSpawnPoint
        );

        SplitReceiptPart.OnReceiptPartDelivered += HandlePartDelivered;

        receipt.onReturnToStartComplete = () =>
        {
            SoundManager.Instance.PlaySound("success");
            cameraController.MoveToPosition("Actividad 1 Mostrar Pantalla", () =>
            {
                UpdateInstructionOnce(2, () =>
                {
                    DOVirtual.DelayedCall(1, () =>
                    {
                        cameraController.MoveToPosition("Actividad 1 Mostrar Recibo Partido", () =>
                        {
                            UpdateInstructionOnce(3);
                        });
                    });
                });
            });
        };

        receipt.onSplitReceipt = () =>
        {
            SoundManager.Instance.PlaySound("success");
            UpdateInstructionOnce(4);
        };
    }

    private void HandlePartDelivered()
    {
        deliveredParts++;

        if (deliveredParts >= 2)
        {
            SplitReceiptPart.OnReceiptPartDelivered -= HandlePartDelivered;

            if (receiptPanelUI != null) receiptPanelUI.SetActive(false);
            currentAttempt++;
            CustomerMovement movement = currentCustomer.GetComponent<CustomerMovement>();
            movement.MoveToExit();

            if (currentAttempt < maxAttempts)
            {
                UpdateInstructionOnce(5, StartNewAttempt, StartCompetition);
            }
            else
            {
                CompleteSequence();
            }
        }

    }

    private void CompleteSequence()
    {
        StopActivityTimer();

        cameraController.MoveToPosition("Actividad 1 Sucessfull", () =>
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

    protected override void OnDisable()
    {
        base.OnDisable();
        SplitReceiptPart.OnReceiptPartDelivered -= HandlePartDelivered;
    }

    private void OnDestroy()
    {
        SplitReceiptPart.OnReceiptPartDelivered -= HandlePartDelivered;
    }


    protected override void Initialize() { }
}
