using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ClaseoSpawnerUI : MonoBehaviour
{
    [System.Serializable]
    public struct DefPorMadurez
    {
        public MaturityLevel madurez;
        public GameObject prefabMundo;
        public Sprite icono;
        public string nombreUI; // opcional
    }
    [System.Serializable]
    public struct ProductoClaseo
    {
        public JabaTipo fruta;                 // Papaya / Platano / Ajitomate
        public List<DefPorMadurez> variantes;  // 3: No/Medio/Maduro
    }

    [Header("Scroll / Viewport")]
    public RectTransform viewport;
    public RectTransform content;
    public GameObject buttonTemplate; // DISABLED en escena

    [Header("Conveyor")]
    public float spawnStartX = 400f;
    public float leftClampX = -400f;
    public float itemSpacing = 140f;
    public float moveSpeed = 180f;
    public float arriveThreshold = 2f;
    public bool oldestOnLeft = true;

    [Header("Capacidad visible")]
    public int maxVisibleButtons = 6;
    public float approxCellWidth = 140f;

    [Header("Preview knobs → se copian al drag")]
    public Material previewMaterial;
    public bool clampYZ = true;
    public Vector2 yRange = new Vector2(-1f, 3f);
    public Vector2 zRange = new Vector2(-5f, 5f);
    public float alturaPreview = 0f;
    public float alturaPreviewExtra = 0f;

    [Header("Plano del ESTANTE (control total desde Spawner)")]
    public Transform planeAnchor;             // ← tu plano compartido en escena
    public Vector3 planeNormalLocal = Vector3.up;
    public Collider planeBounds;              // ← su collider (o el del bloque activo)
    public bool useFloorFallback = true;      // fuera del estante → raycast a suelo
    public float dragDepthOffset = 0.18f;
    public float followLerp = 0f;

    [Header("Productos (Papaya / Platano / Ajitomate)")]
    public List<ProductoClaseo> productos = new();

    // Estado
    readonly List<RectTransform> _items = new();
    readonly List<GameObject> _itemGOs = new();
    readonly Queue<GameObject> _pool = new();

    public System.Action<GameObject> OnButtonSpawned;

    void Awake()
    {
        if (!viewport && TryGetComponent(out ScrollRect sr))
            viewport = sr.viewport;

        if (buttonTemplate)
        {
            var le = buttonTemplate.GetComponent<LayoutElement>();
            if (le && le.preferredWidth > 0f) approxCellWidth = le.preferredWidth;
        }

        var hlg = content ? content.GetComponent<HorizontalLayoutGroup>() : null;
        if (hlg) hlg.enabled = false;
    }

    void Update()
    {
        for (int i = 0; i < _items.Count; i++)
        {
            var rt = _items[i]; if (!rt) continue;
            int targetIndex = oldestOnLeft ? i : (_items.Count - 1 - i);
            float targetX = leftClampX + targetIndex * itemSpacing;
            var pos = rt.anchoredPosition;

            if (Mathf.Abs(pos.x - targetX) > arriveThreshold)
            {
                pos.x = Mathf.MoveTowards(pos.x, targetX, moveSpeed * Time.deltaTime);
                rt.anchoredPosition = pos;
            }
        }

        for (int i = _items.Count - 1; i >= 0; i--)
        {
            if (_itemGOs[i] == null || _items[i] == null)
            { _itemGOs.RemoveAt(i); _items.RemoveAt(i); }
        }
    }

    GameObject GetPooledOrNew()
    {
        if (_pool.Count > 0)
        {
            var go = _pool.Dequeue();
            go.transform.SetParent(content, false);
            go.SetActive(true);
            return go;
        }
        return Instantiate(buttonTemplate, content);
    }

    public void ClearAll()
    {
        for (int i = _itemGOs.Count - 1; i >= 0; i--) Recycle(_itemGOs[i]);
        _itemGOs.Clear();
        _items.Clear();
    }

    public void Recycle(GameObject go)
    {
        if (!go) return;
        go.SetActive(false);
        go.transform.SetParent(transform, false);
        _pool.Enqueue(go);
        int idx = _itemGOs.IndexOf(go);
        if (idx >= 0) { _itemGOs.RemoveAt(idx); _items.RemoveAt(idx); }
    }

    // ====== Balanceado 8/8/8 ======
    private DefPorMadurez? GetVar(ProductoClaseo p, MaturityLevel m)
    {
        if (p.variantes == null) return null;
        foreach (var v in p.variantes) if (v.madurez == m) return v;
        return null;
    }
    void SpawnButton(JabaTipo fruta, DefPorMadurez varDef)
    {
        var go = GetPooledOrNew();
        go.SetActive(true);

        // payload
        var payload = go.GetComponent<ClaseoButtonPayload>() ?? go.AddComponent<ClaseoButtonPayload>();
        payload.fruta = fruta;
        payload.madurez = varDef.madurez;
        payload.prefabMundo = varDef.prefabMundo;
        payload.icono = varDef.icono;

        // view
        var view = go.GetComponent<ReproUIButtonView>();
        if (view)
        {
            if (view.icon) view.icon.sprite = varDef.icono;
            if (view.label) view.label.text = string.IsNullOrEmpty(varDef.nombreUI)
                                               ? $"{fruta} {varDef.madurez}"
                                               : varDef.nombreUI;
        }

        // drag (copia TODOS los knobs desde el Spawner)
        var drag = go.GetComponent<UIDragToWorldClaseo>() ?? go.AddComponent<UIDragToWorldClaseo>();
        drag.previewMaterial = previewMaterial;
        drag.clampYZ = clampYZ;
        drag.yRange = yRange;
        drag.zRange = zRange;
        drag.alturaPreview = alturaPreview;
        drag.alturaPreviewExtra = alturaPreviewExtra;

        // ⬅ estos dos siempre apuntan a TU plano compartido (que reposiciona la Activity)
        drag.planeAnchor = planeAnchor;
        drag.planeNormalLocal = planeNormalLocal;
        drag.planeBounds = planeBounds;

        drag.useFloorFallback = useFloorFallback;
        drag.dragDepthOffset = dragDepthOffset;
        drag.followLerp = followLerp;

        // posición inicial (derecha, tipo cinta)
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        var pos = rt.anchoredPosition;
        pos.x = spawnStartX;
        rt.anchoredPosition = pos;

        _items.Add(rt);
        _itemGOs.Add(go);

        OnButtonSpawned?.Invoke(go);
    }

    // === En ClaseoSpawnerUI ===
    // Llama a este en vez de SpawnExactBalanced(...) SOLO para el bloque Ajitomate
    // ===== BATCH determinístico con autocorrección =====
    public void SpawnExactBalancedDeterministic(JabaTipo fruta, int cantidadPorMadurez, bool shuffle = false, int? seed = null)
    {
        if (!buttonTemplate || !content || cantidadPorMadurez <= 0) return;
        if (buttonTemplate.activeSelf) buttonTemplate.SetActive(false);

        var def = productos.Find(x => x.fruta == fruta);
        if (def.variantes == null || def.variantes.Count == 0)
        {
            Debug.LogError($"[ClaseoSpawnerUI] No hay variantes configuradas para {fruta}");
            return;
        }

        // 1) Construye 8/8/8
        var bag = BuildBalancedList(def, cantidadPorMadurez);
        if (bag.Count == 0)
        {
            Debug.LogError($"[ClaseoSpawnerUI] Faltan variantes para {fruta} (No/Medio/Maduro).");
            return;
        }

        // 2) (opcional) mezcla
        if (shuffle) ShuffleInPlace(bag, seed);

        // 3) spawnea la bolsa en orden (ya mezclada si aplica)
        for (int i = 0; i < bag.Count; i++)
            SpawnButton(fruta, bag[i]);   // <- tu SpawnButton(fruta, DefPorMadurez v)

        // 4) autocorrección por si algún botón no quedó registrado (pools/plantillas)
        DefPorMadurez vNo = bag.Find(v => v.madurez == MaturityLevel.NoMaduro);
        DefPorMadurez vMedio = bag.Find(v => v.madurez == MaturityLevel.MedioMaduro);
        DefPorMadurez vMad = bag.Find(v => v.madurez == MaturityLevel.Maduro);
        EnsureCounts(fruta, cantidadPorMadurez, vNo, vMedio, vMad);

        Debug.Log($"[ClaseoSpawnerUI] Spawned {(shuffle ? "shuffle " : "")}{fruta} = {bag.Count} (objetivo {cantidadPorMadurez * 3}).");
    }

    // Mantén tu API legacy redirigida aquí si quieres
    public void SpawnExactBalanced(JabaTipo fruta, int cantidadPorMadurez, bool shuffle = true)
    {
        if (!buttonTemplate || !content || cantidadPorMadurez <= 0) return;
        if (buttonTemplate.activeSelf) buttonTemplate.SetActive(false);

        var def = productos.Find(x => x.fruta == fruta);
        if (def.variantes == null || def.variantes.Count == 0)
        {
            Debug.LogError($"[ClaseoSpawnerUI] No hay variantes configuradas para {fruta}");
            return;
        }

        // 8/8/8
        var bag = BuildBalancedList(def, cantidadPorMadurez);
        if (bag.Count == 0) return;

        // Mezcla si corresponde
        if (shuffle) ShuffleInPlace(bag);

        // Spawnea en el orden de la bolsa
        for (int i = 0; i < bag.Count; i++)
            SpawnButton(fruta, bag[i]);

        // Seguridad: corrige faltantes si algo falló
        EnsureCounts(fruta, cantidadPorMadurez,
            bag.Find(v => v.madurez == MaturityLevel.NoMaduro),
            bag.Find(v => v.madurez == MaturityLevel.MedioMaduro),
            bag.Find(v => v.madurez == MaturityLevel.Maduro));
    }

    private int CountButtons(JabaTipo fruta, MaturityLevel m)
    {
        int c = 0;

        // Recorremos los hijos del content y contamos payloads válidos
        int childN = content ? content.childCount : 0;
        for (int i = 0; i < childN; i++)
        {
            var t = content.GetChild(i);
            if (!t || t.gameObject == buttonTemplate) continue;

            var p = t.GetComponent<ClaseoButtonPayload>();
            if (p && p.fruta == fruta && p.madurez == m) c++;
        }

        // Por si algún botón quedó fuera del content (no debería):
        var extras = GetComponentsInChildren<ClaseoButtonPayload>(true);
        foreach (var p in extras)
        {
            if (!p) continue;
            if (p.transform.parent != content) continue; // ignoramos si no está en content
            if (p.gameObject == buttonTemplate) continue;

            if (p.fruta == fruta && p.madurez == m) c++;
        }

        return c;
    }

    private void EnsureCounts(JabaTipo fruta, int expectedPerLevel,
                          DefPorMadurez defNo, DefPorMadurez defMedio, DefPorMadurez defMad)
    {
        int haveNo = CountButtons(fruta, MaturityLevel.NoMaduro);
        int haveMedio = CountButtons(fruta, MaturityLevel.MedioMaduro);
        int haveMad = CountButtons(fruta, MaturityLevel.Maduro);

        int missNo = Mathf.Max(0, expectedPerLevel - haveNo);
        int missMedio = Mathf.Max(0, expectedPerLevel - haveMedio);
        int missMad = Mathf.Max(0, expectedPerLevel - haveMad);

        if (missNo > 0) { for (int i = 0; i < missNo; i++) SpawnButton(fruta, defNo); }
        if (missMedio > 0) { for (int i = 0; i < missMedio; i++) SpawnButton(fruta, defMedio); }
        if (missMad > 0) { for (int i = 0; i < missMad; i++) SpawnButton(fruta, defMad); }

        Debug.Log($"[ClaseoSpawnerUI] EnsureCounts {fruta} → No:{haveNo}+{missNo} Medio:{haveMedio}+{missMedio} Mad:{haveMad}+{missMad}");
    }

    [ContextMenu("SanityDump")]
    public void SanityDump()
    {
        if (!content) { Debug.LogWarning("[ClaseoSpawnerUI] SanityDump: no content"); return; }

        Debug.Log($"[ClaseoSpawnerUI] SanityDump: hijos en content = {content.childCount}");
        for (int i = 0; i < content.childCount; i++)
        {
            var t = content.GetChild(i);
            var p = t ? t.GetComponent<ClaseoButtonPayload>() : null;
            string info = p ? $"{p.fruta}/{p.madurez}" : "SIN payload";
            Debug.Log($"  - [{i}] {t.name}  :: {info}");
        }
    }

    static void ShuffleInPlace<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private List<DefPorMadurez> BuildBalancedList(ProductoClaseo def, int cantidadPorMadurez)
    {
        var bag = new List<DefPorMadurez>(cantidadPorMadurez * 3);

        DefPorMadurez? vNo = null, vMedio = null, vMad = null;
        foreach (var v in def.variantes)
        {
            switch (v.madurez)
            {
                case MaturityLevel.NoMaduro: vNo = v; break;
                case MaturityLevel.MedioMaduro: vMedio = v; break;
                case MaturityLevel.Maduro: vMad = v; break;
            }
        }

        if (vNo == null || vMedio == null || vMad == null) return bag;

        for (int i = 0; i < cantidadPorMadurez; i++) bag.Add(vNo.Value);
        for (int i = 0; i < cantidadPorMadurez; i++) bag.Add(vMedio.Value);
        for (int i = 0; i < cantidadPorMadurez; i++) bag.Add(vMad.Value);

        return bag;
    }
    // 👇 Fisher–Yates que NO toca el Random global de Unity
    static void ShuffleInPlace<T>(List<T> list, int? seed = null)
    {
        System.Random rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

  
}
