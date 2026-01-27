using UnityEngine;
using System;

public static class ReproEvents
{
    // HUD
    public static Action<JabaTipo, int> OnNivelActualizado;   // (zona, nivel 0..4)
    public static Action<JabaTipo, int> OnDespachoProcesado;  // (zona, total por zona)
    public static Action OnErrorDrop;                          // drop incorrecto

    // Ítem soltado desde el carrusel (scroll). Útil para métricas/debug.
    // (jabaTipo declarado en el ítem, productoTipo, GO arrastrado, área/slot detectados, validez)
    public static Action<JabaTipo, ProductoTipo, GameObject, UbicacionArea, UbicacionSlot, bool> OnConveyorItemDropped;
}

public static class TipoUtils
{
    // En esta actividad el JabaTipo del ítem debe coincidir con el tipo del área destino.
    public static bool Coincide(JabaTipo item, JabaTipo area) => item == area;
}
