using UnityEngine;
using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;

public class SupervisorMovement : MonoBehaviour
{
    public Transform entryPoint;
    public List<Transform> middlePath;
    public Transform exitPoint;
    public Animator animator;
    public float walkSpeed = 2f;

    public void GoToEntryPoint(Action onComplete)
    {
        MoveTo(entryPoint.position, onComplete);
    }

    public void GoThroughMiddlePath(Action onComplete)
    {
        StartCoroutine(FollowPath(middlePath, onComplete));
    }

    public void GoToExit(Action onComplete)
    {
        MoveTo(exitPoint.position, onComplete);
    }

    private void MoveTo(Vector3 position, Action onComplete)
    {
        if (animator != null)
            animator.SetBool("isWalking", true);

        float distance = Vector3.Distance(transform.position, position);
        float duration = distance / walkSpeed;

        transform.DORotateQuaternion(Quaternion.LookRotation(position - transform.position), 0.2f);
        transform.DOMove(position, duration).SetEase(Ease.Linear).OnComplete(() =>
        {
            if (animator != null)
                animator.SetBool("isWalking", false);

            onComplete?.Invoke();
        });
    }

    private IEnumerator FollowPath(List<Transform> points, Action onComplete)
    {
        foreach (var point in points)
        {
            bool reached = false;
            MoveTo(point.position, () => reached = true);
            yield return new WaitUntil(() => reached);
        }
        onComplete?.Invoke();
    }
}
