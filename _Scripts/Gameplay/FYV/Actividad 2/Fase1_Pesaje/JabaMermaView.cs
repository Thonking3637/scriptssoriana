using UnityEngine;

public class JabaMermaView : MonoBehaviour
{
    [Header("Tipo de Jaba")]
    public JabaTipo jabaTipo;

    [Header("Peso propio de la jaba")]
    public float pesoJabaKg = 2.80f;

    [Header("Contenido")]
    public Transform contenidoRoot;              // opcional, si no se asigna, usa this.transform
    public RuntimeCrateFiller filler;            // NUEVO: runtime-only
    public GameObject contenidoInstanciado;      // si en algún caso usas prefab lleno

    [Header("Estado")]
    public ProductoCatalogSO productoActual;
    public float pesoRegistradoKg = 0f;

    private void Awake()
    {
        if (!contenidoRoot) contenidoRoot = this.transform;
        if (filler == null) filler = GetComponentInChildren<RuntimeCrateFiller>(true);
        if (filler && filler.instancesParent == null) filler.instancesParent = contenidoRoot;
    }

    public void SetJabaTipo(JabaTipo tipo) => jabaTipo = tipo;

    public bool AceptaProducto(ProductoCatalogSO p)
    {
        if (p == null) return false;
        switch (jabaTipo)
        {
            case JabaTipo.Huevos: return p.tipoProducto == ProductoTipo.Huevo;
            case JabaTipo.Frutas: return p.tipoProducto == ProductoTipo.Fruta;
            case JabaTipo.Verduras: return p.tipoProducto == ProductoTipo.Verdura;
        }
        return false;
    }

    public void ClearContenido()
    {
        productoActual = null;
        pesoRegistradoKg = 0f;

        if (filler) filler.Clear();

        if (contenidoInstanciado)
        {
            Destroy(contenidoInstanciado);
            contenidoInstanciado = null;
        }
    }

    public void SetContenidoConProducto(ProductoCatalogSO p, float pesoAproxKg)
    {
        ClearContenido();
        productoActual = p;
        pesoRegistradoKg = pesoAproxKg;

        // Preferimos procedural (filler) si hay prefabUnidad
        if (filler != null && p.prefabUnidad != null)
        {
            // Regla simple: unidades ≈ k * kg (ajústala por producto)
            // p.ej.: 14 unidades por kg, mínimo 6, máximo 70
            int unidades = Mathf.Clamp(Mathf.RoundToInt(pesoAproxKg * 14f), 6, 70);

            filler.SetPrefabAndFill(p.prefabUnidad, unidades);
        }
        else if (p.prefabLleno != null)
        {
            // fallback: un prefab ya lleno
            contenidoInstanciado = Instantiate(p.prefabLleno, contenidoRoot);
            contenidoInstanciado.transform.localPosition = Vector3.zero;
            contenidoInstanciado.transform.localRotation = Quaternion.identity;
            contenidoInstanciado.transform.localScale = Vector3.one;
        }
        // si no hay unidad ni lleno, se guarda el peso igualmente (sin visual)
    }
}
