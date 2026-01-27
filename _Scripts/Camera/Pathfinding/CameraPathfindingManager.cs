using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;

public class CameraPathfindingManager : MonoBehaviour
{
    public Transform cameraTransform;
    private List<NodoCamara> nodos;

    private string zonaActual = "A3";

    [Header("Rotación")]
    public float lookAheadTime = 0.3f;
    public bool usarLookAt = true;

    [Header("Velocidad de cámara")]
    public float velocidad = 4f; // metros por segundo

    // 🟢 Nueva ruta guardada para visualización
    private List<NodoCamara> ultimaRutaCalculada;

    private void Awake()
    {
        nodos = new List<NodoCamara>(FindObjectsOfType<NodoCamara>());
    }

    public string BuscarNodoCercano()
    {
        float minDist = float.MaxValue;
        string nodoMasCercano = zonaActual;

        foreach (var nodo in nodos)
        {
            float dist = Vector3.Distance(cameraTransform.position, nodo.Punto.position);
            if (dist < minDist)
            {
                minDist = dist;
                nodoMasCercano = nodo.id;
            }
        }

        return nodoMasCercano;
    }

    public void MoverCamaraDesdeHasta(
    string desde,
    string hasta,
    float duracion = 1f,
    System.Action onComplete = null,
    Transform objetivoLookAt = null)
    {
        DOTween.Kill(cameraTransform);

        Debug.Log($"🔎 Buscando ruta: de {desde} a {hasta}");

        var inicio = nodos.Find(n => n.id == desde);
        var fin = nodos.Find(n => n.id == hasta);

        if (inicio == null || fin == null)
        {
            Debug.LogError($"❌ Nodo no encontrado. Desde: {desde}, Hasta: {hasta}");
            return;
        }

        var camino = BuscarRuta(inicio, fin);
        ultimaRutaCalculada = camino;

        if (camino == null)
        {
            Debug.LogError("❌ No se encontró una ruta válida.");
            return;
        }

        zonaActual = hasta;

        Sequence seq = DOTween.Sequence();

        for (int i = 0; i < camino.Count; i++)
        {
            var puntoDestino = camino[i].Punto.position;

            // 🟡 Detectar nodo anterior para log
            if (i > 0)
            {
                string desdeNodo = camino[i - 1].id;
                string hastaNodo = camino[i].id;
                float distLog = Vector3.Distance(camino[i - 1].Punto.position, camino[i].Punto.position);
                Debug.Log($"📏 Ruta: {desdeNodo} → {hastaNodo}, distancia: {distLog:F2} metros");
            }

            float distancia = Vector3.Distance(cameraTransform.position, puntoDestino);

            // ✅ Aplica duración mínima y máxima para evitar lentitud o saltos abruptos
            float duracionSegmento = Mathf.Clamp(distancia / velocidad, 0.3f, 1.5f); // entre 0.3s y 1.5s por segmento

            seq.Append(cameraTransform.DOMove(puntoDestino, duracionSegmento).SetEase(Ease.Linear));

            if (usarLookAt)
            {
                Quaternion rotacion;

                if (objetivoLookAt != null && i == camino.Count - 1)
                {
                    // Solo mirar al objetivo al llegar al último punto
                    Vector3 dir = (objetivoLookAt.position - puntoDestino).normalized;
                    rotacion = Quaternion.LookRotation(dir);
                    seq.Join(cameraTransform.DORotateQuaternion(rotacion, lookAheadTime).SetEase(Ease.Linear));
                }
                else if (i + 1 < camino.Count)
                {
                    // Mirar hacia el siguiente punto del camino
                    Vector3 siguiente = camino[i + 1].Punto.position;
                    Vector3 dir = (siguiente - puntoDestino).normalized;
                    rotacion = Quaternion.LookRotation(dir);
                    seq.Join(cameraTransform.DORotateQuaternion(rotacion, lookAheadTime).SetEase(Ease.Linear));
                }
            }
        }

        seq.OnComplete(() => onComplete?.Invoke());
    }



    private List<NodoCamara> BuscarRuta(NodoCamara inicio, NodoCamara destino)
    {
        var distancias = new Dictionary<NodoCamara, float>();
        var previos = new Dictionary<NodoCamara, NodoCamara>();
        var nodosPendientes = new List<NodoCamara>();

        foreach (var nodo in nodos)
        {
            distancias[nodo] = float.MaxValue;
            previos[nodo] = null;
            nodosPendientes.Add(nodo);
        }

        distancias[inicio] = 0;

        while (nodosPendientes.Count > 0)
        {
            // Seleccionar el nodo con menor distancia
            nodosPendientes.Sort((a, b) => distancias[a].CompareTo(distancias[b]));
            var actual = nodosPendientes[0];
            nodosPendientes.RemoveAt(0);

            if (actual == destino)
                break;

            foreach (var vecino in actual.vecinos)
            {
                float distancia = Vector3.Distance(actual.Punto.position, vecino.Punto.position);
                float nuevaDistancia = distancias[actual] + distancia;

                if (nuevaDistancia < distancias[vecino])
                {
                    distancias[vecino] = nuevaDistancia;
                    previos[vecino] = actual;
                }
            }
        }

        // Reconstruir ruta desde destino hacia inicio
        var ruta = new List<NodoCamara>();
        var nodoActual = destino;

        while (nodoActual != null)
        {
            ruta.Insert(0, nodoActual);
            nodoActual = previos[nodoActual];
        }

        // Si la ruta no empieza en inicio, no hay camino válido
        if (ruta.Count == 0 || ruta[0] != inicio)
            return null;

        return ruta;
    }


    public void SetZonaActual(string zona) => zonaActual = zona;

    [ContextMenu("🔍 Validar conexiones de nodos")]
    public void ValidarNodos()
    {
        var nodos = FindObjectsOfType<NodoCamara>();
        HashSet<string> ids = new();

        foreach (var nodo in nodos)
        {
            if (string.IsNullOrWhiteSpace(nodo.id))
            {
                Debug.LogError($"❌ Nodo sin ID: {nodo.name}");
            }
            else if (!ids.Add(nodo.id))
            {
                Debug.LogError($"❌ ID duplicado: {nodo.id} en {nodo.name}");
            }

            if (nodo.vecinos == null || nodo.vecinos.Count == 0)
            {
                Debug.LogWarning($"⚠ Nodo '{nodo.id}' no tiene vecinos conectados.");
            }
            else
            {
                foreach (var vecino in nodo.vecinos)
                {
                    if (vecino == null)
                    {
                        Debug.LogError($"❌ Nodo '{nodo.id}' tiene una conexión nula.");
                    }
                    else if (!vecino.vecinos.Contains(nodo))
                    {
                        Debug.LogWarning($"🔁 Nodo '{nodo.id}' conecta a '{vecino.id}' pero no es bidireccional.");
                    }
                }
            }
        }

        Debug.Log($"✅ Validación de {nodos.Length} nodos completada.");
    }

    [ContextMenu("🔍 Diagnóstico de todas las rutas")]
    public void DiagnosticarTodasLasRutas()
    {
        nodos = new List<NodoCamara>(FindObjectsOfType<NodoCamara>());

        if (nodos == null || nodos.Count == 0)
        {
            Debug.LogError("❌ No hay nodos registrados.");
            return;
        }

        foreach (var origen in nodos)
        {
            foreach (var destino in nodos)
            {
                if (origen == destino) continue;

                List<NodoCamara> ruta = BuscarRuta(origen, destino);

                if (ruta == null || ruta.Count == 0)
                {
                    Debug.LogError($"🚫 No hay ruta de '{origen.id}' a '{destino.id}'");
                }
                else
                {
                    string camino = $"🟢 Ruta de '{origen.id}' a '{destino.id}': ";
                    foreach (var nodo in ruta)
                    {
                        camino += nodo.id + " → ";
                    }
                    camino = camino.TrimEnd(' ', '→');
                    Debug.Log(camino);
                }
            }
        }

        Debug.Log("✅ Diagnóstico completo de todas las rutas.");
    }

    // 🟢 Dibujar la última ruta en la escena
    private void OnDrawGizmos()
    {
        if (ultimaRutaCalculada == null || ultimaRutaCalculada.Count < 2) return;

        Gizmos.color = Color.green;

        for (int i = 0; i < ultimaRutaCalculada.Count - 1; i++)
        {
            var from = ultimaRutaCalculada[i].Punto.position;
            var to = ultimaRutaCalculada[i + 1].Punto.position;
            Gizmos.DrawLine(from, to);
            Gizmos.DrawSphere(from, 0.15f);
        }

        Gizmos.DrawSphere(ultimaRutaCalculada[^1].Punto.position, 0.15f);
    }
}
