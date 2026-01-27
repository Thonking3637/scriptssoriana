using UnityEngine;
using DG.Tweening;
using System;
using UnityEngine.EventSystems;

public class Ticket : MonoBehaviour, IPointerClickHandler
{
    public Transform targetPoint;
    public bool isMoving = false;

    public event Action OnTicketDelivered;

    public void Initialize(Transform target)
    {
        if (target == null)
        {
            Debug.LogError("Error: targetPoint en Ticket es NULL.");
            return;
        }

        targetPoint = target;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        MoveTicket();
    }

    private void MoveTicket()
    {
        if (isMoving || targetPoint == null) return;

        isMoving = true;

        transform.DORotate(new Vector3(0, 146, 0), 0.5f).SetEase(Ease.OutBack)
            .OnComplete(() =>
            {
                if (!gameObject.activeInHierarchy) return;

                Vector3 startPos = transform.position;
                Vector3 midPoint = (startPos + targetPoint.position) / 2 + Vector3.up * 0.8f;
                Vector3[] path = new Vector3[] { startPos, midPoint, targetPoint.position };

                transform.DOPath(path, 1.5f, PathType.CatmullRom)
                    .SetEase(Ease.InOutQuad)
                    .OnComplete(() =>
                    {
                        OnTicketDelivered?.Invoke();

                        if (gameObject != null)
                            Destroy(gameObject);
                    });
            });
    }
}
