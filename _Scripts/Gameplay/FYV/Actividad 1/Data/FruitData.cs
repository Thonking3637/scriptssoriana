using System;
using UnityEngine;

public enum DefectoTipo { Golpe, Rajadura, Blandidura, Inconsistencia }

[Serializable]
public class FruitData
{
    public string nombre;
    public GameObject prefabBueno;
    public GameObject prefabMalo;
    public float pesoKg = 0.25f;
    public bool esDefectuoso;
    public DefectoTipo[] defectosReales;
}
