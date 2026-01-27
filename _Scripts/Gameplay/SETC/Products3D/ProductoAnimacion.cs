using UnityEngine;
using System.Collections;
using DG.Tweening;

public class ProductoAnimacion : MonoBehaviour
{
    [Header("Animación de giro")]
    public Vector3 ejesGiro = new Vector3(0, 360, 0);
    public float duracionGiro = 1.2f;

    [Header("Parpadeo")]
    public float duracionParpadeo = 0.1f;

    private Coroutine parpadeoCoroutine;

    public void EmpezarParpadeo()
    {
        if (parpadeoCoroutine == null)
            parpadeoCoroutine = StartCoroutine(ParpadearInfinitoCoroutine());
    }

    public void DetenerParpadeo()
    {
        if (parpadeoCoroutine != null)
        {
            StopCoroutine(parpadeoCoroutine);
            parpadeoCoroutine = null;

            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
                r.enabled = true;
        }
    }

    public void Girar(System.Action onComplete = null)
    {
        transform.DORotate(ejesGiro, duracionGiro, RotateMode.FastBeyond360)
            .SetEase(Ease.InOutSine)
            .OnComplete(() => onComplete?.Invoke());
    }

    private IEnumerator ParpadearInfinitoCoroutine()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) yield break;

        while (true)
        {
            foreach (var r in renderers)
                r.enabled = false;
            yield return new WaitForSeconds(duracionParpadeo);

            foreach (var r in renderers)
                r.enabled = true;
            yield return new WaitForSeconds(duracionParpadeo);
        }
    }
}
