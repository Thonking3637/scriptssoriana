using UnityEngine;

[DisallowMultipleComponent]
public class RevitaVegetalRemovable : MonoBehaviour
{
    [Tooltip("Sólo para ayudarte en editor; el sistema usa el Tag 'Removable'.")]
    public bool gizmo = true;

    private void OnValidate()
    {
        if (this.tag != "Removable")
            Debug.LogWarning($"[Revita] '{name}' debería tener Tag 'Removable' para el paso de Recorte.");
        if (!GetComponent<Collider>())
            Debug.LogWarning($"[Revita] '{name}' necesita un Collider para recibir TAP.");
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!gizmo) return;
        Gizmos.color = new Color(0f, 1f, 0.2f, 0.35f);
        Gizmos.DrawCube(transform.position, Vector3.one * 0.05f);
    }
#endif
}
