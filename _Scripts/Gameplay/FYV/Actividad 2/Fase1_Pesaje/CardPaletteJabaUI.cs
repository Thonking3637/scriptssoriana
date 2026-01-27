using UnityEngine;
using UnityEngine.UI;

public class CardPaletteJabaUI : MonoBehaviour
{
    [System.Serializable]
    public class JabaCard
    {
        public string nombre;
        public JabaTipo tipo;
        public GameObject prefabJaba;
        public Button button;
    }

    [Header("Tarjetas de jaba")]
    public JabaCard[] jabaCards;

    [Header("Drag Preview")]
    public Material previewMaterial;
    public float alturaPreview = 0.3f;

    public bool interactable = true;

    [Header("Preview Jabas")]
    public float fixedXWorld_Jabas = 0f;
    public bool clampYZ_Jabas = true;
    public Vector2 yRange_Jabas = new Vector2(-1f, 3f);
    public Vector2 zRange_Jabas = new Vector2(-5f, 5f);
    public float alturaPreview_Jabas = 0.0f;

    private void Start()
    {
        foreach (var c in jabaCards)
        {
            if (!c.button) continue;

            var drag = c.button.gameObject.AddComponent<UIDragToWorld>();
            drag.prefabMundo = c.prefabJaba;
            drag.esJaba = true;
            drag.tipoJaba = c.tipo;
            drag.previewMaterial = previewMaterial;
            drag.alturaPreview = alturaPreview;

            drag.constrainToFixedX = true;
            drag.fixedXWorld = fixedXWorld_Jabas;
            drag.clampYZ = clampYZ_Jabas;
            drag.yRange = yRange_Jabas;
            drag.zRange = zRange_Jabas;
            drag.alturaPreview = alturaPreview_Jabas;

            drag.dropMaskJaba = LayerMask.GetMask("BalanzaSlot");  // JABA solo cae en Slot
            drag.dropMaskProducto = ~0;
        }
    }

    private void Update()
    {
        foreach (var c in jabaCards)
        {
            if (c.button) c.button.interactable = interactable;
        }
    }
}
