using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UbicacionCarouselUI : MonoBehaviour
{
    [System.Serializable]
    public class JabaCard
    {
        public string nombre;
        public JabaTipo tipo;       // Huevos / Frutas / Verduras
        public GameObject prefab;   // prefab de jaba ya llena (modelado específico)
        public Button button;
    }

    [Header("Carrusel de jabas (Fase 2)")]
    public JabaCard[] jabaCards;

    [Header("Drag Preview")]
    public Material previewMaterial;
    public float alturaPreview = 0.3f;

    [Header("Preview")]
    public float fixedXWorld = 0f;
    public bool clampYZ = true;
    public Vector2 yRange = new Vector2(-1f, 3f);
    public Vector2 zRange = new Vector2(-5f, 5f);
    public float alturaPreviewExtra = 0.0f;

    [Header("Uso de tarjetas")]
    public bool singleUseGlobal = true;

    private HashSet<GameObject> usados = new HashSet<GameObject>();

    public bool interactable = true;

    private void Start()
    {
        foreach (var c in jabaCards)
        {
            if (!c.button || c.prefab == null) continue;

            var drag = c.button.gameObject.AddComponent<UIDragToWorldUbicacion>();
            drag.prefabMundo = c.prefab;
            drag.tipoJaba = c.tipo;
            drag.previewMaterial = previewMaterial;
            drag.alturaPreview = alturaPreview;

            drag.constrainToFixedX = true;
            drag.fixedXWorld = fixedXWorld;
            drag.clampYZ = clampYZ;
            drag.yRange = yRange;
            drag.zRange = zRange;
            drag.alturaPreview = alturaPreviewExtra;

            // Puedes limitar con layers si lo deseas:
            // drag.dropMaskUbicacion = LayerMask.GetMask("Ubicacion");
        }

        RefreshAvailability();
    }

    private void Update()
    {
        foreach (var c in jabaCards)
        {
            if (c.button) c.button.interactable = interactable && (!singleUseGlobal || !usados.Contains(c.prefab));
        }
    }

    public void MarkAsUsedByPrefab(GameObject prefab)
    {
        if (!singleUseGlobal || prefab == null) return;

        usados.Add(prefab);

        foreach (var c in jabaCards)
            if (c.prefab == prefab && c.button)
                c.button.gameObject.SetActive(false);

        RefreshAvailability();
    }

    public int RemainingAvailable()
    {
        int total = 0;
        foreach (var c in jabaCards)
            if (c.prefab != null && !usados.Contains(c.prefab)) total++;
        return total;
    }

    public void SetGlobalInteractable(bool value)
    {
        interactable = value;
        RefreshAvailability();
    }

    public void RefreshAvailability()
    {
        foreach (var c in jabaCards)
        {
            if (c.button == null || c.prefab == null) continue;
            bool disponible = !singleUseGlobal || !usados.Contains(c.prefab);
            c.button.interactable = interactable && disponible;
            c.button.gameObject.SetActive(disponible);
        }
    }

    public int TotalTargets()
    {
        int total = 0;
        foreach (var c in jabaCards)
            if (c.prefab != null) total++;
        return total;
    }

    public void ResetUsados()
    {
        usados.Clear();

        foreach (var c in jabaCards)
        {
            if (c.button == null || c.prefab == null) continue;
            c.button.gameObject.SetActive(true);
            c.button.interactable = true;
        }

        RefreshAvailability();
    }
}
