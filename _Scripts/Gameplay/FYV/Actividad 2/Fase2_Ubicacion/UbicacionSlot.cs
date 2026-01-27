using UnityEngine;

[RequireComponent(typeof(Collider))]
public class UbicacionSlot : MonoBehaviour
{
    public UbicacionArea area;
    public Transform snapPoint;
    public bool ocupado = false;
    private GameObject _instancia;

    private void Reset()
    {
        if (!area) area = GetComponentInParent<UbicacionArea>();
    }

    public GameObject Place(GameObject prefab)
    {
        if (ocupado || !prefab) return null;
        Vector3 pos = snapPoint ? snapPoint.position : transform.position;
        Quaternion rot = snapPoint ? snapPoint.rotation : transform.rotation;
        _instancia = Instantiate(prefab, pos, rot);
        ocupado = true;
        return _instancia;
    }

    public void Clear()
    {
        if (_instancia) Destroy(_instancia);
        _instancia = null;
        ocupado = false;
    }
}
