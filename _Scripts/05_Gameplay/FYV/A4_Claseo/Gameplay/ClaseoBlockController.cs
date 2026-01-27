using UnityEngine;

public class ClaseoBlockController : MonoBehaviour
{
    [Header("Áreas (jabas) de este producto: NoMaduro / MedioMaduro / Maduro")]
    public ClaseoAreaCounter areaNoMaduro;
    public ClaseoAreaCounter areaMedioMaduro;
    public ClaseoAreaCounter areaMaduro;

    [Header("Lanza cuando las 3 áreas alcanzan su meta")]
    public System.Action onBlockCompleted;

    private void OnEnable() { Hook(true); }
    private void OnDisable() { Hook(false); }

    private void Hook(bool add)
    {
        if (areaNoMaduro != null)
        {
            if (add) areaNoMaduro.onAreaCompleted += OnAreaCompleted;
            else areaNoMaduro.onAreaCompleted -= OnAreaCompleted;
        }
        if (areaMedioMaduro != null)
        {
            if (add) areaMedioMaduro.onAreaCompleted += OnAreaCompleted;
            else areaMedioMaduro.onAreaCompleted -= OnAreaCompleted;
        }
        if (areaMaduro != null)
        {
            if (add) areaMaduro.onAreaCompleted += OnAreaCompleted;
            else areaMaduro.onAreaCompleted -= OnAreaCompleted;
        }
    }

    private void OnAreaCompleted(ClaseoAreaCounter _)
    {
        if (areaNoMaduro && areaMedioMaduro && areaMaduro)
        {
            if (areaNoMaduro.count >= areaNoMaduro.target &&
                areaMedioMaduro.count >= areaMedioMaduro.target &&
                areaMaduro.count >= areaMaduro.target)
            {
                onBlockCompleted?.Invoke();
            }
        }
    }

    public void ResetAllAreas(int targetPerArea)
    {
        if (areaNoMaduro) { areaNoMaduro.target = targetPerArea; areaNoMaduro.ResetCounter(); }
        if (areaMedioMaduro) { areaMedioMaduro.target = targetPerArea; areaMedioMaduro.ResetCounter(); }
        if (areaMaduro) { areaMaduro.target = targetPerArea; areaMaduro.ResetCounter(); }
    }
}
