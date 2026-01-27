using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ProductCarouselUI : MonoBehaviour
{
    [System.Serializable]
    public class ProductCard
    {
        public string nombre;
        public ProductoCatalogSO producto;
        public Button button;
    }

    [Header("Carrusel de productos")]
    public ProductCard[] productCards;

    [Header("Drag Preview")]
    public Material previewMaterial;
    public float alturaPreview = 0.3f;

    [Header("Preview Productos")]
    public float fixedXWorld_Productos = 0f;   // ← define el X de productos
    public bool clampYZ_Productos = true;
    public Vector2 yRange_Productos = new Vector2(-1f, 3f);
    public Vector2 zRange_Productos = new Vector2(-5f, 5f);
    public float alturaPreview_Productos = 0.0f;

    [Header("Uso de productos")]
    public bool singleUseGlobal = true;

    private HashSet<ProductoCatalogSO> usados = new HashSet<ProductoCatalogSO>();

    public bool interactable = true;

    private void Start()
    {
        foreach (var c in productCards)
        {
            if (!c.button || c.producto == null) continue;

            var drag = c.button.gameObject.AddComponent<UIDragToWorld>();

            GameObject prefab = null;

            if (c.producto.prefabUnidad != null)
            {
                prefab = c.producto.prefabUnidad;
            }
            else if (c.producto.prefabLleno != null)
            {
                prefab = c.producto.prefabLleno;
            }
            drag.prefabMundo = prefab;
            drag.esJaba = false;
            drag.producto = c.producto;
            drag.previewMaterial = previewMaterial;
            drag.alturaPreview = alturaPreview;

            drag.constrainToFixedX = true;
            drag.fixedXWorld = fixedXWorld_Productos;
            drag.clampYZ = clampYZ_Productos;
            drag.yRange = yRange_Productos;
            drag.zRange = zRange_Productos;
            drag.alturaPreview = alturaPreview_Productos;

            drag.dropMaskProducto = LayerMask.GetMask("Jaba");     // PRODUCTO solo mira Jabas
            drag.dropMaskJaba = ~0; // no aplica aquí

            Debug.Log(
           $"[Carrusel] Card:{c.nombre} prod:{c.producto?.name} " +
           $"unidad:{c.producto?.prefabUnidad} lleno:{c.producto?.prefabLleno} " +
           $"→ prefabMundo asignado:{drag.prefabMundo}",
           c.button
            );
        }

        
    }

    private void Update()
    {
        foreach (var c in productCards)
        {
            if (c.button) c.button.interactable = interactable;
        }
    }

    public void MarkAsUsed(ProductoCatalogSO p)
    {
        if (!singleUseGlobal || p == null) return;
        usados.Add(p);

        foreach (var c in productCards)
            if (c.producto == p && c.button)
                c.button.gameObject.SetActive(false);

        RefreshAvailability();
    }

    public int RemainingAvailable()
    {
        int total = 0;
        foreach (var c in productCards)
            if (c.producto != null && !usados.Contains(c.producto)) total++;
        return total;
    }

    public void SetGlobalInteractable(bool value)
    {
        interactable = value;
        RefreshAvailability();
    }

    public void RefreshAvailability()
    {
        foreach (var c in productCards)
        {
            if (c.button == null || c.producto == null) continue;
            bool disponible = !singleUseGlobal || !usados.Contains(c.producto);
            c.button.interactable = interactable && disponible;
        }
    }

    public int TotalTargets()
    {
        int total = 0;
        foreach (var c in productCards)
            if (c.producto != null) total++;
        return total;
    }

    public void ResetUsados()
    {
        usados.Clear();

        foreach (var c in productCards)
        {
            if (c.button == null || c.producto == null) continue;
            c.button.gameObject.SetActive(true);
            c.button.interactable = true;
        }

        RefreshAvailability();
    }
}
