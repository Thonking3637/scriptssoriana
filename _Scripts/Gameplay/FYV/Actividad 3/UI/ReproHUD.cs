using UnityEngine;
using TMPro;

public class ReproHUD : MonoBehaviour
{
    [Header("Niveles por zona")]
    public TextMeshProUGUI lvlRepro;
    public TextMeshProUGUI lvlRecic;
    public TextMeshProUGUI lvlMallas;
    public TextMeshProUGUI lvlCajas;

    [Header("Despachos por zona")]
    public TextMeshProUGUI dspRepro;
    public TextMeshProUGUI dspRecic;
    public TextMeshProUGUI dspMallas;
    public TextMeshProUGUI dspCajas;

    [Header("Errores")]
    public TextMeshProUGUI tmpErrores;
    private int _errores;

    private void OnEnable()
    {
        ReproEvents.OnNivelActualizado += OnNivel;
        ReproEvents.OnDespachoProcesado += OnDespacho;
        ReproEvents.OnErrorDrop += OnError;
    }

    private void OnDisable()
    {
        ReproEvents.OnNivelActualizado -= OnNivel;
        ReproEvents.OnDespachoProcesado -= OnDespacho;
        ReproEvents.OnErrorDrop -= OnError;
    }

    private void OnNivel(JabaTipo z, int nivel)
    {
        string txt = $"{nivel}/6";
        switch (z)
        {
            case JabaTipo.Reprocesos: if (lvlRepro) lvlRepro.text = txt; break;
            case JabaTipo.Reciclaje: if (lvlRecic) lvlRecic.text = txt; break;
            case JabaTipo.MallasPlasticas: if (lvlMallas) lvlMallas.text = txt; break;
            case JabaTipo.CajasReutilizables: if (lvlCajas) lvlCajas.text = txt; break;
        }
    }

    private void OnDespacho(JabaTipo z, int total)
    {
        switch (z)
        {
            case JabaTipo.Reprocesos: if (dspRepro) dspRepro.text = total.ToString(); break;
            case JabaTipo.Reciclaje: if (dspRecic) dspRecic.text = total.ToString(); break;
            case JabaTipo.MallasPlasticas: if (dspMallas) dspMallas.text = total.ToString(); break;
            case JabaTipo.CajasReutilizables: if (dspCajas) dspCajas.text = total.ToString(); break;
        }
    }

    private void OnError()
    {
        _errores++;
        if (tmpErrores) tmpErrores.text = _errores.ToString();
    }
}
