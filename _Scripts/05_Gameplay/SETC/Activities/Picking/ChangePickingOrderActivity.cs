using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System;

public class ChangePickingOrderActivity : ActivityBase
{
    public static ChangePickingOrderActivity Instance { get; private set; }

    [Serializable]
    public class ZonaObjetivo
    {
        public string zonaId;
        public Transform objetivoVisual;
        public string cameraPositionId;
    }

    [Header("Referencias UI Picking Normal")]
    public Transform scrollContent;
    public GameObject estantePrefab;
    public ProductoPanelUI panelProductoUI;

    [Header("Sprites productos")]
    public Sprite spriteQueso;

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

    [Header("Flujo cambio de producto")]
    public GameObject panelDiferencia;
    public Button botonVolverModulo;
    public Button botonConfirmar;

    [Header("Panel llamada celular")]
    public GameObject panelLlamadaCelular;
    public TMP_Text timerText;

    [Header("Pop ups llamada")]
    public GameObject panelPopUpCliente;
    public TMP_Text textoCliente;

    public GameObject panelPopUpPicker;
    public TMP_Text textoPicker;

    public GameObject panelInferiorLlamada;
    public Button botonLlamarCliente;

    public GameObject panelOpcionesCliente;
    public List<Button> botonesOpciones;
    public int indiceOpcionCorrecta;

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

    public override void StartActivity()
    {
        base.StartActivity();

        scrollContent.gameObject.SetActive(false);
        panelProductoUI.gameObject.SetActive(false);

        celularObjeto3D?.gameObject.SetActive(true);
        panelInvisibleTouch?.SetActive(false);
        botonConfirmar.interactable = false;

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

        CrearListaConUnProducto();
        BuildScrollEstantes();

        if (productosDelPedido.Count > 0)
        {
            var primerProducto = productosDelPedido[0];
            panelProductoUI.gameObject.SetActive(true);
            panelProductoUI.Configurar(primerProducto, null, null);
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
            Debug.Log($"⛔ Debes presionar primero: {botonesUbicaciones[indiceActualUbicacion].GetComponent<ZonaButton>()?.zonaId}");
            return;
        }

        UpdateInstructionOnce(5);
        SoundManager.Instance.PlayLoop("pasos");
        panelBotonesUbicaciones?.SetActive(false);

        var zona = objetivosPorZona.Find(z => z.zonaId == destino);
        string cameraPositionId = zona?.cameraPositionId ?? "P1";
        Transform objetivoActual = zona?.objetivoVisual;

        string nodoInicio = string.IsNullOrEmpty(zonaActual)
            ? cameraPathfindingManager.BuscarNodoCercano()
            : zonaActual;

        cameraPathfindingManager.MoverCamaraDesdeHasta(nodoInicio, destino, 1f, () =>
        {
            cameraController.MoveToPosition(cameraPositionId, () =>
            {
                zonaActual = destino;
                SoundManager.Instance.StopLoop("pasos");
                if (indiceActualUbicacion < productosFísicos.Count)
                {
                    var producto = productosFísicos[indiceActualUbicacion];
                    var productoAnim = producto.GetComponent<ProductoAnimacion>();
                    productoAnim?.EmpezarParpadeo();
                }

                if (indiceActualUbicacion < panelesPorProducto.Count)
                {
                    var productoPicking = productosDelPedido[indiceActualUbicacion];
                    panelProductoUI.gameObject.SetActive(true);
                    panelProductoUI.Configurar(productoPicking, null, null);
                    panelProductoUI.SetInteractable(false);

                    var panelTouch = panelesPorProducto[indiceActualUbicacion];
                    if (panelTouch != null)
                    {
                        panelTouch.gameObject.SetActive(true);
                        panelTouch.Activar(() =>
                        {
                            ProcesarProductoTomado();
                        });
                    }
                }
            });
        }, objetivoActual);
    }
    private void ProcesarProductoTomado()
    {
        var producto = productosFísicos[indiceActualUbicacion];
        var productoPicking = productosDelPedido[indiceActualUbicacion];

        var productoAnim = producto.GetComponent<ProductoAnimacion>();

        if (productoAnim != null)
        {
            productoAnim.DetenerParpadeo();

            productoAnim.Girar(() =>
            {
                UpdateInstructionOnce(6);             
                panelProductoUI.gameObject.SetActive(true);
                panelProductoUI.SetInteractable(true);
                botonConfirmar.interactable = false;
                panelProductoUI.Configurar(productoPicking, null, producto.gameObject);
            });
        }
    }
    public void MostrarPanelDiferencia()
    {
        panelDiferencia.SetActive(true);

        botonVolverModulo.gameObject.SetActive(true);
        botonVolverModulo.onClick.RemoveAllListeners();
        botonVolverModulo.onClick.AddListener(() =>
        {
            VolverAlModulo();
        });
    }

    private void VolverAlModulo()
    {
        string nodoInicio = zonaActual;
        string nodoFinal = "A3";

        botonVolverModulo.gameObject.SetActive(false);

        cameraPathfindingManager.MoverCamaraDesdeHasta(nodoInicio, nodoFinal, 1f, () =>
        {
            cameraController.MoveToPosition("A6 Inicio Actividad", () =>
            {
                UpdateInstructionOnce(8, () =>
                {
                    panelInferiorLlamada.SetActive(true);
                    botonLlamarCliente.onClick.RemoveAllListeners();
                    botonLlamarCliente.onClick.AddListener(() =>
                    {                      
                        MostrarPanelLlamarCliente();
                    });
                    
                });
            });
        }, objetivosPorZona[0].objetivoVisual);
    }

    private void MostrarPanelLlamarCliente()
    {
        botonLlamarCliente.interactable = false;
        panelLlamadaCelular.SetActive(true);

        timerText.text = "Llamando...";

        SoundManager.Instance.PlaySound("ring", () =>
        {
            timerText.text = "00:00";
            StartCoroutine(ContadorLlamada());

            MostrarPopUpCliente("Buenos días, ¿qué pasó?");
            SoundManager.Instance.PlaySound("vozcliente", () =>
            {
                UpdateInstructionOnce(9, () =>
                {
                    MostrarOpcionesCliente();
                });
            });
        });
    }

    private void MostrarOpcionesCliente()
    {
        panelOpcionesCliente.SetActive(true);

        for (int i = 0; i < botonesOpciones.Count; i++)
        {
            int index = i;
            botonesOpciones[i].onClick.RemoveAllListeners();
            botonesOpciones[i].onClick.AddListener(() =>
            {
                if (index == indiceOpcionCorrecta)
                {
                    panelOpcionesCliente.SetActive(false);
                    SoundManager.Instance.PlaySound("success");
                    MostrarPopUpPicker("Buenos días, el producto que desea no se encuentra, pero tengo este otro que tiene un sabor y cantidad parecida, ¿desea hacer el cambio?");
                    SoundManager.Instance.PlaySound("picker_respuesta", () =>
                    {
                        MostrarPopUpCliente("Sí, está bien, agréguelo.");
                        SoundManager.Instance.PlaySound("vozcliente1", () =>
                        {
                            StopCoroutine(ContadorLlamada());
                            timerText.text = "Llamada finalizada";
                            DOVirtual.DelayedCall(1f, () =>
                            {
                                panelLlamadaCelular.SetActive(false);
                                panelProductoUI.gameObject.SetActive(true);
                                botonConfirmar.interactable = true;
                                UpdateInstructionOnce(10);
                            });
                        });
                    });
                }
                else
                {
                    SoundManager.Instance.PlaySound("error");
                }
            });
        }
    }
    private IEnumerator ContadorLlamada()
    {
        int segundos = 0;

        while (panelLlamadaCelular.activeSelf)
        {
            int minutos = segundos / 60;
            int segundosMostrar = segundos % 60;

            timerText.text = $"{minutos:00}:{segundosMostrar:00}";
            segundos++;

            yield return new WaitForSeconds(1f);
        }
    }

    private void CrearListaConUnProducto()
    {
        productosDelPedido.Clear();
        productosDelPedido.Add(new ProductoPicking("65", "Refrescos", "Fresh Limón", "123456789", 1, 178.50f, spriteQueso));
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

    public void LanzarInstruccionCamara(int indice)
    {
        UpdateInstructionOnce(indice);
    }
    private void MostrarPopUpCliente(string texto)
    {
        panelPopUpCliente.SetActive(true);
        StartCoroutine(EscribirTexto(textoCliente, texto));
    }

    private void MostrarPopUpPicker(string texto)
    {
        panelPopUpPicker.SetActive(true);
        StartCoroutine(EscribirTexto(textoPicker, texto));
    }

    private IEnumerator EscribirTexto(TMP_Text textUI, string texto)
    {
        textUI.text = "";
        foreach (char c in texto)
        {
            textUI.text += c;
            yield return new WaitForSeconds(0.03f);
        }
    }

    public void ConfirmarFinal()
    {
        SoundManager.Instance.PlaySound("win");
        UpdateInstructionOnce(11, () =>
        {
            CompleteActivity();
        });
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
        if (Instance == this) Instance = null;
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

    protected override void Initialize() { }
}
