using UnityEngine;

public class UbicacionArea : MonoBehaviour
{
    public JabaTipo tipoArea;
    public UbicacionSlot[] slots;

    [Header("Claseo opcional")]
    public bool validarMadurez = false;
    public MaturityLevel requiredLevel = MaturityLevel.NoMaduro;

    public UbicacionSlot GetFirstFreeSlot()
    {
        if (slots == null) return null;
        foreach (var s in slots)
            if (s != null && !s.ocupado) return s;
        return null;
    }

    public void ClearAll()
    {
        if (slots == null) return;
        foreach (var s in slots) s?.Clear();
    }

    // Valida solo por JabaTipo (modo viejo) o por JabaTipo+Madurez (Claseo)
    public bool ValidaArea(JabaTipo fruta, MaturityLevel level)
    {
        if (!validarMadurez)
            return tipoArea == fruta;

        return tipoArea == fruta && level == requiredLevel;
    }

    // Overload de compat si ya tienes llamadas con string:
    public bool ValidaArea(JabaTipo fruta, string etiqueta)
    {
        if (!validarMadurez) return tipoArea == fruta;
        if (System.Enum.TryParse<MaturityLevel>(etiqueta, true, out var parsed))
            return tipoArea == fruta && parsed == requiredLevel;
        return false;
    }
}
