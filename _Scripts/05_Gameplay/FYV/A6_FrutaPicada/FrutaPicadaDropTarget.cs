using UnityEngine;

public class FrutaPicadaDropTarget : MonoBehaviour
{
    [Header("Qué acciones acepta esta zona")]
    public FrutaAccionTipo[] accionesAceptadas;

    [Header("Snap para el objeto final (opcional)")]
    public Transform snapPoint;

    [Header("Controllers")]
    public FrutaPicadaLavadoController lavado;
    public FrutaPicadaDesinfeccionController desinfeccion;
    public FrutaPicadaCorteExhibicionController corteExhibicion;
    public FrutaPicadaCorteCoctelController corteCoctel;
    public FrutaPicadaVitrinaController vitrina;

    [Header("Ocupación (para no spawnear más de 1)")]
    [Tooltip("Si está activo, esta zona sólo puede tener 1 instancia creada por UIDragToWorldFruta.")]
    public bool singleInstance = true;

    [Tooltip("Se marca true cuando ya se instanció algo aquí. Puedes limpiarlo manualmente desde la actividad si destruyes el objeto.")]
    public bool ocupado;

    /// <summary>
    /// Devuelve true si esta zona puede aceptar una nueva instancia visual.
    /// </summary>
    public bool CanSpawnInstance()
    {
        if (!singleInstance) return true;
        return !ocupado;
    }

    /// <summary>
    /// Marca la zona como ocupada (llamado normalmente al instanciar).
    /// </summary>
    public void MarkOccupied()
    {
        if (singleInstance)
            ocupado = true;
    }

    /// <summary>
    /// Libera manualmente la zona (por si destruyes el objeto de la escena y quieres permitir otro spawn).
    /// </summary>
    public void ClearOccupied()
    {
        ocupado = false;
    }

    public bool CanAccept(FrutaAccionTipo accion)
    {
        if (accionesAceptadas == null) return false;
        for (int i = 0; i < accionesAceptadas.Length; i++)
        {
            if (accionesAceptadas[i] == accion) return true;
        }
        return false;
    }

    public void HandleDrop(FrutaAccionTipo accion, Vector3 worldPos)
    {
        if (!CanAccept(accion))
        {
            // Error simple
            if (lavado != null) lavado.PlayError();
            else if (desinfeccion != null) desinfeccion.PlayError();
            else if (corteExhibicion != null) corteExhibicion.PlayError();
            else if (corteCoctel != null) corteCoctel.PlayError();
            else if (vitrina != null) vitrina.PlayError();
            return;
        }

        // Rutear según acción
        switch (accion)
        {
            // LAVADO
            case FrutaAccionTipo.Lavado_Agua:
                if (lavado != null) lavado.TryAplicarAgua();
                break;

            case FrutaAccionTipo.Lavado_Papaya:
                if (lavado != null) lavado.TryColocarPapayaLavadero();
                break;

            case FrutaAccionTipo.Lavado_Desinfectante:
                if (lavado != null) lavado.TryAplicarDesinfectante();
                break;

            // DESINFECCIÓN
            case FrutaAccionTipo.Desinfeccion_Jaba:
                if (desinfeccion != null) desinfeccion.TryColocarJaba();
                break;

            case FrutaAccionTipo.Desinfeccion_Agua:
                if (desinfeccion != null) desinfeccion.TryAplicarAguaEnJaba();
                break;

            case FrutaAccionTipo.Desinfeccion_Desinfectante:
                if (desinfeccion != null) desinfeccion.TryAplicarDesinfectanteEnJaba();
                break;

            case FrutaAccionTipo.Desinfeccion_Papaya:
                if (desinfeccion != null) desinfeccion.TryColocarPapayaEnJaba();
                break;

            case FrutaAccionTipo.Desinfeccion_Timer:
                if (desinfeccion != null) desinfeccion.TryIniciarTimer();
                break;

            // CORTE EXHIBICIÓN 
            case FrutaAccionTipo.Corte_Exhibicion_Papaya:
                if (corteExhibicion != null) corteExhibicion.TryColocarFrutaEnMesa();
                break;

            case FrutaAccionTipo.Corte_Exhibicion_Cuchillo:
                if (corteExhibicion != null) corteExhibicion.TryUsarCuchillo();
                break;

            case FrutaAccionTipo.Corte_Exhibicion_Film:
                if (corteExhibicion != null) corteExhibicion.TryAplicarFillFilm();
                break;

            case FrutaAccionTipo.Corte_Exhibicion_Etiqueta:
                if (corteExhibicion != null) corteExhibicion.TryAplicarEtiqueta();
                break;

            case FrutaAccionTipo.Corte_Exhibicion_Supermarket:
                if (corteExhibicion != null) corteExhibicion.TryEnviarASupermarket();
                break;
            // CORTE COCTEL 
            case FrutaAccionTipo.Corte_Coctel_Papaya:
                if (corteCoctel != null) corteCoctel.TryColocarFrutaEnMesa();
                break;

            case FrutaAccionTipo.Corte_Coctel_Cuchillo1:
                if (corteCoctel != null) corteCoctel.TryUsarCuchillo1();
                break;

            case FrutaAccionTipo.Corte_Coctel_Cuchillo2:
                if (corteCoctel != null) corteCoctel.TryUsarCuchillo2();
                break;

            case FrutaAccionTipo.Corte_Coctel_Bagazo:
                if (corteCoctel != null) corteCoctel.TryRetirarBagazo();
                break;

            case FrutaAccionTipo.Corte_Coctel_Cuchillo3:
                if (corteCoctel != null) corteCoctel.TryUsarCuchillo3();
                break;

            case FrutaAccionTipo.Corte_Coctel_Taper:
                if (corteCoctel != null) corteCoctel.TryColocarTaper();
                break;

            case FrutaAccionTipo.Corte_Coctel_Etiqueta:
                if (corteCoctel != null) corteCoctel.TryAplicarEtiqueta();
                break;

            // VITRINA
            case FrutaAccionTipo.Vitrina_ProductoFinal:
                if (vitrina != null) vitrina.OnProductoColocadoEnVitrina();
                break;
        }
    }
}
