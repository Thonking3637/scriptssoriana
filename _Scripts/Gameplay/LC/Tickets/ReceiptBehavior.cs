using UnityEngine;
using TMPro;
using System.Collections;
using DG.Tweening;
using UnityEngine.EventSystems;

public class ReceiptBehavior : MonoBehaviour, IPointerClickHandler
{
    private Client clientData;
    private Transform scannerPosition;
    private Transform initialPosition;

    private bool scannedOnce = false;
    private bool alreadyScanned = false;

    private GameObject receiptPanelUI;
    private TextMeshProUGUI infoText;

    private GameObject splitReceiptBig;
    private GameObject splitReceiptSmall;

    private bool isShowing = false;

    private Transform clientTarget;
    private Transform registerTarget;

    private Transform bigSpawnPoint;
    private Transform smallSpawnPoint;

    public System.Action onReturnToStartComplete;
    public System.Action onSplitReceipt;

    public void Initialize(
        Client client,
        Transform scannerPos,
        Transform startPos,
        GameObject panelUI,
        TextMeshProUGUI textUI,
        GameObject bigPrefab,
        GameObject smallPrefab,
        Transform clientDest,
        Transform registerDest,
        Transform bigSpawn,
        Transform smallSpawn)
    {
        clientData = client;
        scannerPosition = scannerPos;
        initialPosition = startPos;
        receiptPanelUI = panelUI;
        infoText = textUI;
        splitReceiptBig = bigPrefab;
        splitReceiptSmall = smallPrefab;
        clientTarget = clientDest;
        registerTarget = registerDest;
        bigSpawnPoint = bigSpawn;
        smallSpawnPoint = smallSpawn;
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        if (isShowing) return;

        if (!scannedOnce)
        {
            scannedOnce = true;
            StartCoroutine(ShowReceiptInScanner());
        }
        else if (!alreadyScanned)
        {
            alreadyScanned = true;
            SplitReceipt();
        }
    }

    private IEnumerator ShowReceiptInScanner()
    {
        isShowing = true;

        transform.DOMove(scannerPosition.position, 0.75f).SetEase(Ease.InOutSine);
        yield return new WaitForSeconds(0.8f);

        string info =
            $"{clientData.receiptType}\n" +
            $"NOMBRE CLIENTE: {clientData.clientName}\n" +
            $"DIRECCIÓN: {clientData.address}\n" +
            $"MONTO A PAGAR: ${clientData.paymentAmount}";

        if (receiptPanelUI != null) receiptPanelUI.SetActive(true);
        if (infoText != null) infoText.text = info;

        yield return new WaitForSeconds(0.5f);

        transform.DOMove(initialPosition.position, 0.75f).SetEase(Ease.InOutSine);
        yield return new WaitForSeconds(0.8f);

        onReturnToStartComplete?.Invoke();
        isShowing = false;
    }

    private void SplitReceipt()
    {
        GameObject bigPart = Instantiate(splitReceiptBig, bigSpawnPoint.position, splitReceiptBig.transform.rotation);
        GameObject smallPart = Instantiate(splitReceiptSmall, smallSpawnPoint.position, splitReceiptSmall.transform.rotation);

        SplitReceiptPart bigScript = bigPart.GetComponent<SplitReceiptPart>();
        SplitReceiptPart smallScript = smallPart.GetComponent<SplitReceiptPart>();

        bigScript.destination = SplitReceiptPart.Destination.Client;
        bigScript.clientTarget = clientTarget;

        smallScript.destination = SplitReceiptPart.Destination.Register;
        smallScript.registerTarget = registerTarget;

        Destroy(gameObject);
        onSplitReceipt?.Invoke();
    }
}
