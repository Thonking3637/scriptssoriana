using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class NodoCamara : MonoBehaviour
{
    [Header("ID único del nodo (ej: A3, Centro, Congelados)")]
    public string id;

    [Header("Vecinos directamente conectados")]
    public List<NodoCamara> vecinos = new List<NodoCamara>();

    public Transform Punto => this.transform;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.2f);

        if (vecinos != null)
        {
            Gizmos.color = Color.yellow;
            foreach (var vecino in vecinos)
            {
                if (vecino != null)
                    Gizmos.DrawLine(transform.position, vecino.transform.position);
            }
        }

        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.3f, id);
    }
#endif
}
