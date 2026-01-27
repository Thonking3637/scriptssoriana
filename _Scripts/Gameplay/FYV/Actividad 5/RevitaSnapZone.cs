using UnityEngine;
using System;

[RequireComponent(typeof(Collider))]
public class RevitaSnapZone : MonoBehaviour
{
    [Header("Snap")]
    public Transform snapPoint;
    public bool alignRotation = true;
    public bool zeroVelocity = true;

    [Header("Filtro")]
    public string requiredRootNameContains = "Vegetal"; // opcional: por nombre
    public string requiredTag = "";                     // opcional: por tag

    public event Action<GameObject> OnSnapped;

    private void Reset()
    {
        var c = GetComponent<Collider>();
        c.isTrigger = true;

        if (!snapPoint)
        {
            var p = new GameObject("SnapPoint").transform;
            p.SetParent(transform);
            p.SetPositionAndRotation(transform.position, transform.rotation);
            snapPoint = p;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        var go = other.attachedRigidbody ? other.attachedRigidbody.gameObject : other.gameObject;
        if (!PassesFilter(go)) return;
        
        if (snapPoint)
        {
            go.transform.position = snapPoint.position;
            if (alignRotation) go.transform.rotation = snapPoint.rotation;
        }

        if (zeroVelocity)
        {
            var rb = go.GetComponent<Rigidbody>();
            if (rb) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        }

        OnSnapped?.Invoke(go);
        SoundManager.Instance?.PlaySound("success");
    }

    private bool PassesFilter(GameObject go)
    {
        if (!string.IsNullOrEmpty(requiredTag) && !go.CompareTag(requiredTag)) return false;
        if (!string.IsNullOrEmpty(requiredRootNameContains) && !go.name.Contains(requiredRootNameContains)) return false;
        return true;
    }
}
