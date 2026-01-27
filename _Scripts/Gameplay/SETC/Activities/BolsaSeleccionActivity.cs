using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class BolsaSeleccionActivity : ActivityBase
{
    [Header("Sprites por tipo de bolsa")]
    public Sprite spriteComestible;
    public Sprite spriteNoComestible;
    public Sprite spriteCongelado;

    [Header("Imágenes a cambiar")]
    public RawImage imagenSeleccion;
    public RawImage imagenOrdenamiento;

    [Header("Fase 1: Selección de Productos")]
    public GameObject panelSeleccion;
    public TextMeshProUGUI preguntaTexto;
    public List<Button> botonesProducto;
    public List<ProductoSETC> productos;

    [Header("Fase 2: Ordenamiento")]
    public GameObject panelOrdenamiento;
    public Transform contenedorProductos;
    public GameObject prefabOrdenable;
    public TextMeshProUGUI ordenamientoTexto;
    public Button botonValidar;
    public Button botonEmbolsar;

    [Header("Fase 3: Matching de Objetos")]
    public GameObject panelMatching;
    public List<DropZone> zonasDropMatching;
    public List<ArrastrableEntrega> objetosArrastrablesMatching;

    [Header("Fase 4: Clasificación de Entrega")]
    public GameObject panelClasificacion;
    public List<DropZone> zonasEntrega;
    public List<ArrastrableEntrega> objetosEntrega;

    private TipoBolsa[] ordenRondas = new TipoBolsa[] {
        TipoBolsa.Comestible,
        TipoBolsa.NoComestible,
        TipoBolsa.Congelado
    };

    private int rondaActual = 0;
    private TipoBolsa tipoActual;

    private List<ProductoSETC> seleccionCorrecta = new();
    private List<ProductoSETC> productosSeleccionados = new();
    private int correctosSeleccionados;

    private void ResetPanel(GameObject panel)
    {
        if (panel == null) return;

        panel.transform.localScale = Vector3.zero;
        panel.SetActive(false);

        var cg = panel.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = 0f;
    }

    private void OcultarTodosLosPaneles(GameObject excepto = null)
    {
        if (panelSeleccion != excepto) OcultarPanel(panelSeleccion);
        if (panelOrdenamiento != excepto) OcultarPanel(panelOrdenamiento);
        if (panelMatching != excepto) OcultarPanel(panelMatching);
        if (panelClasificacion != excepto) OcultarPanel(panelClasificacion);
    }

    public override void StartActivity()
    {
        base.StartActivity();

        ResetPanel(panelSeleccion);
        ResetPanel(panelOrdenamiento);
        ResetPanel(panelMatching);
        ResetPanel(panelClasificacion);

        rondaActual = 0;

        OcultarTodosLosPaneles();

        cameraController.MoveToPosition("Iniciando A1", () =>
        {
            UpdateInstructionOnce(0, IniciarRonda);
        });
    }

    private void ReproducirSonidoCorrecto() => SoundManager.Instance.PlaySound("success");
    private void ReproducirSonidoError() => SoundManager.Instance.PlaySound("error");

    private void IniciarRonda()
    {
        UpdateInstructionOnce(1);

        tipoActual = ordenRondas[rondaActual];
        seleccionCorrecta = productos.FindAll(p => p.tipo == tipoActual);
        productosSeleccionados.Clear();
        correctosSeleccionados = 0;

        ActualizarSpritesTipoBolsa();

        foreach (var b in botonesProducto) b.gameObject.SetActive(true);

        preguntaTexto.text = $"¿Qué productos van en la bolsa de {FormatearTipoBolsa(tipoActual)}?";

        MostrarPanel(panelSeleccion);

        ConfigurarBotones();
    }

    private void ActualizarSpritesTipoBolsa()
    {
        Sprite spriteActual = tipoActual switch
        {
            TipoBolsa.Comestible => spriteComestible,
            TipoBolsa.NoComestible => spriteNoComestible,
            TipoBolsa.Congelado => spriteCongelado,
            _ => null
        };

        if (spriteActual != null)
        {
            if (imagenSeleccion != null) imagenSeleccion.texture = spriteActual.texture;
            if (imagenOrdenamiento != null) imagenOrdenamiento.texture = spriteActual.texture;
        }
    }

    private void ConfigurarBotones()
    {
        List<ProductoSETC> productosDesordenados = new(productos);
        Shuffle(productosDesordenados);

        for (int i = 0; i < botonesProducto.Count; i++)
        {
            var producto = productosDesordenados[i];
            var btn = botonesProducto[i];

            var texto = btn.GetComponentInChildren<TextMeshProUGUI>();

            var imagenProducto = btn.transform.Find("Image")?.GetComponent<Image>();
            if (imagenProducto != null)
                imagenProducto.sprite = producto.imagen;

            texto.text = producto.nombre;
            btn.interactable = true;
            btn.image.color = Color.white;

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                if (producto.tipo == tipoActual)
                {
                    btn.image.color = Color.green;
                    ReproducirSonidoCorrecto();
                    productosSeleccionados.Add(producto);
                    correctosSeleccionados++;

                    if (correctosSeleccionados == seleccionCorrecta.Count)
                    {
                        UpdateInstructionOnce(2, MostrarPanelOrdenamiento);
                    }
                }
                else
                {
                    btn.image.color = Color.red;
                    ReproducirSonidoError();
                }

                btn.interactable = false;
            });
        }
    }

    private void MostrarPanelOrdenamiento()
    {
        OcultarPanel(panelSeleccion);        
        MostrarPanel(panelOrdenamiento);
        ordenamientoTexto.text = "Ordena los productos de menor a mayor peso";

        foreach (Transform hijo in contenedorProductos)
            Destroy(hijo.gameObject);

        List<ProductoSETC> desordenados = new(productosSeleccionados);
        Shuffle(desordenados);

        foreach (var p in desordenados)
        {
            GameObject obj = Instantiate(prefabOrdenable, contenedorProductos);
            obj.GetComponent<ProductoOrdenableUI>().Configurar(p);
        }

        botonEmbolsar.gameObject.SetActive(false);
        botonValidar.onClick.RemoveAllListeners();
        botonValidar.onClick.AddListener(ValidarOrdenamiento);
    }

    private void ValidarOrdenamiento()
    {
        List<float> pesos = new();

        foreach (Transform t in contenedorProductos)
        {
            var ordenable = t.GetComponent<ProductoOrdenableUI>();
            if (ordenable != null)
                pesos.Add(ordenable.producto.peso);
        }

        for (int i = 0; i < pesos.Count - 1; i++)
        {
            if (pesos[i] > pesos[i + 1])
            {
                ReproducirSonidoError();
                return;
            }
        }

        ReproducirSonidoCorrecto();

        foreach (Transform t in contenedorProductos)
        {
            var dragHandler = t.GetComponent<ProductoDragHandler>();
            if (dragHandler != null)
                dragHandler.enabled = false;
        }

        UpdateInstructionOnce(2);
        botonEmbolsar.gameObject.SetActive(true);
        botonEmbolsar.onClick.RemoveAllListeners();
        botonEmbolsar.onClick.AddListener(() =>
        {
            botonEmbolsar.interactable = false;

            OcultarPanel(panelOrdenamiento, () =>
            {
                rondaActual++;
                if (rondaActual < ordenRondas.Length)
                {
                    botonEmbolsar.interactable = true;
                    UpdateInstructionOnce(3, IniciarRonda);
                }
                else
                {
                    UpdateInstructionOnce(4, MostrarPanelMatching);
                }
            });
        });
    }

    private void MostrarPanelMatching()
    {
        OcultarTodosLosPaneles(panelMatching);
        MostrarPanel(panelMatching);

        foreach (var z in zonasDropMatching) z.ResetZona();
        foreach (var o in objetosArrastrablesMatching) o.ResetObjeto();

        Shuffle(objetosArrastrablesMatching);

        for (int i = 0; i < objetosArrastrablesMatching.Count; i++)
        {
            var o = objetosArrastrablesMatching[i];
            o.transform.SetSiblingIndex(i);
            o.OnDropCorrecto += RevisarMatchingCompleto;
        }
    }

    private void RevisarMatchingCompleto()
    {
        if (zonasDropMatching.TrueForAll(z => z.EsZonaCompleta()))
        {
            UpdateInstructionOnce(5, MostrarPanelClasificacion);
        }
    }

    private void MostrarPanelClasificacion()
    {
        OcultarTodosLosPaneles(panelClasificacion);
        MostrarPanel(panelClasificacion);

        foreach (var z in zonasEntrega) z.ResetZona();
        foreach (var o in objetosEntrega) o.ResetObjeto();

        foreach (var o in objetosEntrega)
            o.OnDropCorrecto += RevisarClasificacionCompleta;
    }

    private void RevisarClasificacionCompleta()
    {
        if (zonasEntrega.TrueForAll(z => z.EsZonaCompleta()))
        {
            DesactivarTodosLosObjetos();
            SoundManager.Instance.PlaySound("win");
            UpdateInstructionOnce(6, () =>
            {
                CompleteActivity();
            });
        }
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = Random.Range(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void DesactivarTodosLosObjetos()
    {
        // Desactiva paneles
        panelSeleccion?.SetActive(false);
        panelOrdenamiento?.SetActive(false);
        panelMatching?.SetActive(false);
        panelClasificacion?.SetActive(false);

        // Desactiva botones de selección
        foreach (var b in botonesProducto)
            b.gameObject.SetActive(false);

        // Desactiva objetos arrastrables
        foreach (var o in objetosArrastrablesMatching)
            o.gameObject.SetActive(false);

        foreach (var o in objetosEntrega)
            o.gameObject.SetActive(false);

    }

    protected override void Initialize() { }
}
