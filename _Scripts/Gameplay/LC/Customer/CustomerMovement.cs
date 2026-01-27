using UnityEngine;
using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;

public class CustomerMovement : MonoBehaviour
{
    [Header("Customer Configuration")]
    public Transform checkoutPoint;
    public Transform pinEntryPoint;
    public List<Transform> exitPath;
    public Animator animator;
    public float walkSpeed = 2f;

    [Header("Rotation")]
    public float rotationClient = -180f;

    private CustomerSpawner spawner;

    // Para cortar flujos/tweens
    private Tween moveTween;
    private Tween rotateTween;
    private Tween idleDelayTween;
    private Coroutine pinRoutine;
    private Coroutine exitRoutine;

    public void Initialize(
        Transform checkout,
        Transform pinEntry,
        List<Transform> exitPoints,
        Animator anim,
        CustomerSpawner spawnerRef = null
    )
    {
        // ✅ Animator sí o sí (si no, al menos que falle explícito)
        if (anim == null)
        {
            Debug.LogError("CustomerMovement.Initialize: Animator es NULL en " + gameObject.name);
            return;
        }

        checkoutPoint = checkout;          // puede ser null (Supervisor)
        pinEntryPoint = pinEntry;          // puede ser null (Supervisor)
        exitPath = exitPoints;             // puede ser null/empty si no hay salida
        animator = anim;
        spawner = spawnerRef;

        KillAllTweensAndRoutines();

        // Mantén tu orientación base si la necesitas
        transform.rotation = Quaternion.Euler(0, -90, 0);

        // Reset flags de anim
        animator.SetBool("isWalking", false);
        animator.SetBool("isWaving", false);
        animator.SetBool("isTexting", false);
    }

    // =========================
    // Idle (waving -> texting)
    // =========================
    private void PlayIdleAnimation()
    {
        if (animator == null) return;

        animator.SetBool("isWaving", true);

        idleDelayTween?.Kill();
        idleDelayTween = DOVirtual.DelayedCall(3f, () =>
        {
            if (!isActiveAndEnabled || animator == null) return;
            animator.SetBool("isWaving", false);
            animator.SetBool("isTexting", true);
        }, ignoreTimeScale: false);
    }

    private void StopIdleAnimation()
    {
        idleDelayTween?.Kill();
        idleDelayTween = null;

        if (animator != null)
        {
            animator.SetBool("isWaving", false);
            animator.SetBool("isTexting", false);
        }
    }

    // =========================
    // Public API
    // =========================
    public void MoveToCheckout(Action onComplete = null)
    {
        if (checkoutPoint == null)
        {
            Debug.LogError("MoveToCheckout: checkoutPoint es NULL en " + gameObject.name);
            return;
        }

        StopIdleAnimation();
        MoveToPosition(checkoutPoint.position, -90, -1f, () =>
        {
            RotateTo(rotationClient, 0.5f, () =>
            {
                PlayIdleAnimation();
                onComplete?.Invoke();
            });
        });
    }

    public void MoveToPinEntry(Action onComplete = null)
    {
        if (pinEntryPoint == null)
        {
            Debug.LogError("MoveToPinEntry: pinEntryPoint es NULL en " + gameObject.name);
            return;
        }
        if (checkoutPoint == null)
        {
            Debug.LogError("MoveToPinEntry: checkoutPoint es NULL en " + gameObject.name);
            return;
        }

        StopIdleAnimation();

        RotateTo(-90, 0.5f, () =>
        {
            MoveToPosition(pinEntryPoint.position, -90, -1f, () =>
            {
                RotateTo(-180, 0.5f, () =>
                {
                    onComplete?.Invoke();

                    if (pinRoutine != null) StopCoroutine(pinRoutine);
                    pinRoutine = StartCoroutine(WaitAtPinEntryThenReturn());
                });
            });
        });
    }

    public void MoveToExit(Action onComplete = null)
    {
        if (exitPath == null || exitPath.Count == 0)
        {
            Debug.LogWarning("MoveToExit: exitPath vacío en " + gameObject.name);
            onComplete?.Invoke();
            SafeReturnToPoolOrDisable();
            return;
        }

        StopIdleAnimation();

        MoveToPosition(exitPath[0].position, -90, -1f, () =>
        {
            if (exitRoutine != null) StopCoroutine(exitRoutine);
            exitRoutine = StartCoroutine(FollowExitPath(onComplete));
        });
    }

    public void MoveToPosition(Vector3 targetPosition, float targetYRotation, float duration = -1f, Action onComplete = null)
    {
        moveTween?.Kill();
        rotateTween?.Kill();

        if (duration < 0f)
        {
            float distance = Vector3.Distance(transform.position, targetPosition);
            duration = Mathf.Max(0.01f, distance / Mathf.Max(0.01f, walkSpeed));
        }

        RotateTo(targetYRotation, 0.5f, () =>
        {
            if (animator != null) animator.SetBool("isWalking", true);

            moveTween = transform.DOMove(targetPosition, duration)
                .SetEase(Ease.Linear)
                .OnComplete(() =>
                {
                    if (animator != null) animator.SetBool("isWalking", false);
                    onComplete?.Invoke();
                });
        });
    }

    // =========================
    // Internals
    // =========================
    private IEnumerator WaitAtPinEntryThenReturn()
    {
        yield return new WaitForSeconds(0.5f);

        if (!isActiveAndEnabled) yield break;

        RotateTo(-270, 0.5f, () =>
        {
            if (checkoutPoint == null) return;

            MoveToPosition(checkoutPoint.position, -270, -1f, () =>
            {
                RotateTo(-180, 0.5f, null);
            });
        });
    }

    private IEnumerator FollowExitPath(Action onComplete)
    {
        for (int i = 1; i < exitPath.Count; i++)
        {
            var a = exitPath[i - 1];
            var b = exitPath[i];
            if (a == null || b == null) continue;

            float distance = Vector3.Distance(a.position, b.position);
            float duration = Mathf.Max(0.01f, distance / Mathf.Max(0.01f, walkSpeed));

            bool reached = false;
            float rot = (i == exitPath.Count - 1) ? 0f : -90f;

            MoveToPosition(b.position, rot, duration, () => reached = true);
            yield return new WaitUntil(() => reached);
        }

        onComplete?.Invoke();
        SafeReturnToPoolOrDisable();
    }

    private void RotateTo(float yRot, float duration, Action onComplete)
    {
        rotateTween?.Kill();
        rotateTween = transform.DORotateQuaternion(Quaternion.Euler(0f, yRot, 0f), duration)
            .SetEase(Ease.OutSine)
            .OnComplete(() => onComplete?.Invoke());
    }

    private void SafeReturnToPoolOrDisable()
    {
        if (spawner != null)
        {
            spawner.RemoveCustomer(gameObject);
            return;
        }

        Debug.LogWarning("CustomerMovement: spawner es NULL, desactivando objeto: " + gameObject.name);
        gameObject.SetActive(false);
    }

    private void KillAllTweensAndRoutines()
    {
        moveTween?.Kill(); moveTween = null;
        rotateTween?.Kill(); rotateTween = null;
        idleDelayTween?.Kill(); idleDelayTween = null;

        if (pinRoutine != null) { StopCoroutine(pinRoutine); pinRoutine = null; }
        if (exitRoutine != null) { StopCoroutine(exitRoutine); exitRoutine = null; }

        StopIdleAnimation();
    }

    private void OnDisable()
    {
        KillAllTweensAndRoutines();

        if (animator != null)
        {
            animator.SetBool("isWalking", false);
            animator.SetBool("isWaving", false);
            animator.SetBool("isTexting", false);
        }
    }
}
