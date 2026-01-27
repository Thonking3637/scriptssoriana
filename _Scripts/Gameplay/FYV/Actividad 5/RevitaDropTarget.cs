using UnityEngine;

[RequireComponent(typeof(Collider))]
public class RevitaDropTarget : MonoBehaviour
{
    [Header("Tipo aceptado por este target")]
    public RevitaAccionTipo tipoAceptado = RevitaAccionTipo.Ninguno;

    [Header("Snap donde debe quedar el objeto")]
    public Transform snapPoint;

    [Header("Ocupación")]
    public bool permitirSoloUnaInstancia = true;
    GameObject _currentInstance;

    [Header("Actividad (para notificar)")]
    public RevitalizacionActivity activity;   // 👈 arrástralo en el inspector

    public bool CanSpawnInstance()
    {
        if (!permitirSoloUnaInstancia) return true;
        return _currentInstance == null;
    }

    public void MarkOccupied(GameObject instance)
    {
        _currentInstance = instance;
    }

    /// <summary>
    /// Llamado por UIDragToWorldRevita al soltar.
    /// </summary>
    public bool HandleDrop(RevitaAccionTipo tipo, Vector3 hitPos)
    {
        if (tipo == RevitaAccionTipo.Ninguno) return false;
        if (tipo != tipoAceptado) return false;

        if (!CanSpawnInstance())
            return false;

        SoundManager.Instance.PlaySound("success");

        // 🔹 Notificar al Activity según el tipo de botón
        if (activity != null)
        {
            switch (tipo)
            {
                case RevitaAccionTipo.Frio_Jaba:
                    activity.NotifyFrioColocadoDesdeUI();
                    break;

                case RevitaAccionTipo.Vitrina_Producto:
                    activity.NotifyVitrinaColocadaDesdeUI();
                    break;
            }
        }

        return true;
    }
}
