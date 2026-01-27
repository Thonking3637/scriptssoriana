using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Collections.Generic;

public class DropZone : MonoBehaviour, IDropHandler
{
    [Header("Validación de IDs")]
    public List<string> idsEsperados = new(); // puedes poner solo uno si quieres
    public bool asignadoCorrectamente = false; // usado en BolsaSeleccionActivity
    public int cantidadEsperada = 1; // usado en entregas múltiples

    private int entregasActuales = 0;

    public Action onDropCorrect;

    public bool EsZonaCorrecta(ArrastrableBase arrastrable)
    {
        return idsEsperados.Contains(arrastrable.id);
    }

    public bool EsZonaCompleta()
    {
        return entregasActuales >= cantidadEsperada;
    }

    public void RegistrarEntregaCorrecta()
    {
        entregasActuales++;
        asignadoCorrectamente = true;

        if (entregasActuales >= cantidadEsperada)
        {
            onDropCorrect?.Invoke();
        }
    }

    public void ResetZona()
    {
        entregasActuales = 0;
        asignadoCorrectamente = false;
    }

    public void OnDrop(PointerEventData eventData)
    {
        // La lógica se maneja desde ArrastrableBase
    }

}
