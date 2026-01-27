using UnityEngine;

[System.Serializable]
public class ProductoPicking
{
    public string numeroEstante;
    public string nombreEstante;
    public string nombreProducto;
    public string codigoProducto;
    public int cantidadSolicitada;
    public float precio;
    public Sprite imagen;

    public ProductoPicking(
        string numeroEstante,
        string nombreEstante,
        string nombreProducto,
        string codigoProducto,
        int cantidadSolicitada,
        float precio,
        Sprite imagen)
    {
        this.numeroEstante = numeroEstante;
        this.nombreEstante = nombreEstante;
        this.nombreProducto = nombreProducto;
        this.codigoProducto = codigoProducto;
        this.cantidadSolicitada = cantidadSolicitada;
        this.precio = precio;
        this.imagen = imagen;
    }
}
