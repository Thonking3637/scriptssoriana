using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "SemanaGuia", menuName = "Recibo/SemanaGuiaSO")]
public class SemanaGuiaSO : ScriptableObject
{
    [System.Serializable]
    public class CasoDia
    {
        public string dia;           // "Lunes", "Martes", etc.
        public string producto;      // "Papa", "Cebolla", etc.
        public int kg;               // 5
        public bool registradoApp;   // true = Sí, false = No
    }

    public List<CasoDia> casos = new List<CasoDia>();
}
