using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Training/Store Catalog", fileName = "StoreCatalog")]
public class StoreCatalog: ScriptableObject
{
    [Serializable]
    public class Store
    {
        public string storeId;   // ej: SORI_001 (estable)
        public string storeName; // ej: CIUDAD DE MÉXICO (visible)
        public bool enabled = true;
    }

    public List<Store> stores = new();
}
