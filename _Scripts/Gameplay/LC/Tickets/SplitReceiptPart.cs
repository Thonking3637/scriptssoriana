using UnityEngine;
using System.Collections;
using System;
using UnityEngine.EventSystems;

public class SplitReceiptPart : MonoBehaviour, IPointerClickHandler
{
    public enum Destination
    {
        Client,
        Register
    }

    public Destination destination;
    public Transform clientTarget;
    public Transform registerTarget;

    public static event Action OnReceiptPartDelivered;

    private bool isMoving = false;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isMoving) return;
        HandleClick();
    }

    private void HandleClick()
    {
        Transform target = destination == Destination.Client ? clientTarget : registerTarget;
        StartCoroutine(MoveToTarget(target));
    }

    private IEnumerator MoveToTarget(Transform target)
    {
        SoundManager.Instance.PlaySound("success");
        isMoving = true;

        while (Vector3.Distance(transform.position, target.position) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target.position, 3f * Time.deltaTime);
            yield return null;
        }

        OnReceiptPartDelivered?.Invoke();
        Destroy(gameObject);
    }
}
