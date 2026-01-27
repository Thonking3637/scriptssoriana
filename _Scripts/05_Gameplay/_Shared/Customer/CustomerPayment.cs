using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using System;

public class CustomerPayment : MonoBehaviour
{
    [Header("Money Configuration")]
    public MoneySpawner moneySpawner;
    public Transform customerMoneySpawnPoint;
    public Transform moneyCollectionPoint;

    [Header("UI Elements")]
    public TextMeshProUGUI receivedAmountText;
    public GameObject moneyPanel;

    [HideInInspector] public float receivedAmount = 0f;
    private float changeDue = 0f;

    // Lista de dinero actualmente en escena (del cliente)
    public List<GameObject> spawnedCustomerMoney = new List<GameObject>();

    public event Action<float> OnAllCustomerMoneyCollected;

    public void GenerateCustomerPayment(float totalAmount)
    {
        if (moneySpawner == null)
        {
            Debug.LogError("CustomerPayment: moneySpawner es NULL.");
            return;
        }
        if (customerMoneySpawnPoint == null || moneyCollectionPoint == null)
        {
            Debug.LogError("CustomerPayment: faltan puntos de spawn/colección.");
            return;
        }

        // Limpia dinero previo (si venía de un intento anterior)
        ClearSpawnedMoneyImmediate();

        receivedAmount = Mathf.Ceil((totalAmount * 1.2f) / 100f) * 100f;

        float remainingAmount = receivedAmount;
        float[] moneyValues = { 500f, 200f, 100f, 50f, 20f };
        int[] moneyIndexes = { 0, 1, 2, 3, 4 };

        for (int i = 0; i < moneyValues.Length; i++)
        {
            while (remainingAmount >= moneyValues[i])
            {
                remainingAmount -= moneyValues[i];

                GameObject money = moneySpawner.SpawnMoneyForCustomer(moneyIndexes[i], customerMoneySpawnPoint.position);
                if (money == null)
                {
                    Debug.LogError("CustomerPayment: No se pudo spawnear el billete.");
                    continue;
                }

                // ✅ Importantísimo para pooling + DOTween:
                // mata tweens previos del objeto reusado
                DOTween.Kill(money.transform);

                // ✅ NO addcomponent siempre. Reusar si ya existe.
                var collector = money.GetComponent<MoneyCollector>();
                if (collector == null) collector = money.AddComponent<MoneyCollector>();
                collector.Init(this, moneySpawner);

                spawnedCustomerMoney.Add(money);
            }
        }

        changeDue = receivedAmount - totalAmount;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (receivedAmountText != null)
            receivedAmountText.text = $"${receivedAmount:0.00}";

        // Si moneySpawner maneja UI/estado global
        if (moneySpawner != null)
            moneySpawner.UpdateReceivedAmount(receivedAmount, changeDue);
    }

    public float GetReceivedAmount() => receivedAmount;

    public void CollectCustomerMoney()
    {
        // Si no hay dinero, igual consideramos "coleccionado"
        if (spawnedCustomerMoney.Count == 0)
        {
            ShowMoneyPanel();
            OnAllCustomerMoneyCollected?.Invoke(receivedAmount);
            return;
        }

        // Mueve todos y al final limpia. Ojo: aquí no debes modificar la lista mientras iteras.
        for (int i = 0; i < spawnedCustomerMoney.Count; i++)
        {
            GameObject money = spawnedCustomerMoney[i];
            if (money == null) continue;

            DOTween.Kill(money.transform);

            money.transform.DOMove(moneyCollectionPoint.position, 0.5f)
                .SetEase(Ease.InQuad)
                .SetTarget(money.transform)
                .OnComplete(() =>
                {
                    // Si tienes pooling para billetes, aquí deberías ReturnToPool.
                    // Como tu flujo actual destruye, lo mantengo:
                    if (money != null) Destroy(money);
                });
        }

        spawnedCustomerMoney.Clear();

        ShowMoneyPanel();

        OnAllCustomerMoneyCollected?.Invoke(receivedAmount);
    }

    private void ShowMoneyPanel()
    {
        if (moneyPanel == null) return;

        moneyPanel.SetActive(true);

        // evita tweens acumulados si llaman varias veces
        DOTween.Kill(moneyPanel.transform);

        moneyPanel.transform.localScale = Vector3.zero;
        moneyPanel.transform.DOScale(Vector3.one, 0.3f)
            .SetEase(Ease.OutBack)
            .SetTarget(moneyPanel.transform);
    }

    public void ResetCustomerPayment()
    {
        receivedAmount = 0f;
        changeDue = 0f;

        ClearSpawnedMoneyMoveToCollector();

        if (receivedAmountText != null) receivedAmountText.text = "$0.00";
    }

    // Limpieza inmediata (sin animación) para evitar residuos de intentos anteriores
    private void ClearSpawnedMoneyImmediate()
    {
        for (int i = 0; i < spawnedCustomerMoney.Count; i++)
        {
            var money = spawnedCustomerMoney[i];
            if (money == null) continue;

            DOTween.Kill(money.transform);
            Destroy(money);
        }
        spawnedCustomerMoney.Clear();
    }

    // Limpieza con animación (como lo tenías), pero sin callbacks peligrosos
    private void ClearSpawnedMoneyMoveToCollector()
    {
        for (int i = 0; i < spawnedCustomerMoney.Count; i++)
        {
            var money = spawnedCustomerMoney[i];
            if (money == null) continue;

            DOTween.Kill(money.transform);

            money.transform.DOMove(moneyCollectionPoint.position, 0.5f)
                .SetEase(Ease.InQuad)
                .SetTarget(money.transform)
                .OnComplete(() =>
                {
                    if (money != null) Destroy(money);
                });
        }
        spawnedCustomerMoney.Clear();
    }

    private void OnDisable()
    {
        // ❌ NO hagas: OnAllCustomerMoneyCollected = null;
        // Eso borra listeners externos.
        // Solo asegúrate de limpiar tweens locales si quieres:
        if (moneyPanel != null) DOTween.Kill(moneyPanel.transform);

        for (int i = 0; i < spawnedCustomerMoney.Count; i++)
        {
            var money = spawnedCustomerMoney[i];
            if (money != null) DOTween.Kill(money.transform);
        }
    }
}
