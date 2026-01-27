using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using System;

public class MoneySpawner: MonoBehaviour
{
    [Header("Money Configuration")]
    public List<GameObject> moneyPrefabs;
    public Transform spawnPoint;
    public Transform billStackPoint;
    public Transform coinStackPoint;
    public Transform customerMoneySpawnPoint;
    public Transform customerPosition;
    public float moveDuration = 0.5f;

    [Header("UI Elements")]
    public TextMeshProUGUI changeDueText;
    public TextMeshProUGUI givenAmountText;
    public TextMeshProUGUI totalPurchaseText;

    private float changeDue = 0f;

    public List<GameObject> spawnedMoney = new List<GameObject>();
    private int billStackCount = 0;
    private int coinStackCount = 0;
    private int firstCoinIndex = 5;
    private float givenAmount = 0f;

    // NUEVO PARA ACTIVIDAD PERSONALIZADA
    private TextMeshProUGUI targetAmountText;
    private TextMeshProUGUI currentAmountText;
    private int targetAmount = 0;
    private Action onMoneyDelivered;
    private Transform deliveryTarget;
    private bool deliverToCustomTarget = false;
    private bool useCustomDeliveryFlow = false;
    private int requiredCustomAmount = 0;

    private Dictionary<float, int> denominationCounts = new();

    public void SpawnMoney(int index)
    {
        if (index < 0 || index >= moneyPrefabs.Count) return;

        GameObject money = Instantiate(moneyPrefabs[index], spawnPoint.position, moneyPrefabs[index].transform.rotation);
        spawnedMoney.Add(money);

        bool isCoin = index >= firstCoinIndex;
        Transform targetStackPoint = isCoin ? coinStackPoint : billStackPoint;

        if (isCoin)
        {
            Vector3 coinPosition = targetStackPoint.position + new Vector3(0, coinStackCount * 0.005f, 0);
            coinStackCount++;
            money.transform.DOMove(coinPosition, moveDuration).SetEase(Ease.OutQuad);
        }
        else
        {
            Vector3 billPosition = targetStackPoint.position + new Vector3(
                UnityEngine.Random.Range(-0.05f, 0.05f),
                billStackCount * 0.002f,
                UnityEngine.Random.Range(-0.05f, 0.05f)
            );
            billStackCount++;
            money.transform.DOMove(billPosition, moveDuration).SetEase(Ease.OutQuad);
        }

        float value = GetMoneyValue(index);
        if (!denominationCounts.ContainsKey(value))
            denominationCounts[value] = 0;

        denominationCounts[value]++;

        givenAmount += GetMoneyValue(index);
        UpdateGivenAmountUI();
        UpdatePartialWithdrawalUI();

    }

    public Dictionary<float, int> GetDenominationCounts()
    {
        return new Dictionary<float, int>(denominationCounts);
    }

    private float GetMoneyValue(int index)
    {
        float[] moneyValues = { 500f, 200f, 100f, 50f, 20f, 10f, 5f, 2f, 1f};
        return (index >= 0 && index < moneyValues.Length) ? moneyValues[index] : 0f;
    }

    private void UpdateGivenAmountUI()
    {
        if (givenAmountText != null)
        {
            givenAmountText.text = $"${givenAmount:0.00}";
        }
    }

    public void RemoveLastMoney()
    {
        if (spawnedMoney.Count > 0)
        {
            GameObject lastMoney = spawnedMoney[spawnedMoney.Count - 1];
            spawnedMoney.RemoveAt(spawnedMoney.Count - 1);

            lastMoney.transform.DOMove(spawnPoint.position, moveDuration).SetEase(Ease.InQuad)
                .OnComplete(() => Destroy(lastMoney));

            float moneyValue = GetMoneyValueByObject(lastMoney);

            if (denominationCounts.ContainsKey(moneyValue))
            {
                denominationCounts[moneyValue]--;

                if (denominationCounts[moneyValue] <= 0)
                    denominationCounts.Remove(moneyValue);
            }


            givenAmount -= moneyValue;
            UpdatePartialWithdrawalUI();

            UpdateGivenAmountUI();

            if (lastMoney.CompareTag("Coin"))
                coinStackCount--;
            else
                billStackCount--;

        }
    }

    private float GetMoneyValueByObject(GameObject moneyObject)
    {
        for (int i = 0; i < moneyPrefabs.Count; i++)
        {
            if (moneyObject.name.Contains(moneyPrefabs[i].name))
            {
                return GetMoneyValue(i);
            }
        }
        return 0f;
    }
    public void ResetMoney()
    {
        foreach (GameObject money in spawnedMoney)
        {
            money.transform.DOMove(spawnPoint.position, moveDuration).SetEase(Ease.InQuad)
                .OnComplete(() => Destroy(money));
        }
        spawnedMoney.Clear();

        billStackCount = 0;
        coinStackCount = 0;
        givenAmount = 0f;
        changeDue = 0f;

        denominationCounts.Clear();
        UpdateGivenAmountUI();
    }

    public void ResetMoneyUI()
    {
        billStackCount = 0;
        coinStackCount = 0;
        givenAmount = 0f;
        changeDue = 0f;

        if (givenAmountText != null) givenAmountText.text = "$0.00";
        if (changeDueText != null) changeDueText.text = "$0.00";
        if (totalPurchaseText != null) totalPurchaseText.text = "$0.00";
    }

    public void GiveChange()
    {
        float changeDue = this.changeDue;

        if (!Mathf.Approximately(givenAmount, changeDue))
        {
            SoundManager.Instance.PlaySound("error");
            return;
        }

        MoneyManager.GiveChangeToCustomer(this, customerPosition);
    }

    public void DestroyMoney(GameObject money)
    {
        Destroy(money);
    }

    public GameObject SpawnMoneyForCustomer(int index, Vector3 spawnPosition)
    {
        if (index < 0 || index >= moneyPrefabs.Count)
        {
            Debug.LogError($"Error: Índice fuera de rango ({index}) en SpawnMoneyForCustomer.");
            return null;
        }

        Debug.Log($"Generando billete de índice {index} en posición {spawnPosition}");

        GameObject money = Instantiate(moneyPrefabs[index], spawnPosition, moneyPrefabs[index].transform.rotation);
        return money;
    }


    public void UpdateReceivedAmount(float receivedAmount, float changeDue)
    {
        this.changeDue = changeDue;

        if (changeDueText != null)
        {
            changeDueText.text = $"${changeDue:0.00}";
        }
    }

    public void ValidateChange()
    {
        float changeDue = this.changeDue;

        if (givenAmount == 0)
        {
            SoundManager.Instance.PlaySound("error");
            return;
        }

        if (!Mathf.Approximately(givenAmount, changeDue))
        {
            Debug.Log("Error: La cantidad entregada no es correcta.");
            SoundManager.Instance.PlaySound("error");
            return;
        }

        MoneyManager.GiveChangeToCustomer(this, customerPosition);
        FindObjectOfType<CashPaymentActivity>().OnCorrectChangeGiven();
    }

    public void UpdateTotalPurchaseText(float totalAmount)
    {
        if (totalPurchaseText != null)
        {
            totalPurchaseText.text = "$" + totalAmount.ToString("0.00");
        }
    }

    public float GetGivenAmount()
    {
        return givenAmount;
    }

    // ✅ NUEVO: UI de progreso para retiro parcial
    public void SetPartialWithdrawalTexts(TextMeshProUGUI targetText, TextMeshProUGUI progressText, int amount)
    {
        targetAmountText = targetText;
        currentAmountText = progressText;
        targetAmount = amount;
        UpdatePartialWithdrawalUI();
    }

    private void UpdatePartialWithdrawalUI()
    {
        if (targetAmountText != null)
            targetAmountText.text = $"${targetAmount:N0}";

        if (currentAmountText != null)
            currentAmountText.text = $"${GetTotalAmount():N0}";
    }

    public int GetTotalAmount()
    {
        int total = 0;
        foreach (GameObject money in spawnedMoney)
        {
            total += (int)GetMoneyValueByObject(money);
        }
        return total;
    }

    public bool ValidatePartialWithdrawal(int requiredAmount)
    {
        return GetTotalAmount() == requiredAmount;
    }

    public void SetCustomDeliveryTarget(Transform target, Action onCompleteCallback, int requiredAmount)
    {
        deliveryTarget = target;
        onMoneyDelivered = onCompleteCallback;
        deliverToCustomTarget = true;
        useCustomDeliveryFlow = true;
        requiredCustomAmount = requiredAmount;
    }

    public void DeliverMoneyToCustomTarget()
    {
        if (!deliverToCustomTarget || deliveryTarget == null)
        {
            Debug.LogWarning("No hay destino definido para la entrega personalizada.");
            return;
        }

        if (spawnedMoney.Count == 0)
        {
            Debug.LogWarning("No hay billetes para entregar al destino personalizado.");
            return;
        }

        foreach (GameObject money in spawnedMoney)
        {
            money.transform.DOMove(deliveryTarget.position, 0.5f).SetEase(Ease.InQuad)
                .OnComplete(() => Destroy(money));
        }

        spawnedMoney.Clear();
        onMoneyDelivered?.Invoke();
    }

    public void ValidateAlertAmount()
    {
        if (!useCustomDeliveryFlow || !deliverToCustomTarget)
        {
            Debug.LogWarning(" No está configurado el flujo de retiro personalizado.");
            SoundManager.Instance.PlaySound("error");
            return;
        }

        if (!ValidatePartialWithdrawal(requiredCustomAmount))
        {
            Debug.Log("No se entregó la cantidad exacta requerida.");
            SoundManager.Instance.PlaySound("error");
            return;
        }

        Debug.Log("Cantidad correcta entregada, entregando dinero.");
        DeliverMoneyToCustomTarget();
    }


}
