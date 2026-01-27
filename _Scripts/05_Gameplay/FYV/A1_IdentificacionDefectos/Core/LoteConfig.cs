using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class LoteConfig
{
    public string nombreLote = "Caja 01";
    public float totalKg = 40f;
    public List<FruitData> items = new();

    [Serializable]
    public class TechSheetsPerLot
    {
        [Tooltip("Hasta 5 sprites (en orden de los 5 botones).")]
        public Sprite[] sprites = new Sprite[5];

        [Tooltip("Habilita/deshabilita cada bot�n para este lote (tama�o 5).")]
        public bool[] enabled = new bool[5] { true, true, true, true, false };
    }

    // En LoteConfig.cs
    public TechSheetsPerLot techSheets;
}
