using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ReproSpawnerUI : MonoBehaviour
{
    [System.Serializable]
    public struct ProductDef
    {
        public string nombre;
        public JabaTipo tipo;

        [Header("Prefab legacy (compat)")]
        public GameObject prefabMundo;

        [Header("Variantes (opcional)")]
        public List<GameObject> prefabsMundoVariantes;

        public Sprite icono;

        public bool HasAnyPrefab()
        {
            if (prefabMundo != null) return true;
            if (prefabsMundoVariantes == null) return false;
            for (int i = 0; i < prefabsMundoVariantes.Count; i++)
                if (prefabsMundoVariantes[i] != null) return true;
            return false;
        }

        public bool ContainsPrefab(GameObject p)
        {
            if (p == null) return false;
            if (prefabMundo == p) return true;

            if (prefabsMundoVariantes != null)
            {
                for (int i = 0; i < prefabsMundoVariantes.Count; i++)
                    if (prefabsMundoVariantes[i] == p) return true;
            }
            return false;
        }
    }

    [Header("Scroll / Viewport")]
    public RectTransform viewport;
    public RectTransform content;
    public GameObject buttonTemplate;

    [Header("Conveyor")]
    public float spawnStartX = 400f;
    public float leftClampX = -400f;
    public float itemSpacing = 140f;
    public float moveSpeed = 180f;
    public float arriveThreshold = 2f;

    [Header("Capacidad visible")]
    public int maxVisibleButtons = 6;
    public float approxCellWidth = 140f;

    [Header("Productos (nueva API)")]
    public List<ProductDef> productos = new List<ProductDef>();

    // ===== Compat API antigua =====
    [Header("Compatibilidad con API antigua (opcional)")]
    public List<GameObject> prefabsProducto = new List<GameObject>();
    public List<JabaTipo> tiposProducto = new List<JabaTipo>();

    [Header("Uso de tarjetas")]
    public bool singleUseGlobal = false;
    public bool interactable = true;

    [Header("Preview knobs (copiados a UIDragToWorldRepro)")]
    public Material previewMaterial;
    public float fixedXWorld = 0f;
    public bool clampYZ = true;
    public Vector2 yRange = new Vector2(-1f, 3f);
    public Vector2 zRange = new Vector2(-5f, 5f);
    public float alturaPreview = 0.3f;
    public float alturaPreviewExtra = 0.0f;

    [Header("Spawn timing")]
    public float spawnIntervalStart = 1.2f;
    public float spawnIntervalMin = 0.45f;
    public float spawnAcceleration = 0.02f;

    [Header("Auto-spawn")]
    public bool enableAutoSpawn = true;
    public bool oldestOnLeft = true;

    // ===== Estado =====
    float _nextSpawn;
    float _interval;
    readonly List<RectTransform> _items = new();
    readonly List<GameObject> _itemGOs = new();
    readonly HashSet<int> _usadosGlobal = new();
    List<int> _bag;

    // Pool
    readonly Queue<GameObject> _pool = new();

    // Whitelist de tipos
    HashSet<JabaTipo> _whitelist;

    // ===== Spawn Budget =====
    bool _useSpawnBudget = false;
    int _spawnBudget = 0;
    int _spawnedSoFar = 0;

    [Header("Hook opcional al spawnear (para Claseo)")]
    public System.Action<GameObject> OnButtonSpawned;

    void Awake()
    {
        if (!viewport && TryGetComponent(out ScrollRect sr))
            viewport = sr.viewport;

        _interval = spawnIntervalStart;
        _nextSpawn = Time.time + _interval;

        UIDragToWorldRepro.OnDropUIButton += OnDropUIButton;

        if (buttonTemplate)
        {
            var le = buttonTemplate.GetComponent<LayoutElement>();
            if (le && le.preferredWidth > 0f)
                approxCellWidth = le.preferredWidth;
        }

        var hlg = content ? content.GetComponent<HorizontalLayoutGroup>() : null;
        if (hlg) hlg.enabled = false;

        // ✅ Compat: SOLO si productos está vacío, arma desde API antigua
        if (productos.Count == 0 && prefabsProducto.Count > 0 && prefabsProducto.Count == tiposProducto.Count)
        {
            for (int i = 0; i < prefabsProducto.Count; i++)
            {
                productos.Add(new ProductDef
                {
                    nombre = tiposProducto[i].ToString(),
                    tipo = tiposProducto[i],
                    prefabMundo = prefabsProducto[i],
                    prefabsMundoVariantes = null,
                    icono = null
                });
            }
        }

        RefillBag();
    }

    void OnDestroy()
    {
        UIDragToWorldRepro.OnDropUIButton -= OnDropUIButton;
    }

    void Update()
    {
        // Mover items a su target
        for (int i = 0; i < _items.Count; i++)
        {
            var rt = _items[i];
            if (!rt) continue;

            int targetIndex = oldestOnLeft ? i : (_items.Count - 1 - i);
            float targetX = leftClampX + targetIndex * itemSpacing;

            Vector2 pos = rt.anchoredPosition;
            if (Mathf.Abs(pos.x - targetX) > arriveThreshold)
            {
                pos.x = Mathf.MoveTowards(pos.x, targetX, moveSpeed * Time.deltaTime);
                rt.anchoredPosition = pos;
            }
        }

        // Limpieza de nulos
        for (int i = _items.Count - 1; i >= 0; i--)
        {
            if (_itemGOs[i] == null || _items[i] == null)
            {
                _items.RemoveAt(i);
                _itemGOs.RemoveAt(i);
            }
        }

        if (!enableAutoSpawn) return;
        if (IsViewportFull()) return;
        if (_useSpawnBudget && _spawnedSoFar >= _spawnBudget) return;

        if (Time.time >= _nextSpawn)
        {
            if (TrySpawnNextFromBag())
            {
                _spawnedSoFar++;
                _interval = Mathf.Max(spawnIntervalMin, _interval - spawnAcceleration);
            }
            _nextSpawn = Time.time + _interval;
        }
    }

    bool IsViewportFull()
    {
        if (!viewport) return true;
        float viewportWidth = viewport.rect.width;

        int capacityByWidth = Mathf.Max(1, Mathf.FloorToInt(viewportWidth / Mathf.Max(1f, approxCellWidth)));
        int cap = Mathf.Min(maxVisibleButtons, capacityByWidth);

        return _items.Count >= cap;
    }

    void RefillBag()
    {
        _bag = new List<int>();
        for (int i = 0; i < productos.Count; i++)
        {
            if (!productos[i].HasAnyPrefab()) continue;
            if (singleUseGlobal && _usadosGlobal.Contains(i)) continue;
            if (_whitelist != null && _whitelist.Count > 0 && !_whitelist.Contains(productos[i].tipo)) continue;
            _bag.Add(i);
        }

        for (int i = _bag.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (_bag[i], _bag[j]) = (_bag[j], _bag[i]);
        }
    }

    bool TrySpawnNextFromBag()
    {
        if (_bag == null || _bag.Count == 0)
        {
            RefillBag();
            if (_bag.Count == 0) return false;
        }

        int idx = _bag[0];
        _bag.RemoveAt(0);
        SpawnButton(productos[idx], idx);
        return true;
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

    public void Recycle(GameObject go)
    {
        if (!go) return;
        go.SetActive(false);
        go.transform.SetParent(transform, false);

        _pool.Enqueue(go);

        int idx = _itemGOs.IndexOf(go);
        if (idx >= 0)
        {
            _itemGOs.RemoveAt(idx);
            _items.RemoveAt(idx);
        }
    }

    void SpawnButton(ProductDef def, int productIndex)
    {
        if (!buttonTemplate || !content) return;

        var go = GetPooledOrNew();
        go.SetActive(true);

        var btn = go.GetComponent<Button>();
        if (btn) btn.interactable = interactable;

        var view = go.GetComponent<ReproUIButtonView>();
        if (view)
        {
            if (view.icon) view.icon.sprite = def.icono;
            if (view.label) view.label.text = string.IsNullOrEmpty(def.nombre) ? def.tipo.ToString() : def.nombre;
        }

        var drag = go.GetComponent<UIDragToWorldRepro>();
        if (!drag) drag = go.AddComponent<UIDragToWorldRepro>();

        // ✅ Compat: el legacy sigue existiendo (por si no llenas variantes)
        drag.prefabMundo = def.prefabMundo;
        drag.SetPrefabVariants(def.prefabsMundoVariantes);

        drag.tipoJaba = def.tipo;
        drag.previewMaterial = previewMaterial;
        drag.constrainToFixedX = true;
        drag.fixedXWorld = fixedXWorld;
        drag.clampYZ = clampYZ;
        drag.yRange = yRange;
        drag.zRange = zRange;
        drag.alturaPreview = alturaPreview;
        drag.alturaPreviewExtra = alturaPreviewExtra;

        var handle = go.GetComponent<ReproUIButtonHandle>();
        if (!handle) handle = go.AddComponent<ReproUIButtonHandle>();
        handle.owner = this;

        var rt = go.GetComponent<RectTransform>();
        if (!rt) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        var pos = rt.anchoredPosition;
        pos.x = spawnStartX;
        rt.anchoredPosition = pos;

        _items.Add(rt);
        _itemGOs.Add(go);

        OnButtonSpawned?.Invoke(go);
    }

    void OnDropUIButton(JabaTipo tipo, GameObject prefabElegido, bool valido)
    {
        if (!valido || !singleUseGlobal) return;

        // ✅ Ahora buscamos por: tipo + “el ProductDef contiene este prefab (sea legacy o variante)”
        int idx = productos.FindIndex(p => p.tipo == tipo && p.ContainsPrefab(prefabElegido));
        if (idx < 0) return;

        if (_usadosGlobal.Add(idx))
        {
            for (int i = _itemGOs.Count - 1; i >= 0; i--)
            {
                var go = _itemGOs[i];
                if (!go) { _itemGOs.RemoveAt(i); _items.RemoveAt(i); continue; }

                var d = go.GetComponent<UIDragToWorldRepro>();
                if (d && d.tipoJaba.Equals(tipo) && d.ContainsPrefabVariant(prefabElegido))
                    Recycle(go);
            }
        }
    }

    // ===== API pública =====
    public void ClearAll()
    {
        for (int i = _itemGOs.Count - 1; i >= 0; i--)
            Recycle(_itemGOs[i]);

        _itemGOs.Clear();
        _items.Clear();

        _interval = spawnIntervalStart;
        _nextSpawn = Time.time + _interval;
    }

    public void ResetAllAndUsed()
    {
        ClearAll();
        _usadosGlobal.Clear();
        _spawnedSoFar = 0;
        RefillBag();
    }

    public void SetGlobalInteractable(bool value)
    {
        interactable = value;
        foreach (var go in _itemGOs)
        {
            var btn = go ? go.GetComponent<Button>() : null;
            if (btn) btn.interactable = value;
        }
    }

    // Whitelist
    public void SetWhitelist(params JabaTipo[] allowed)
    {
        if (allowed == null || allowed.Length == 0) _whitelist = null;
        else _whitelist = new HashSet<JabaTipo>(allowed);
        RefillBag();
    }

    public void ClearWhitelist()
    {
        _whitelist = null;
        RefillBag();
    }

    // ===== Spawn Budget API =====
    public void SetSpawnBudget(int count)
    {
        _useSpawnBudget = count > 0;
        _spawnBudget = Mathf.Max(0, count);
        _spawnedSoFar = 0;
    }

    public void ClearSpawnBudget()
    {
        _useSpawnBudget = false;
        _spawnBudget = 0;
        _spawnedSoFar = 0;
    }

    // (Mantengo SpawnSingle/SpawnSingleByTipo si los usas en otros lados; si no los usas, los puedes borrar)
    public void SpawnSingle(JabaTipo tipo, GameObject prefab)
    {
        int i = productos.FindIndex(p => p.tipo == tipo && p.ContainsPrefab(prefab));
        if (i >= 0) { SpawnButton(productos[i], i); return; }

        int j = productos.FindIndex(p => p.tipo == tipo);
        if (j >= 0)
        {
            var def = productos[j];
            def.prefabMundo = prefab;
            def.prefabsMundoVariantes = null;
            SpawnButton(def, -1);
            return;
        }

        var fallback = new ProductDef
        {
            nombre = tipo.ToString(),
            tipo = tipo,
            prefabMundo = prefab,
            prefabsMundoVariantes = null,
            icono = null
        };
        SpawnButton(fallback, -1);
    }

    public void SpawnSingleByTipo(JabaTipo tipo)
    {
        int idx = productos.FindIndex(p => p.tipo == tipo);
        if (idx >= 0) { SpawnButton(productos[idx], idx); return; }

        int idxLegacy = tiposProducto.FindIndex(t => t == tipo);
        if (idxLegacy >= 0 && idxLegacy < prefabsProducto.Count)
            SpawnSingle(tipo, prefabsProducto[idxLegacy]);
    }
}
