using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(UbicacionArea))]
public class ReproLaneFromUbicacion : MonoBehaviour
{
    [Header("Zona")]
    public JabaTipo zona;                 // Reprocesos/Reciclaje/MallasPlasticas/CajasReutilizables
    [Tooltip("Slots llenos para habilitar PROCESAR (equivale a 1.80 m)")]
    public int nivelesObjetivo = 4;

    [Header("Refs")]
    public UbicacionArea area;            // si es null, se autollenará en Awake
    public Button btnProcesar;            // botón PROCESAR de esta zona

    public int NivelActual { get; private set; }
    public int Despachos { get; private set; }

    // Para el tutorial (avisar una sola vez cuando llega a 4)
    public System.Action<ReproLaneFromUbicacion> OnReachTarget;
    public System.Action<ReproLaneFromUbicacion> OnProcesado;

    private bool _reachAnnounced;
    private float _nextCheck;
    private void Awake()
    {
        if (!area) area = GetComponent<UbicacionArea>();
        if (btnProcesar)
        {
            btnProcesar.interactable = false;
            btnProcesar.onClick.AddListener(Procesar);
        }
    }

    private void Update()
    {
        if (Time.time < _nextCheck) return;
        _nextCheck = Time.time + 0.2f; // actualiza cada 0.2 s, no cada frame

        int ocupados = 0;
        if (area && area.slots != null)
            foreach (var s in area.slots) if (s != null && s.ocupado) ocupados++;

        if (ocupados != NivelActual)
        {
            NivelActual = ocupados;
            ReproEvents.OnNivelActualizado?.Invoke(zona, NivelActual);
        }

        bool puedeProcesar = NivelActual >= nivelesObjetivo;
        if (btnProcesar) btnProcesar.interactable = puedeProcesar;

        if (puedeProcesar && !_reachAnnounced)
        {
            _reachAnnounced = true;

            // 🔊 Sonido cuando se habilita el botón PROCESAR por primera vez
            try { SoundManager.Instance.PlaySound("listoprocesar"); } catch { }

            OnReachTarget?.Invoke(this);
        }
        else if (!puedeProcesar)
        {
            _reachAnnounced = false;
        }
    }


    public void Procesar()
    {
        if (area) area.ClearAll(); // limpia pila visual (slots)
        NivelActual = 0;

        if (btnProcesar) btnProcesar.interactable = false;

        Despachos++;
        ReproEvents.OnDespachoProcesado?.Invoke(zona, Despachos);
        OnProcesado?.Invoke(this);
    }
}
