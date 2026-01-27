using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BlockUI : MonoBehaviour
{
    public Image productoImage;
    public TMP_Text productoNombre;
    public TMP_Text cantidadText;

    [Header("Slot del Sticker")]
    public Image slotImage;

    [HideInInspector]
    public bool estaEnCarrito; // 🔥 se configura desde VerificacionMercanciaActivity

    private bool tieneSticker = false;
    private bool esCheckSticker = false;

    private void Awake()
    {
        productoImage = transform.Find("Image").GetComponent<Image>();
        productoNombre = transform.Find("TMP_TextNombre").GetComponent<TMP_Text>();
        cantidadText = transform.Find("TMP_TextCantidad").GetComponent<TMP_Text>();
        slotImage = transform.Find("SlotDrop/SlotImage").GetComponent<Image>();

        Button botonBorrar = transform.Find("Button").GetComponent<Button>();
        botonBorrar.onClick.AddListener(ResetSticker);
    }

    public void SetData(ProductoSETC producto, int cantidad)
    {
        productoImage.sprite = producto.imagen;
        productoNombre.text = producto.nombre;
        cantidadText.text = "x" + cantidad;
    }

    public void SetSticker(Sprite sprite, bool esCheck)
    {
        slotImage.sprite = sprite;
        slotImage.color = Color.white;
        tieneSticker = true;
        esCheckSticker = esCheck;

        VerificacionMercanciaActivity.Instance.RevisarSiTodosConSticker();
    }
    public bool TieneSticker()
    {
        return tieneSticker;
    }


    public void ResetSticker()
    {
        slotImage.sprite = null;
        slotImage.color = new Color(1, 1, 1, 0); // transparente
        tieneSticker = false;
    }

    public bool EsCorrecto()
    {
        if (!tieneSticker) return false;
        return estaEnCarrito == esCheckSticker;
    }
}
