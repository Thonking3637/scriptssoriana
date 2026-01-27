using UnityEngine;

[RequireComponent(typeof(Collider))]
public class UbicacionAreaAutoStack : MonoBehaviour
{
    [Header("Refs")]
    public UbicacionArea area;

    [Header("Orden de llenado (3D)")]
    public bool fillBottomToTop = true;

    private void Reset()
    {
        if (!area) area = GetComponentInChildren<UbicacionArea>();
    }

    /// <summary>
    /// Coloca el prefab en el primer UbicacionSlot libre de esta jaba (3D).
    /// </summary>
    public bool TryPlace(GameObject prefab, JabaTipo itemTipo, out UbicacionSlot placedSlot)
    {
        placedSlot = null;

        if (!area || prefab == null) return false;
        if (area.tipoArea != itemTipo) return false;
        if (area.slots == null || area.slots.Length == 0) return false;

        if (fillBottomToTop)
        {
            for (int i = 0; i < area.slots.Length; i++)
            {
                var s = area.slots[i];
                if (s != null && !s.ocupado)
                {
                    s.Place(prefab);
                    placedSlot = s;

                    // 🔴 Hook de Claseo: suma 1 si hay contador en este área
                    var counter = GetComponentInParent<ClaseoAreaCounter>();
                    if (counter != null) counter.Add(1);

                    return true;
                }
            }
        }
        else
        {
            for (int i = area.slots.Length - 1; i >= 0; i--)
            {
                var s = area.slots[i];
                if (s != null && !s.ocupado)
                {
                    s.Place(prefab);
                    placedSlot = s;

                    // 🔴 Hook de Claseo: suma 1 si hay contador en este área
                    var counter = GetComponentInParent<ClaseoAreaCounter>();
                    if (counter != null) counter.Add(1);

                    return true;
                }
            }
        }

        return false; // no hay slots libres
    }

}
