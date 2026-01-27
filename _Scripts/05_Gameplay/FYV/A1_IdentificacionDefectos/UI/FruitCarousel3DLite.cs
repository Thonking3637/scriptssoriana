using UnityEngine;
using System.Collections.Generic;

public class FruitCarousel3DLite: MonoBehaviour
{
    [Header("Vista destino (FruitView en escena)")]
    public FruitView view;

    [Header("Escala de visualización")]
    public float scale = 1f;

    private List<FruitData> items;
    private int index;

    public System.Action<FruitData, int> OnShowItem;

    // ==================================================
    // Carga de lista (lote actual)
    // ==================================================
    public void Load(List<FruitData> list)
    {
        if (view == null)
        {
            Debug.LogError("[FruitCarousel] ❌ 'view' no asignado en el inspector.");
            return;
        }
        if (list == null || list.Count == 0)
        {
            Debug.LogError("[FruitCarousel] ❌ Lista de frutas vacía o nula.");
            return;
        }

        items = list;
        index = 0;
        Show(0, true);
    }

    // ==================================================
    // Navegación
    // ==================================================
    public void Next() => Show(index + 1, false);
    public void Prev() => Show(index - 1, false);

    public FruitData Current() => (items == null || items.Count == 0) ? null : items[index];
    public int CurrentIndex() => index;

    // ==================================================
    // Mostrar fruta según índice
    // ==================================================
    private void Show(int i, bool instant)
    {
        if (view == null || items == null || items.Count == 0)
            return;

        // Wrap del índice
        if (i < 0) i = items.Count - 1;
        else if (i >= items.Count) i = 0;
        index = i;

        var d = items[index];
        if (d == null)
        {
            Debug.LogWarning("[FruitCarousel] ⚠️ FruitData nulo en índice " + index);
            return;
        }

        // Elegir prefab según estado
        var src = d.esDefectuoso ? d.prefabMalo : d.prefabBueno;
        if (src == null)
        {
            Debug.LogError($"[FruitCarousel] ❌ Prefab {(d.esDefectuoso ? "malo" : "bueno")} no asignado en '{d.nombre}'.");
            return;
        }

        // Obtener mesh/materials del prefab
        var mf = src.GetComponentInChildren<MeshFilter>();
        var mr = src.GetComponentInChildren<MeshRenderer>();
        if (mf == null || mr == null)
        {
            Debug.LogError($"[FruitCarousel] ❌ Prefab '{src.name}' sin MeshFilter/MeshRenderer en hijos.");
            return;
        }

        Mesh mesh = mf.sharedMesh;
        Material[] mats = mr.sharedMaterials;

        if (mesh == null || mats == null || mats.Length == 0)
        {
            Debug.LogError($"[FruitCarousel] ⚠️ Prefab '{src.name}' tiene mesh o materiales vacíos.");
            return;
        }

        // Aplicar malla y materiales al FruitView
        view.Apply(mesh, mats, Vector3.one * scale);


        // Asegurar que se pueda rotar táctilmente
        // Notificar al Activity para actualizar textos/highlight
        OnShowItem?.Invoke(d, index);

        // Log opcional para depurar
        // Debug.Log($"[FruitCarousel] ✅ Mostrando '{d.nombre}' (idx={index}) | Defectuoso={d.esDefectuoso}");
    }
}
