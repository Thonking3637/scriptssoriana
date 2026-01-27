using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BalanzaSlot : MonoBehaviour
{
    [Header("Spawn")]
    public Transform spawnPoint;

    private GameObject _jabaGO;
    private JabaMermaView _jabaView;

    public bool Ocupado => _jabaGO != null;
    public JabaMermaView GetJabaView() => _jabaView;

    public JabaMermaView SpawnOrReplaceJaba(GameObject jabaPrefab, JabaTipo tipo)
    {
        Clear();

        Vector3 pos = spawnPoint ? spawnPoint.position : transform.position;
        Quaternion rot = spawnPoint ? spawnPoint.rotation : transform.rotation;

        _jabaGO = Instantiate(jabaPrefab, pos, rot);
        _jabaView = _jabaGO.GetComponent<JabaMermaView>();
        if (_jabaView == null) _jabaView = _jabaGO.AddComponent<JabaMermaView>();

        _jabaView.SetJabaTipo(tipo);
        _jabaView.ClearContenido();

        return _jabaView;
    }

    public void Clear()
    {
        if (_jabaGO != null)
        {
            Destroy(_jabaGO);
            _jabaGO = null;
            _jabaView = null;
        }
    }
}
