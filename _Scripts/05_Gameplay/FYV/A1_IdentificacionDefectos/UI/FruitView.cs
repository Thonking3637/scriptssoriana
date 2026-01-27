using UnityEngine;

public class FruitView : MonoBehaviour
{
    [Header("Base (visible)")]
    public MeshFilter baseFilter;
    public MeshRenderer baseRenderer;

    [Header("Escala")]
    public Vector3 defaultScale = Vector3.one;

    public void Apply(Mesh mesh, Material[] mats, Vector3 localScale)
    {
        // 1️⃣ Actualiza base y overlay de forma segura
        if (baseFilter) baseFilter.sharedMesh = mesh;
        if (baseRenderer)
            baseRenderer.sharedMaterials = (mats != null && mats.Length > 0)
                ? mats
                : baseRenderer.sharedMaterials;

        // 2️⃣ Escala segura
        transform.localScale = (localScale == Vector3.zero) ? defaultScale : localScale;

        // 3️⃣ Habilita el OneFingerRotateChild solo en el hijo activo
        OneFingerRotate rotadorActivo = null;

        foreach (Transform child in transform)
        {
            var rot = child.GetComponent<OneFingerRotate>();
            var mr = child.GetComponent<MeshRenderer>();
            bool isActive = child.gameObject.activeInHierarchy && mr != null;

            if (rot != null)
            {
                rot.enabled = isActive; // Solo el hijo visible recibe input
                if (isActive)
                {
                    rotadorActivo = rot;
                }
            }
        }

        if (rotadorActivo == null)
            Debug.LogWarning("[FruitView] No se encontró OneFingerRotateChild activo en los hijos visibles.");
    }
}
