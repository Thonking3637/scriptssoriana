using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;

[System.Serializable]
public class ProductoVerificacion
{
    public ProductoSETC producto;
    public int cantidad;
    public bool estaEnCarrito;
}

public class VerificacionMercanciaActivity : ActivityBase
{
    [Header("Camera Systems")]
    public CanvasGroup fadeCanvasGroup;

    [Header("Paneles UI")]
    public GameObject scrollViewPanel;
    public GameObject stickerPanel;
    public GameObject carritoPanel;

    [Header("Prefabs y contenedor UI")]
    public Transform scrollContent;
    public GameObject blockPrefab;

    [Header("Botones de vistas del carrito")]
    public GameObject buttonLeft;
    public GameObject buttonRight;
    public GameObject buttonTop;
    public GameObject buttonFront;

    [Header("Botones finales")]
    public Button btnDejarPasarPedido;
    public Button btnRegresarAModulo;

    [Header("Productos para carritos")]
    public List<ProductoVerificacion> productosCarrito1;
    public List<ProductoVerificacion> productosCarrito2;

    [Header("Botón para validar")]
    public Button btnValidarPedido;

    [Header("Carritos físicos")]
    public GameObject carritoObjeto1;
    public GameObject carritoObjeto2;

    private List<List<ProductoVerificacion>> carritos;
    private int carritoActualIndex = 0;

    private List<BlockUI> allBlocks = new();

    private bool pressedLeft, pressedRight, pressedTop;
    private bool pasoLeftRightCompletado, pasoTopCompletado, pasoFrontCompletado;

    public static VerificacionMercanciaActivity Instance { get; private set; }

    private bool EsGuiado => (carritoActualIndex == 0); // solo guiado en el primer carrito

    protected override void Awake()
    {
        base.Awake();
        Instance = this;
    }

    public override void StartActivity()
    {
        base.StartActivity();

        carritoActualIndex = 0;

        carritos = new List<List<ProductoVerificacion>>()
        {
            productosCarrito1,
            productosCarrito2
        };

        btnValidarPedido.interactable = false;
        btnValidarPedido.onClick.RemoveAllListeners();
        btnValidarPedido.onClick.AddListener(ValidarTodos);

        OcultarTodoUI();
        PrepararBotonesFinales();

        cameraController.MoveToPosition("A5 Inicio Actividad", () =>
        {
            UpdateInstructionOnce(0, () =>
            {
                cameraController.MoveToPosition("A5 Vista General", () =>
                {
                    UpdateInstructionOnce(1, () =>
                    {
                        StartCoroutine(FadeAndMoveToFrontCarritos());
                    });
                });
            });
        });
    }

    private void PrepararBotonesFinales()
    {
        btnDejarPasarPedido.interactable = false;
        btnRegresarAModulo.interactable = false;
    }

    private void OcultarTodoUI()
    {
        scrollViewPanel.SetActive(false);
        stickerPanel.SetActive(false);
        carritoPanel.SetActive(false);

        buttonLeft.SetActive(false);
        buttonRight.SetActive(false);
        buttonTop.SetActive(false);
        buttonFront.SetActive(false);
    }

    private IEnumerator FadeAndMoveToFrontCarritos()
    {
        if (carritoActualIndex == 0)
        {
            carritoObjeto1.SetActive(true);
            carritoObjeto2.SetActive(false);
        }
        else
        {
            carritoObjeto1.SetActive(false);
            carritoObjeto2.SetActive(true);
        }

        fadeCanvasGroup.alpha = 1f;
        fadeCanvasGroup.gameObject.SetActive(true);

        bool cameraDone = false;
        cameraController.MoveToPosition("A5 C1 VistaFrontal", () =>
        {
            cameraDone = true;
        });

        while (!cameraDone)
            yield return null;

        yield return fadeCanvasGroup.DOFade(0, 1.2f).WaitForCompletion();

        if (carritoActualIndex == 0)
        {
            MostrarBotonesLeftRight();
        }
        else
        {
            carritoPanel.SetActive(true);
            buttonLeft.SetActive(true);
            buttonRight.SetActive(true);
            buttonTop.SetActive(true);
            buttonFront.SetActive(true);

            pasoLeftRightCompletado = true;

            ActivarRevisionProductos();
        }
    }

    private void MostrarBotonesLeftRight()
    {
        UpdateInstructionOnce(2, () =>
        {
            carritoPanel.SetActive(true);
            buttonLeft.SetActive(true);
        });
    }

    public void OnVistaLeftPressed()
    {
        //cameraController.MoveToPosition(Vistas.left);
        pressedLeft = true;

        if (EsGuiado)
        {
            UpdateInstructionOnce(3);
            buttonRight.SetActive(true);
            RevisarLeftRight();
        }
    }

    public void OnVistaRightPressed()
    {
        //cameraController.MoveToPosition(Vistas.right);
        pressedRight = true;

        if (EsGuiado)
            RevisarLeftRight();
    }

    private void RevisarLeftRight()
    {
        if (!pasoLeftRightCompletado && pressedLeft && pressedRight)
        {
            pasoLeftRightCompletado = true;
            buttonTop.SetActive(true);
            UpdateInstructionOnce(4);
        }
    }

    public void OnVistaTopPressed()
    {
        //cameraController.MoveToPosition(Vistas.top);
        pressedTop = true;

        if (EsGuiado)
        {
            RevisarTop();
        }
        else
        {
            // En C2, no exigimos Left/Right
            if (!pasoTopCompletado)
            {
                pasoTopCompletado = true;
                buttonFront.SetActive(true);
                UpdateInstructionOnce(5);
            }
        }
    }

    private void RevisarTop()
    {
        if (pasoLeftRightCompletado && !pasoTopCompletado && pressedTop)
        {
            pasoTopCompletado = true;
            buttonFront.SetActive(true);
            UpdateInstructionOnce(5);
        }
    }

    public void OnVistaFrontPressed()
    {
        if (EsGuiado)
        {
            RevisarFront(); // en C1 requiere Top antes de Front
        }
        else
        {
            // En C2, permite avanzar directo a la revisión
            if (!pasoFrontCompletado)
            {
                pasoFrontCompletado = true;
                UpdateInstructionOnce(6, ActivarRevisionProductos);
            }
        }
    }

    private void RevisarFront()
    {
        if (!pasoFrontCompletado)
        {
            if (!pasoTopCompletado)
            {
                return;
            }
            pasoFrontCompletado = true;
            UpdateInstructionOnce(6, ActivarRevisionProductos);
        }
    }

    private void ActivarRevisionProductos()
    {
        scrollViewPanel.SetActive(true);
        stickerPanel.SetActive(true);
        BuildBlocksFor(carritos[carritoActualIndex]);

        if (carritoActualIndex == 1)
        {
            UpdateInstructionOnce(9);
        }
    }


    private void BuildBlocksFor(List<ProductoVerificacion> lista)
    {
        foreach (var block in allBlocks)
            Destroy(block.gameObject);

        allBlocks.Clear();

        foreach (var item in lista)
        {
            GameObject block = Instantiate(blockPrefab, scrollContent);
            BlockUI blockUI = block.GetComponent<BlockUI>();
            blockUI.SetData(item.producto, item.cantidad);
            blockUI.estaEnCarrito = item.estaEnCarrito;
            allBlocks.Add(blockUI);
        }
    }

    public void RevisarSiTodosConSticker()
    {
        foreach (var block in allBlocks)
        {
            if (!block.TieneSticker())
                return;
        }

        btnValidarPedido.interactable = true;

        UpdateInstructionOnce(7);
    }


    public void ValidarTodos()
    {
        bool hasErrors = false;

        foreach (var block in allBlocks)
        {
            if (!block.EsCorrecto())
            {
                block.ResetSticker();
                hasErrors = true;
            }
        }

        if (hasErrors)
        {
            SoundManager.Instance.PlaySound("badproducts");
        }
        else
        {
            SoundManager.Instance.PlaySound("success");
            UpdateInstructionOnce(8);
            btnDejarPasarPedido.interactable = true;
            btnRegresarAModulo.interactable = true;

            btnValidarPedido.interactable = false;

            btnDejarPasarPedido.onClick.RemoveAllListeners();
            btnRegresarAModulo.onClick.RemoveAllListeners();

            if (carritoActualIndex == 0)
            {
                btnDejarPasarPedido.onClick.AddListener(() =>
                {
                    SoundManager.Instance.PlaySound("success");
                    AvanzarASiguienteCarrito();
                });

                btnRegresarAModulo.onClick.AddListener(() =>
                {
                    SoundManager.Instance.PlaySound("error");
                });
            }
            else
            {
                btnRegresarAModulo.onClick.AddListener(() =>
                {
                    scrollViewPanel.SetActive(false);
                    stickerPanel.SetActive(false);
                    carritoPanel.SetActive(false);
                    SoundManager.Instance.PlaySound("success");
                    cameraController.MoveToPosition("A5 Inicio Actividad", () =>
                    {
                        SoundManager.Instance.PlaySound("win");
                        UpdateInstructionOnce(10, () =>
                        {
                            CompleteActivity();
                        });
                    });
                });


                btnDejarPasarPedido.onClick.AddListener(() =>
                {
                    SoundManager.Instance.PlaySound("error");
                });
            }
        }
    }

    private void AvanzarASiguienteCarrito()
    {
        carritoActualIndex++;
        ResetFlags();
        PrepararBotonesFinales();
        OcultarTodoUI();

        fadeCanvasGroup.alpha = 1f;
        fadeCanvasGroup.gameObject.SetActive(true);

        StartCoroutine(FadeAndMoveToFrontCarritos());
    }

    private void ResetFlags()
    {
        pressedLeft = pressedRight = pressedTop = false;
        pasoLeftRightCompletado = pasoTopCompletado = pasoFrontCompletado = false;
    }

    protected override void Initialize() { }
}