using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class EstanteBlockUI : MonoBehaviour
{
    [Header("Referencias UI")]
    public TMP_Text tmpSurtido;
    public TMP_Text tmpNumeroEstante;
    public TMP_Text tmpNombreEstante;
    public TMP_Text tmpCantidadArticulos;
    public TMP_Text tmpCantidadPiezas;

    public RawImage fondoEstante;
    public Texture imagenActivo;
    public Texture imagenBloqueado;

    private Button blockButton;
    private ProductoPicking productoInfo;
    private bool activo = false;

    private void Awake()
    {
        blockButton = GetComponent<Button>();
        blockButton.onClick.AddListener(OnBlockClicked);
    }

    public void Configurar(string surtido, string numeroEstante, string nombreEstante, ProductoPicking producto)
    {
        tmpSurtido.text = surtido;
        tmpNumeroEstante.text = numeroEstante;
        tmpNombreEstante.text = nombreEstante;

        tmpCantidadArticulos.text = $"0 de {producto.cantidadSolicitada} artículos surtidos";
        tmpCantidadPiezas.text = $"0 de {producto.cantidadSolicitada} piezas surtidas";

        productoInfo = producto;
    }
    private void OnBlockClicked()
    {
        if (!activo) return;

        PickingActivity.Instance.MostrarPanelProducto(productoInfo, this);
    }
    public void SetActivo(bool estado)
    {
        activo = estado;

        if (fondoEstante != null)
            fondoEstante.texture = activo ? imagenActivo : imagenBloqueado;

        if (blockButton != null)
            blockButton.interactable = activo;
    }

    public void MarcarComoSurtido()
    {
        tmpCantidadArticulos.text = $"{productoInfo.cantidadSolicitada} de {productoInfo.cantidadSolicitada} artículos surtidos";
        tmpCantidadPiezas.text = $"{productoInfo.cantidadSolicitada} de {productoInfo.cantidadSolicitada} piezas surtidas";

        activo = false;
        if (blockButton != null)
            blockButton.interactable = false;
    }
}
