// SlidingDoor.cs
using UnityEngine;
using DG.Tweening;

public class SlidingDoor : MonoBehaviour
{
    public enum Axis { X, Z }

    [Header("Hojas")]
    public Transform hojaIzquierda;
    public Transform hojaDerecha;

    [Header("Movimiento")]
    public Axis eje = Axis.X;          // si tu apertura corre sobre Z, cámbialo a Z
    public float distancia = 1.2f;     // cuánto se desplaza cada hoja
    public float duracion = 0.6f;
    public Ease ease = Ease.InOutSine;

    [Tooltip("Usa esto si la geometría quedó invertida")]
    public bool invertirIzquierda = false;
    public bool invertirDerecha = false;

    Vector3 posCerradaIzq, posCerradaDer;
    bool abierta;

    void Awake()
    {
        if (hojaIzquierda) posCerradaIzq = hojaIzquierda.localPosition;
        if (hojaDerecha) posCerradaDer = hojaDerecha.localPosition;
    }

    Vector3 AxisVec() => (eje == Axis.X) ? Vector3.right : Vector3.forward;

    public void Abrir(bool instant = false)
    {
        if (abierta) return;
        abierta = true;

        Vector3 ax = AxisVec();

        if (hojaIzquierda)
        {
            float sgn = invertirIzquierda ? 1f : -1f; // izquierda usualmente -X (o -Z)
            Vector3 dst = posCerradaIzq + ax * sgn * distancia;
            hojaIzquierda.DOKill();
            var t = hojaIzquierda.DOLocalMove(dst, instant ? 0f : duracion).SetEase(ease);
            if (instant) t.Complete();
        }

        if (hojaDerecha)
        {
            float sgn = invertirDerecha ? -1f : 1f; // derecha usualmente +X (o +Z)
            Vector3 dst = posCerradaDer + ax * sgn * distancia;
            hojaDerecha.DOKill();
            var t = hojaDerecha.DOLocalMove(dst, instant ? 0f : duracion).SetEase(ease);
            if (instant) t.Complete();
        }
    }

    public void Cerrar(bool instant = false)
    {
        if (!abierta) return;
        abierta = false;

        if (hojaIzquierda)
        {
            hojaIzquierda.DOKill();
            var t = hojaIzquierda.DOLocalMove(posCerradaIzq, instant ? 0f : duracion).SetEase(ease);
            if (instant) t.Complete();
        }
        if (hojaDerecha)
        {
            hojaDerecha.DOKill();
            var t = hojaDerecha.DOLocalMove(posCerradaDer, instant ? 0f : duracion).SetEase(ease);
            if (instant) t.Complete();
        }
    }
}
