using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RuntimeCrateFiller : MonoBehaviour
{
    [Header("Volumen a rellenar (BoxCollider en el PREFAB)")]
    public BoxCollider volume;            // dejar null: se auto-descubre en Awake
    [Header("Parent de las instancias (opcional)")]
    public Transform instancesParent;     // dejar null: usa this.transform

    [Header("Parámetros de distribución")]
    [Tooltip("Radio aproximado usado para separar instancias.")]
    public float itemRadius = 0.05f;
    [Tooltip("Variación de escala aplicada por instancia (min..max).")]
    public Vector2 uniformScaleRange = new Vector2(0.95f, 1.05f);
    [Tooltip("Rotación aleatoria alrededor de Y.")]
    public bool randomYRotation = true;
    [Tooltip("Semilla para Random.InitState (0 = aleatorio real).")]
    public int seed = 0;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    void Awake()
    {
        if (instancesParent == null) instancesParent = transform;

        if (volume == null)
        {
            // Prioriza hijo llamado "Volume"
            var vols = GetComponentsInChildren<BoxCollider>(true);
            foreach (var v in vols)
            {
                if (v.name.ToLower().Contains("volume")) { volume = v; break; }
            }
            // Si no hay "Volume", toma el primero distinto a un collider de colisión visual
            if (volume == null && vols.Length > 0) volume = vols[0];
        }
    }

    public void Clear()
    {
        for (int i = _spawned.Count - 1; i >= 0; i--)
        {
            if (_spawned[i]) Destroy(_spawned[i]);
        }
        _spawned.Clear();
    }

    /// <summary>
    /// Atajo: limpia, setea prefab y rellena.
    /// </summary>
    public void SetPrefabAndFill(GameObject unitPrefab, int count)
    {
        Clear();
        if (unitPrefab == null || count <= 0) return;
        Fill(unitPrefab, count);
    }

    /// <summary>
    /// Rellena el volumen con 'count' instancias del prefab.
    /// </summary>
    public void Fill(GameObject unitPrefab, int count)
    {
        if (unitPrefab == null) { Debug.LogWarning("[RuntimeCrateFiller] Prefab nulo."); return; }
        if (volume == null) { Debug.LogWarning("[RuntimeCrateFiller] Asigna un BoxCollider de volumen en el prefab."); return; }

        if (seed != 0) Random.InitState(seed);

        // Bounds en espacio local del collider
        var localCenter = volume.center;
        var localSize = volume.size;

        int placed = 0;
        int attempts = 0;
        int maxAttempts = Mathf.Max(1, count) * 30;

        while (placed < count && attempts < maxAttempts)
        {
            attempts++;

            // punto aleatorio local (respetando un margen por el radio)
            var pLocal = new Vector3(
                Random.Range(-localSize.x * 0.5f + itemRadius, localSize.x * 0.5f - itemRadius),
                Random.Range(-localSize.y * 0.5f + itemRadius, localSize.y * 0.5f - itemRadius),
                Random.Range(-localSize.z * 0.5f + itemRadius, localSize.z * 0.5f - itemRadius)
            );
            // a espacio mundo
            Vector3 worldPos = volume.transform.TransformPoint(localCenter + pLocal);

            // chequeo simple de separación (distancia a instancias ya puestas)
            bool overlaps = false;
            for (int i = 0; i < _spawned.Count; i++)
            {
                var s = _spawned[i];
                if (!s) continue;
                if (Vector3.SqrMagnitude(s.transform.position - worldPos) < (itemRadius * itemRadius))
                {
                    overlaps = true; break;
                }
            }
            if (overlaps) continue;

            var go = Instantiate(unitPrefab, worldPos, Quaternion.identity, instancesParent);

            go.transform.localScale = unitPrefab.transform.localScale;

            if (randomYRotation)
                go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            _spawned.Add(go);
            placed++;
        }

        if (placed < count)
        {
            Debug.Log($"[RuntimeCrateFiller] Relleno parcial {placed}/{count}. Ajusta size o itemRadius.");
        }
    }
}
