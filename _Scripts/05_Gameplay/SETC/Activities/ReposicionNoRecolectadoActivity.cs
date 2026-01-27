using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;

public class ReposicionNoRecolectadoActivity : ActivityBase
{
    public static ReposicionNoRecolectadoActivity Instance { get; private set; }

    [System.Serializable]
    public class ZonaObjetivo
    {
        public string zonaId;
        public Transform objetivoVisual;
        public string cameraPositionId;
    }

    [System.Serializable]
    public class PanelPorZona
    {
        public string zonaId;
        public GameObject panelUI;
    }

    [Header("Cámara y movimiento")]
    public CameraPathfindingManager cameraPathfindingManager;

    [Header("UI")]
    public GameObject canvasUbicaciones;
    public GameObject canvasCarrusel;
    public CarruselProductosUI carruselUI;

    [Header("Paneles de UI por Zona")]
    public List<PanelPorZona> panelesPorZona;

    [Header("Zonas")]
    public List<ZonaObjetivo> objetivosPorZona;

    [Header("Objetos 3D Globales")]
    public List<GameObject> objetos3DGlobales;

    [Header("Carrito GO")]
    public GameObject CarritoA4;
    private Dictionary<string, Transform> objetivosDict = new();
    private Transform objetivoActual = null;
    private string zonaActual = "";
    private HashSet<string> zonasCompletadas = new();

    public override void StartActivity()
    {
        base.StartActivity();
        Instance = this;

        ResetInstructions();
        ShowInstructionsPanel();
        CargarObjetivosVisuales();

        cameraController.MoveToPosition("A4", () =>
        {
            UpdateInstructionOnce(0, () =>
            {
                cameraController.MoveToPosition("A4 Carrito", () =>
                {
                    CarritoA4.SetActive(true);
                    UpdateInstructionOnce(1, () =>
                    {
                        cameraController.MoveToPosition("A4 Vista General", () =>
                        {
                            UpdateInstructionOnce(2, MostrarPanelUbicaciones);
                        });
                    });
                });
            });        
        });
    }

    protected override void Initialize() { }

    private void CargarObjetivosVisuales()
    {
        objetivosDict.Clear();
        foreach (var z in objetivosPorZona)
        {
            if (!objetivosDict.ContainsKey(z.zonaId))
                objetivosDict[z.zonaId] = z.objetivoVisual;
        }

        foreach (var obj in objetos3DGlobales)
        {
            if (obj != null)
                obj.SetActive(true);
        }
    }

    public void MostrarPanelUbicaciones()
    {
        canvasUbicaciones.SetActive(true);
    }

    public void OnClickUbicacion(string destino)
    {
        if (zonasCompletadas.Contains(destino))
        {
            Debug.Log("Ya completaste esta zona.");
            return;
        }

        canvasUbicaciones.SetActive(false);

        var zona = objetivosPorZona.Find(z => z.zonaId == destino);
        objetivoActual = zona != null ? zona.objetivoVisual : null;
        string cameraPositionId = zona != null ? zona.cameraPositionId : "P1";

        string nodoInicio = string.IsNullOrEmpty(zonaActual)
            ? cameraPathfindingManager.BuscarNodoCercano()
            : zonaActual;

        SoundManager.Instance.PlayLoop("pasos");

        cameraPathfindingManager.MoverCamaraDesdeHasta(
            nodoInicio,
            destino,
            1f,
            () =>
            {
                cameraController.MoveToPosition(cameraPositionId, () =>
                {
                    SoundManager.Instance.StopLoop("pasos");

                    zonaActual = destino;

                    ActivarPanelZona(destino);
                    UpdateInstructionOnce(3);
                    canvasCarrusel.SetActive(true);
                    carruselUI.GenerarCarrusel();
                });
            },
            objetivoActual
        );
    }


    public void OnProductoColocado()
    {
        zonasCompletadas.Add(zonaActual);

        canvasCarrusel.SetActive(false);
        DesactivarPanelZona(zonaActual);
        UpdateInstructionOnce(4);
        ZonaButton[] todos = canvasUbicaciones.GetComponentsInChildren<ZonaButton>();
        foreach (var z in todos)
        {
            if (z.zonaId == zonaActual)
            {
                z.Desactivar();
                break;
            }
        }

        if (zonasCompletadas.Count >= objetivosDict.Count)
        {
            CarritoA4.SetActive(false);
            SoundManager.Instance.PlaySound("win");
            UpdateInstructionOnce(5, () => CompleteActivity());
        }
        else
        {
            DOVirtual.DelayedCall(0.5f, MostrarPanelUbicaciones);
        }
    }




    public void ActivarPanelZona(string zonaId)
    {
        foreach (var panel in panelesPorZona)
            panel.panelUI.SetActive(false);

        var panelEncontrado = panelesPorZona.Find(p => p.zonaId == zonaId);
        if (panelEncontrado != null && panelEncontrado.panelUI != null)
        {
            panelEncontrado.panelUI.SetActive(true);
        }
        else
        {
            Debug.LogWarning($"No se encontró un panel asignado para la zona '{zonaId}'");
        }
    }

    public void DesactivarPanelZona(string zonaId)
    {
        var panel = panelesPorZona.Find(p => p.zonaId == zonaId);
        if (panel != null && panel.panelUI != null)
        {
            panel.panelUI.SetActive(false);
        }
    }
}
