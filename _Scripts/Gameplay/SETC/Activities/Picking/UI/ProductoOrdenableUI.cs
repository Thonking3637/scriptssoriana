using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ProductoOrdenableUI : MonoBehaviour
{
    public TextMeshProUGUI nombreTexto;
    public TextMeshProUGUI pesoTexto;
    public Image imagenProducto;

    [HideInInspector] public ProductoSETC producto;

    public void Configurar(ProductoSETC p)
    {
        producto = p;
        nombreTexto.text = p.nombre;
        pesoTexto.text = $"{p.peso} kg";
        imagenProducto.sprite = p.imagen;
    }
}
