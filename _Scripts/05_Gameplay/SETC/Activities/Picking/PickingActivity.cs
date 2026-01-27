using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.UI;
using System;

public class PickingActivity : ActivityBase
{
    public static PickingActivity Instance { get; private set; }

    [Serializable]
    public class ZonaObjetivo
    {
        public string zonaId;
        public Transform objetivoVisual;
        public string cameraPositionId;
    }

    [Header("Referencias UI Picking Normal")]
    public GameObject PanelGeneralUICel;
    public Transform scrollContent;
    public GameObject estantePrefab;
    public ProductoPanelUI panelProductoUI;

    [Header("Sprites productos")]
    public Sprite spriteQueso;
    public Sprite spriteManzana;
    public Sprite spriteHarina;
    public Sprite spriteHelado;
    public Sprite spriteRefresco;

    [Header("Celular Flow")]
    public Transform celularObjeto3D;
    public GameObject panelInvisibleTouch;
    public RectTransform panelCelularUI;
    public Button botonCortina;
    public float duracionAnimacionCortina = 0.5f;
    public Vector2 posMostrar = new Vector2(0, 0);
    public Vector2 posOcultar = new Vector2(-500, 0);

    [Header("Picking por ubicaciones con 5 botones")]
    public GameObject panelBotonesUbicaciones;
    public List<Button> botonesUbicaciones;
    public List<Transform> productosFísicos;
    public CanvasGroup flashCanvasGroup;

    [Header("Cámara y movimiento")]
    public CameraPathfindingManager cameraPathfindingManager;
    private string zonaActual = "";

    [Header("Zonas para pathfinding")]
    public List<ZonaObjetivo> objetivosPorZona;

    [Header("Paneles invisibles por producto")]
    public List<PanelInvisibleTouch> panelesPorProducto;

    private int indiceActualUbicacion = 0;
    private List<ProductoPicking> productosDelPedido = new List<ProductoPicking>();

    private bool _cortinaSubscribed = false;

    protected override void Awake()
    {
        base.Awake();

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        SubscribeCortina();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        UnsubscribeCortina();
    }

    private void OnDestroy()
    {
        UnsubscribeCortina();

        if (Instance == this)
            Instance = null;
    }

    private void SubscribeCortina()
    {
        if (_cortinaSubscribed) return;
        if (botonCortina == null) return;

        botonCortina.onClick.AddListener(TogglePanelCelular);
        _cortinaSubscribed = true;
    }

    private void UnsubscribeCortina()
    {
        if (!_cortinaSubscribed) return;

        if (botonCortina != null)
            botonCortina.onClick.RemoveListener(TogglePanelCelular);

        _cortinaSubscribed = false;
    }

    public override void StartActivity()
    {
        base.StartActivity();

        PanelGeneralUICel.SetActive(true);
        scrollContent.gameObject.SetActive(false);
        panelProductoUI.gameObject.SetActive(false);

        celularObjeto3D?.gameObject.SetActive(true);
        panelInvisibleTouch?.SetActive(false);

        if (panelCelularUI != null)
        {
            panelCelularUI.anchoredPosition = posOcultar;
        }

        if (botonCortina != null)
        {
            botonCortina.gameObject.SetActive(false);
        }

        IniciarFlujoCelular();
    }

    private void IniciarFlujoCelular()
    {
        cameraController.MoveToPosition("A6 Inicio Actividad", () =>
        {
            UpdateInstructionOnce(0, () =>
            {
                cameraController.MoveToPosition("A6 Vista Celular", () =>
                {
                    UpdateInstructionOnce(1, () =>
                    {
                        panelInvisibleTouch?.GetComponent<PanelInvisibleTouch>().Activar(() =>
                        {
                            MostrarPanelCelular();
                        });
                    });
                });
            });
        });
    }

    public void MostrarPanelCelular()
    {
        panelCelularUI.DOAnchorPos(posMostrar, duracionAnimacionCortina);

        botonCortina?.gameObject.SetActive(true);
        panelInvisibleTouch?.SetActive(false);

        UpdateInstructionOnce(2);
    }

    private void TogglePanelCelular()
    {
        if (botonCortina != null)
            botonCortina.interactable = false;

        float currentX = panelCelularUI.anchoredPosition.x;
        float threshold = 10f;

        if (Mathf.Abs(currentX - posMostrar.x) < threshold)
        {
            panelCelularUI.DOAnchorPosX(posOcultar.x, duracionAnimacionCortina)
                .SetEase(Ease.InOutCubic)
                .OnComplete(() => botonCortina.interactable = true);
        }
        else
        {
            panelCelularUI.DOAnchorPosX(posMostrar.x, duracionAnimacionCortina)
                .SetEase(Ease.InOutCubic)
                .OnComplete(() => botonCortina.interactable = true);
        }
    }

    public void EmpezarPicking()
    {
        scrollContent.gameObject.SetActive(true);

        CrearListaProductos();
        BuildScrollEstantes();

        if (productosDelPedido.Count > 0)
        {
            var primerProducto = productosDelPedido[0];
            var estante = BuscarEstantePorProducto(primerProducto);
            panelProductoUI.gameObject.SetActive(true);
            panelProductoUI.Configurar(primerProducto, estante, null);

            panelProductoUI.SetInteractable(false);
        }

        UpdateInstructionOnce(3, () =>
        {
            panelBotonesUbicaciones.SetActive(true);
            ConfigurarBotonesUbicaciones();
            UpdateInstructionOnce(4);
        });
    }

    private void ConfigurarBotonesUbicaciones()
    {
        for (int i = 0; i < botonesUbicaciones.Count; i++)
        {
            int index = i;
            string destino = objetivosPorZona[index].zonaId;

            botonesUbicaciones[i].onClick.RemoveAllListeners();
            botonesUbicaciones[i].onClick.AddListener(() => OnClickUbicacion(destino));
            botonesUbicaciones[i].interactable = (i == 0);
        }
    }

    public void OnClickUbicacion(string destino)
    {
        if (botonesUbicaciones[indiceActualUbicacion].GetComponent<ZonaButton>()?.zonaId != destino)
        {
            SoundManager.Instance.PlaySound("error");
            Debug.Log($"Debes presionar primero: {botonesUbicaciones[indiceActualUbicacion].GetComponent<ZonaButton>()?.zonaId}");
            return;
        }

        UpdateInstructionOnce(5);

        panelBotonesUbicaciones?.SetActive(false);

        var zona = objetivosPorZona.Find(z => z.zonaId == destino);
        string cameraPositionId = zona?.cameraPositionId ?? "P1";
        Transform objetivoActual = zona?.objetivoVisual;

        string nodoInicio = string.IsNullOrEmpty(zonaActual)
            ? cameraPathfindingManager.BuscarNodoCercano()
            : zonaActual;

        SoundManager.Instance.PlayLoop("pasos");

        cameraPathfindingManager.MoverCamaraDesdeHasta(nodoInicio, destino, 1f, () =>
        {
            cameraController.MoveToPosition(cameraPositionId, () =>
            {
                SoundManager.Instance.StopLoop("pasos");
                zonaActual = destino;

                if (indiceActualUbicacion < productosFísicos.Count)
                {
                    var producto = productosFísicos[indiceActualUbicacion];
                    var productoAnim = producto.GetComponent<ProductoAnimacion>();
                    productoAnim?.EmpezarParpadeo();
                }

                if (indiceActualUbicacion < panelesPorProducto.Count)
                {
                    var productoPicking = productosDelPedido[indiceActualUbicacion];
                    var estanteUI = BuscarEstantePorProducto(productoPicking);
                    panelProductoUI.gameObject.SetActive(true);
                    panelProductoUI.Configurar(productoPicking, estanteUI, null);
                    panelProductoUI.SetInteractable(false);

                    var panelTouch = panelesPorProducto[indiceActualUbicacion];
                    if (panelTouch != null)
                    {
                        panelTouch.gameObject.SetActive(true);
                        panelTouch.Activar(() =>
                        {
                            ProcesarProductoTomado(indiceActualUbicacion);
                        });
                    }
                }
            });
        }, objetivoActual);
    }

    private void ProcesarProductoTomado(int index)
    {
        var producto = productosFísicos[index];
        var productoAnim = producto.GetComponent<ProductoAnimacion>();

        if (productoAnim != null)
        {
            productoAnim.DetenerParpadeo();

            productoAnim.Girar(() =>
            {
                var productoPicking = productosDelPedido[index];
                var estanteUI = BuscarEstantePorProducto(productoPicking);
                MostrarPanelProducto(productoPicking, estanteUI, producto.gameObject);

                panelProductoUI.SetInteractable(true);

                UpdateInstructionOnce(6);
            });
        }
        else
        {
            var productoPicking = productosDelPedido[index];
            var estanteUI = BuscarEstantePorProducto(productoPicking);

            MostrarPanelProducto(productoPicking, estanteUI, producto.gameObject);
        }
    }

    private EstanteBlockUI BuscarEstantePorProducto(ProductoPicking producto)
    {
        foreach (Transform child in scrollContent)
        {
            var estanteUI = child.GetComponent<EstanteBlockUI>();
            if (estanteUI != null && estanteUI.tmpNombreEstante.text.Contains(producto.nombreEstante))
                return estanteUI;
        }
        return null;
    }

    private void CrearListaProductos()
    {
        productosDelPedido.Clear();
        productosDelPedido.Add(new ProductoPicking("65", "Refrescos", "Fresh Manzana", "123456789", 1, 178.50f, spriteQueso));
        productosDelPedido.Add(new ProductoPicking("74", "Hierbas", "Te Verde", "654321754", 1, 30.00f, spriteManzana));
        productosDelPedido.Add(new ProductoPicking("88", "Galletas", "Galletas de Avena", "789012127", 1, 15.75f, spriteHarina));
        productosDelPedido.Add(new ProductoPicking("92", "Lacteos", "Leche Entera", "8901237413", 1, 60.00f, spriteHelado));
        productosDelPedido.Add(new ProductoPicking("12", "Vinos", "Vino Morado", "9012344727", 1, 12.00f, spriteRefresco));
    }

    private void BuildScrollEstantes()
    {
        for (int i = 0; i < productosDelPedido.Count; i++)
        {
            var producto = productosDelPedido[i];
            var block = Instantiate(estantePrefab, scrollContent);
            var estanteUI = block.GetComponent<EstanteBlockUI>();

            estanteUI.Configurar(
                "SURTIDO NORMAL",
                producto.numeroEstante,
                producto.nombreEstante,
                producto
            );

            estanteUI.SetActivo(i == 0);
        }
    }

    public void MostrarPanelProducto(ProductoPicking producto, EstanteBlockUI estante)
    {
        int index = productosDelPedido.IndexOf(producto);
        GameObject objetoFisico = (index >= 0 && index < productosFísicos.Count) ? productosFísicos[index].gameObject : null;

        MostrarPanelProducto(producto, estante, objetoFisico);
    }

    public void MostrarPanelProducto(ProductoPicking producto, EstanteBlockUI estante, GameObject objetoFisico)
    {
        panelProductoUI.gameObject.SetActive(true);
        panelProductoUI.Configurar(producto, estante, objetoFisico);
    }

    public void ActivarSiguienteEstante(EstanteBlockUI estanteActual)
    {
        bool activarSiguiente = false;

        foreach (Transform child in scrollContent)
        {
            var estanteUI = child.GetComponent<EstanteBlockUI>();
            if (estanteUI == null) continue;

            if (activarSiguiente)
            {
                estanteUI.SetActivo(true);
                return;
            }

            if (estanteUI == estanteActual)
                activarSiguiente = true;
        }
    }

    public void ReactivarBotonesPicking()
    {
        if (indiceActualUbicacion < botonesUbicaciones.Count)
        {
            botonesUbicaciones[indiceActualUbicacion].interactable = true;
            panelBotonesUbicaciones?.SetActive(true);
        }
    }

    public void AvanzarIndiceUbicacionYReactivarBotones()
    {
        indiceActualUbicacion++;
        if (indiceActualUbicacion < botonesUbicaciones.Count)
        {
            botonesUbicaciones[indiceActualUbicacion].interactable = true;
            panelBotonesUbicaciones?.SetActive(true);
        }
        else
        {
            string nodoInicio = zonaActual;
            string nodoFinal = "A3";

            cameraPathfindingManager.MoverCamaraDesdeHasta(
                nodoInicio,
                nodoFinal,
                1f,
                () =>
                {
                    cameraController.MoveToPosition("A6 Inicio Actividad", () =>
                    {
                        SoundManager.Instance.PlaySound("win");
                        UpdateInstructionOnce(9, () =>
                        {
                            CompleteActivity();
                        });
                    });
                },
                objetivosPorZona[0].objetivoVisual
            );
        }
    }

    public void LanzarInstruccionCamara(int index)
    {
        UpdateInstructionOnce(index);
    }

    protected override void Initialize()
    {
    }
}
