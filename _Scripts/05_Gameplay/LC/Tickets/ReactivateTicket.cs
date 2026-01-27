using UnityEngine;
using DG.Tweening;
using System;
using UnityEngine.EventSystems;

public class ReactivateTicket : MonoBehaviour, IPointerClickHandler
{
    public Transform targetPoint;
    public Transform returnPoint;
    public float moveDuration = 0.5f;
    public float waitDuration = 1f;

    private Action onComplete;
    private bool alreadyUsed = false;

    private void OnEnable()
    {
        alreadyUsed = false;
    }

    private void OnDisable()
    {
        DOTween.Kill(transform);
    }

    public void Initialize(Transform toPoint, Transform returnTo, Action onCompleteCallback)
    {
        targetPoint = toPoint;
        returnPoint = returnTo;
        onComplete = onCompleteCallback;

        alreadyUsed = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (alreadyUsed) return;
        alreadyUsed = true;
        MoveToTarget();
    }

    private void MoveToTarget()
    {
        transform.DOMove(targetPoint.position, moveDuration).OnComplete(() =>
        {
            DOVirtual.DelayedCall(waitDuration, () =>
            {
                transform.DOMove(returnPoint.position, moveDuration).OnComplete(() =>
                {
                    onComplete?.Invoke();
                    gameObject.SetActive(false);
                });
            });
        });
    }
}
