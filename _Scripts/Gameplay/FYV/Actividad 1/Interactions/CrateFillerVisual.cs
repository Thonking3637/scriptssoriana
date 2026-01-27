using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class CrateFillerVisual : MonoBehaviour
{
    [Header("Volumen de la caja")]
    public BoxCollider crateVolume;

    [Header("Prefab a llenar")]
    public GameObject prefab;

    [Header("Cantidad y espaciamiento")]
    [Min(0)] public int count = 40;
    [Tooltip("Radio aproximado entre instancias para evitar solaparse visualmente.")]
    public float itemRadius = 0.05f;

    [Header("Variaciones visuales")]
    [Range(0.8f, 1.2f)] public float scaleJitter = 1.0f;
    public bool keepUpAxis = true;
    [Range(0f, 30f)] public float tiltJitter = 5f;

    [Header("Random Seed")]
    public int seed = 1234;

    [Header("Parenting y limpieza")]
    public bool parentUnderThis = true;
    public bool clearBeforeFill = true;

    [HideInInspector] public List<GameObject> spawned = new();

#if UNITY_EDITOR
    void OnValidate()
    {
        if (crateVolume == null)
            crateVolume = GetComponent<BoxCollider>();
    }
#endif

    public void FillBox()
    {
        if (crateVolume == null || prefab == null)
        {
            Debug.LogWarning("Asigna crateVolume (BoxCollider) y prefab.");
            return;
        }

        if (clearBeforeFill) ClearBox();

        Random.InitState(seed);

        Bounds bounds = new Bounds(crateVolume.center, crateVolume.size);
        float padding = itemRadius * 0.5f;
        bounds.Expand(new Vector3(-padding, -padding, -padding));

        int placed = 0;
        int attempts = 0;
        int maxAttempts = Mathf.Max(1, count) * 25;

        while (placed < count && attempts < maxAttempts)
        {
            attempts++;

            Vector3 localPos = new Vector3(
                Random.Range(-bounds.extents.x + itemRadius, bounds.extents.x - itemRadius),
                Random.Range(-bounds.extents.y + itemRadius, bounds.extents.y - itemRadius),
                Random.Range(-bounds.extents.z + itemRadius, bounds.extents.z - itemRadius)
            );

            Vector3 worldPos = crateVolume.transform.TransformPoint(bounds.center + localPos);

#if UNITY_EDITOR
            GameObject go;
            var prefabAsset = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(prefab);
            if (prefabAsset != null)
                go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefabAsset);
            else
                go = Instantiate(prefab);
#else
GameObject go = Instantiate(prefab);
#endif

            // 👇 evitar heredar escala del parent
            if (parentUnderThis) go.transform.SetParent(transform, false);

            // POSICIÓN
            go.transform.position = worldPos;
            go.transform.rotation = RandomRotation();

            // 👌 ESCALA ORIGINAL DEL PREFAB
            go.transform.localScale = prefab.transform.localScale;

            // (opcional) escala jitter
            // if (!Mathf.Approximately(scaleJitter, 1f))
            // {
            //     float factor = Random.Range(1f / scaleJitter, scaleJitter);
            //     go.transform.localScale *= factor;
            // }

            spawned.Add(go);
        }

        if (placed < count)
            Debug.Log($"Relleno parcial: {spawned.Count}/{count}. Ajusta tamaño o itemRadius.");
    }

    public void ClearBox()
    {
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            var go = spawned[i];
            if (!go) continue;
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(go);
            else Destroy(go);
#else
            Destroy(go);
#endif
        }
        spawned.Clear();
    }

    public void AutoFitFromChildrenRenderers(float padding = 0.0f)
    {
        if (crateVolume == null) return;
        var rends = GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return;

        var b = new Bounds(transform.position, Vector3.zero);
        foreach (var r in rends)
            if (r && r.enabled) b.Encapsulate(r.bounds);

        // Pasar a espacio local del BoxCollider
        var t = crateVolume.transform;
        var localCenter = t.InverseTransformPoint(b.center);
        var localSize = Vector3.Scale(b.size, new Vector3(
            1f / t.lossyScale.x,
            1f / t.lossyScale.y,
            1f / t.lossyScale.z
        ));

        localSize += Vector3.one * padding;
        crateVolume.center = localCenter;
        crateVolume.size = localSize;
    }

    private Quaternion RandomRotation()
    {
        if (!keepUpAxis) return Random.rotationUniform;
        float yaw = Random.Range(0f, 360f);
        float pitch = Random.Range(-tiltJitter, tiltJitter);
        float roll = Random.Range(-tiltJitter, tiltJitter);
        return Quaternion.Euler(pitch, yaw, roll);
    }

    private void ApplyScale(GameObject go)
    {
        if (Mathf.Approximately(scaleJitter, 1f)) return;
        float factor = Random.Range(1f / scaleJitter, scaleJitter);
        go.transform.localScale *= factor;
    }

    void OnDrawGizmosSelected()
    {
        if (!crateVolume) return;
        Gizmos.color = Color.green;
        var m = crateVolume.transform.localToWorldMatrix;
        Gizmos.matrix = m;
        Gizmos.DrawWireCube(crateVolume.center, crateVolume.size);
    }
}
