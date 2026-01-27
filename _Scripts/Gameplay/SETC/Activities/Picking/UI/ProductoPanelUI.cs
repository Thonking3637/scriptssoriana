using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;

public class ProductoPanelUI : MonoBehaviour
{
    public Image imagenProducto;
    public TMP_Text nombreProducto;
    public TMP_Text codigoProducto;
    public TMP_Text cantidadSolicitada;
    public TMP_Text precio;

    public TMP_InputField inputCodigoSurtido;
    public TMP_Dropdown dropdownCantidad;

    public Button botonCamara;
    public Button botonConfirmar;
    public Button botonExcluir;

    private ProductoPicking productoActual;
    private EstanteBlockUI estanteActual;
    private GameObject objetoFisicoActual;

    [Header("Flash efecto")]
    public CanvasGroup flashCanvasGroup;

    private bool _subscribed = false;

    private void OnEnable()
    {
        ResetState();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        DOTween.Kill(this);
    }

    private void Subscribe()
    {
        if (_subscribed) return;

        botonCamara?.onClick.AddListener(OnCamaraClick);
        botonConfirmar?.onClick.AddListener(ConfirmarSurtido);
        dropdownCantidad?.onValueChanged.AddListener(OnDropdownChanged);

        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;

        botonCamara?.onClick.RemoveListener(OnCamaraClick);
        botonConfirmar?.onClick.RemoveListener(ConfirmarSurtido);
        dropdownCantidad?.onValueChanged.RemoveListener(OnDropdownChanged);

        _subscribed = false;
    }

    private void ResetState()
    {
        botonConfirmar.interactable = false;
        inputCodigoSurtido.text = "";
        dropdownCantidad.value = 0;
    }

    private void OnCamaraClick()
    {
        if (productoActual == null) return;

        HacerFlash();
        inputCodigoSurtido.text = productoActual.codigoProducto;
    }

    private void HacerFlash()
    {
        if (flashCanvasGroup == null) return;

        var img = flashCanvasGroup.GetComponentInChildren<Image>();
        if (img != null) img.color = Color.white;

        flashCanvasGroup.alpha = 0;
        flashCanvasGroup.gameObject.SetActive(true);

        flashCanvasGroup.DOFade(1, 0.1f).OnComplete(() =>
        {
            flashCanvasGroup.DOFade(0, 0.3f);
            SoundManager.Instance?.PlaySound("flash");

            if (PickingActivity.Instance != null && PickingActivity.Instance.gameObject.activeInHierarchy)
            {
                PickingActivity.Instance.LanzarInstruccionCamara(7);
            }
            else if (ChangePickingOrderActivity.Instance != null &&
                     ChangePickingOrderActivity.Instance.gameObject.activeInHierarchy)
            {
                ChangePickingOrderActivity.Instance.LanzarInstruccionCamara(7);
                ChangePickingOrderActivity.Instance.MostrarPanelDiferencia();
            }
        });
    }

    public void Configurar(ProductoPicking producto, EstanteBlockUI estante, GameObject objetoFisico)
    {
        productoActual = producto;
        estanteActual = estante;
        objetoFisicoActual = objetoFisico;

        imagenProducto.sprite = producto.imagen;
        nombreProducto.text = "Producto " + producto.nombreProducto;
        cantidadSolicitada.text = $"Solicitado: {producto.cantidadSolicitada}.0 PZA";
        codigoProducto.text = producto.codigoProducto;

        float precioAnterior = producto.precio + 0.01f;
        precio.text = $"De ${precioAnterior:F2} A ${producto.precio:F2}";

        ResetState();
    }

    private void ConfirmarSurtido()
    {
        if (productoActual == null) return;

        if (inputCodigoSurtido.text != productoActual.codigoProducto)
        {
            Debug.Log("⚠ Código incorrecto");
            return;
        }

        string textoSeleccionado = dropdownCantidad.options[dropdownCantidad.value].text;
        if (textoSeleccionado == "Selecciona...")
        {
            Debug.Log("⚠ Debes seleccionar una cantidad válida.");
            return;
        }

        int cantidadElegida = int.Parse(textoSeleccionado);
        if (cantidadElegida != productoActual.cantidadSolicitada)
        {
            Debug.Log("⚠ Cantidad incorrecta");
            return;
        }

        if (objetoFisicoActual != null)
            objetoFisicoActual.SetActive(false);

        estanteActual?.MarcarComoSurtido();

        SoundManager.Instance?.PlaySound("success");

        gameObject.SetActive(false);

        if (PickingActivity.Instance != null && PickingActivity.Instance.gameObject.activeInHierarchy)
        {
            PickingActivity.Instance.ActivarSiguienteEstante(estanteActual);
            PickingActivity.Instance.AvanzarIndiceUbicacionYReactivarBotones();
        }
        else if (ChangePickingOrderActivity.Instance != null &&
                 ChangePickingOrderActivity.Instance.gameObject.activeInHierarchy)
        {
            ChangePickingOrderActivity.Instance.ConfirmarFinal();
        }
    }

    private void OnDropdownChanged(int value)
    {
        if (productoActual == null) return;

        string textoSeleccionado = dropdownCantidad.options[value].text;
        if (textoSeleccionado == "0") return;

        int cantidadElegida = int.Parse(textoSeleccionado);

        if (cantidadElegida == productoActual.cantidadSolicitada)
        {
            if (PickingActivity.Instance != null && PickingActivity.Instance.gameObject.activeInHierarchy)
                PickingActivity.Instance.LanzarInstruccionCamara(8);
        }
    }

    public void SetInteractable(bool estado)
    {
        botonCamara.interactable = estado;
        botonConfirmar.interactable = estado;
        dropdownCantidad.interactable = estado;
        inputCodigoSurtido.interactable = estado;
    }

    public void SimularClickCamara()
    {
        if (productoActual == null) return;
        inputCodigoSurtido.text = productoActual.codigoProducto;
    }
}
