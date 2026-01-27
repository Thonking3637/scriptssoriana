using UnityEngine;
using DG.Tweening;

public static class MoneyManager
{
    public static void GiveChange(float changeAmount, MoneySpawner moneySpawner)
    {
        if (moneySpawner == null)
        {
            Debug.LogError("MoneySpawner no ha sido asignado a MoneyManager.");
            return;
        }

        Debug.Log($"🔹 Dando vuelto de ${changeAmount:0.00} MXN");

        float[] moneyValues = { 500f, 200f, 100f, 50f, 20f, 10f, 5f, 2f, 1f };
        int[] moneyIndexes = { 0, 1, 2, 3, 4, 5, 6, 7, 8 };

        for (int i = 0; i < moneyValues.Length; i++)
        {
            while (changeAmount >= moneyValues[i])
            {
                changeAmount -= moneyValues[i];
                moneySpawner.SpawnMoney(moneyIndexes[i]); // Ahora se llama a través de una instancia
            }
        }
    }
    public static void OpenMoneyPanel(GameObject moneyPanel, Vector2 startPosition, Vector2 endPosition)
    {
        if (moneyPanel == null) return;

        RectTransform rectTransform = moneyPanel.GetComponent<RectTransform>();
        rectTransform.anchoredPosition = startPosition;
        moneyPanel.SetActive(true);

        rectTransform.DOAnchorPos(endPosition, 0.5f).SetEase(Ease.OutBack);
        Debug.Log("🔹 Panel de MoneySpawner activado.");
    }

    public static void CloseMoneyPanel(GameObject moneyPanel, Vector2 hidePosition)
    {
        if (moneyPanel == null) return;

        RectTransform rectTransform = moneyPanel.GetComponent<RectTransform>();
        rectTransform.DOAnchorPos(hidePosition, 0.5f).SetEase(Ease.InQuad)
            .OnComplete(() => moneyPanel.SetActive(false));

        Debug.Log("🔹 Panel de MoneySpawner ocultado.");
    }

    public static void GiveChangeToCustomer(MoneySpawner moneySpawner, Transform customerPoint)
    {
        if (customerPoint == null || moneySpawner == null)
        {
            Debug.LogError("❌ Error: Parámetros inválidos en GiveChangeToCustomer.");
            return;
        }

        if (moneySpawner.spawnedMoney.Count == 0)
        {
            Debug.LogWarning("⚠️ No hay billetes para entregar al cliente. Se omite la entrega.");
            SoundManager.Instance.PlaySound("error");
            return;
        }

        foreach (GameObject money in moneySpawner.spawnedMoney)
        {
            money.transform.DOMove(customerPoint.position, 0.5f).SetEase(Ease.InQuad)
                .OnComplete(() => moneySpawner.DestroyMoney(money));
        }

        moneySpawner.spawnedMoney.Clear();
    }
}
