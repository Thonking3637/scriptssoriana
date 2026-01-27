using UnityEngine;

public class RevitaResettable : MonoBehaviour
{
    [Header("Home (si no asignas, toma la posición/rotación/escala inicial)")]
    public Transform home;

    [Header("Opcional: forzar parent original al reset")]
    public bool restoreParent = true;

    Transform _cachedParent;
    Vector3 _pos0, _scale0;
    Quaternion _rot0;

    void Awake()
    {
        _cachedParent = transform.parent;
        if (home == null)
        {
            _pos0 = transform.position;
            _rot0 = transform.rotation;
            _scale0 = transform.localScale;
        }
    }

    public void ResetNow()
    {
        if (restoreParent && _cachedParent != null)
            transform.SetParent(_cachedParent, true);

        if (home)
        {
            transform.SetPositionAndRotation(home.position, home.rotation);
            transform.localScale = home.localScale;
        }
        else
        {
            transform.SetPositionAndRotation(_pos0, _rot0);
            transform.localScale = _scale0;
        }
    }
}
