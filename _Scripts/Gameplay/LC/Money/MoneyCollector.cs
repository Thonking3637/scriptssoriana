using UnityEngine;
using DG.Tweening;
using System;
using UnityEngine.EventSystems;


public class MoneyCollector : MonoBehaviour, IPointerClickHandler
{
    private CustomerPayment customerPayment;
    private MoneySpawner moneySpawner;
    private bool isCollected = false;

    public event Action<float> OnAllCustomerMoneyCollected;

    public void Init(CustomerPayment customerPayment, MoneySpawner moneySpawner)
    {
        this.customerPayment = customerPayment;
        this.moneySpawner = moneySpawner;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isCollected) return;
        isCollected = true;

        transform.DOMove(customerPayment.moneyCollectionPoint.position, 0.5f)
            .SetEase(Ease.InQuad)
            .OnComplete(() =>
            {
                // Guardar referencia antes de destruir para remover bien
                var go = gameObject;

                Destroy(go);
                customerPayment.spawnedCustomerMoney.Remove(go);

                if (customerPayment.spawnedCustomerMoney.Count == 0)
                {
                    customerPayment.CollectCustomerMoney(); // esto ahora dispara el evento
                }
            });
    }
}
