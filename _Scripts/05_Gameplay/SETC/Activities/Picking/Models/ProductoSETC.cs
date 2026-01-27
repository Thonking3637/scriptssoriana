using UnityEngine;

public enum TipoBolsa
{
    Comestible,
    NoComestible,
    Congelado
}

[System.Serializable]
public class ProductoSETC
{
    public string nombre;
    public Sprite imagen;
    public TipoBolsa tipo;
    public float peso;
}
