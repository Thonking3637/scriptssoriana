using UnityEngine;
using System;
using System.Collections;
using DG.Tweening;
using UnityEngine.EventSystems;

public class CardInteraction : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Transform cardFirstPosition;
    public Transform cardSecondPosition;
    public Transform cardReturnPosition;
    public MeshRenderer secondPositionIndicator;

    private bool isCardUsed = false;
    private bool isCardGrabbed = false;
    private bool reachedFirstPosition = false;

    private Camera mainCamera;
    private Collider cardCollider;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Coroutine blinkCoroutine;

    public event Action OnCardMovedToFirstPosition;
    public event Action OnCardMovedToSecondPosition;
    public event Action OnCardReturned;

    private void Start()
    {
        mainCamera = Camera.main;
        cardCollider = GetComponent<Collider>();
        initialPosition = transform.position;
        initialRotation = transform.rotation;

        if (secondPositionIndicator != null)
            secondPositionIndicator.enabled = false;
    }

    // ✅ Reemplaza OnMouseDown
    public void OnPointerClick(PointerEventData eventData)
    {
        if (isCardUsed) return;

        if (!reachedFirstPosition)
            MoveToFirstPosition();
    }

    private void MoveToFirstPosition()
    {
        SoundManager.Instance.PlaySound("success");

        if (cardCollider != null) cardCollider.enabled = false;

        transform.DOMove(cardFirstPosition.position, 1f).SetEase(Ease.OutQuad);
        transform.DORotateQuaternion(cardFirstPosition.rotation, 1f).SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                if (cardCollider != null) cardCollider.enabled = true;

                reachedFirstPosition = true;

                if (secondPositionIndicator != null)
                {
                    secondPositionIndicator.enabled = true;
                    blinkCoroutine = StartCoroutine(BlinkIndicator());
                }

                OnCardMovedToFirstPosition?.Invoke();
            });
    }
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!reachedFirstPosition || isCardUsed) return;
        isCardGrabbed = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!reachedFirstPosition || !isCardGrabbed || isCardUsed) return;

        Vector3 world = ScreenToWorldAtObjectDepth(eventData.position);
        transform.position = new Vector3(world.x, cardFirstPosition.position.y, cardFirstPosition.position.z);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!reachedFirstPosition || !isCardGrabbed || isCardUsed) return;

        isCardGrabbed = false;

        if (Vector3.Distance(transform.position, cardSecondPosition.position) < 0.1f)
        {
            MoveToSecondPosition();
        }
        else
        {
            transform.DOMove(cardFirstPosition.position, 0.5f).SetEase(Ease.OutQuad);
            transform.DORotateQuaternion(cardFirstPosition.rotation, 0.5f).SetEase(Ease.OutQuad);
        }
    }

    private void MoveToSecondPosition()
    {
        isCardGrabbed = false;

        transform.position = cardSecondPosition.position;
        transform.rotation = cardSecondPosition.rotation;

        if (secondPositionIndicator != null)
            secondPositionIndicator.enabled = false;

        if (blinkCoroutine != null)
            StopCoroutine(blinkCoroutine);

        OnCardMovedToSecondPosition?.Invoke();
        MoveToReturnPosition();
    }

    private IEnumerator BlinkIndicator()
    {
        while (true)
        {
            if (secondPositionIndicator != null)
                secondPositionIndicator.enabled = !secondPositionIndicator.enabled;

            yield return new WaitForSeconds(0.5f);
        }
    }

    public void ResetCard()
    {
        isCardUsed = false;
        isCardGrabbed = false;
        reachedFirstPosition = false;

        if (secondPositionIndicator != null)
            secondPositionIndicator.enabled = false;

        transform.position = initialPosition;
        transform.rotation = initialRotation;

        if (cardCollider != null)
            cardCollider.enabled = true;

        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }

        gameObject.SetActive(false);
    }

    private void MoveToReturnPosition()
    {
        float returnSpeed = 0.5f;

        transform.DOMove(cardReturnPosition.position, returnSpeed).SetEase(Ease.Linear);
        transform.DORotateQuaternion(cardReturnPosition.rotation, returnSpeed).SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                isCardUsed = true;
                OnCardReturned?.Invoke();
                gameObject.SetActive(false);
            });
    }

    private Vector3 ScreenToWorldAtObjectDepth(Vector2 screenPos)
    {
        if (mainCamera == null) mainCamera = Camera.main;

        float z = mainCamera.WorldToScreenPoint(transform.position).z;
        Vector3 p = new Vector3(screenPos.x, screenPos.y, z);
        return mainCamera.ScreenToWorldPoint(p);
    }
}
