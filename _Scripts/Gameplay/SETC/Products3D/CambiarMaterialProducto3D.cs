using UnityEngine;

public class CambiarMaterialProducto3D : MonoBehaviour
{
    [Header("Configuración")]
    public string productoIdEsperado;
    public GameObject producto3D; // El objeto 3D que ya está en escena
    public Material nuevoMaterial; // El material que se aplicará al colocarlo correctamente

    public void CambiarMaterialSiCoincide(string id)
    {
        if (id != productoIdEsperado) return;

        if (producto3D == null)
        {
            Debug.LogWarning("❌ No se asignó el producto 3D.");
            return;
        }

        var renderer = producto3D.GetComponentInChildren<MeshRenderer>();
        if (renderer != null)
        {
            renderer.material = nuevoMaterial;
        }
        else
        {
            Debug.LogWarning($"❌ El producto '{producto3D.name}' no tiene MeshRenderer en sus hijos.");
        }
    }
}
