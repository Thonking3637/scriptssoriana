using UnityEngine;

[CreateAssetMenu(fileName = "ProductoCatalog", menuName = "Merma/Producto", order = 0)]
public class ProductoCatalogSO : ScriptableObject
{
    [Header("Identidad")]
    public string nombre;
    public ProductoTipo tipoProducto;

    [Header("Visual")]
    [Tooltip("Prefab unidad para llenado procedural (CrateFillerVisual)")]
    public GameObject prefabUnidad;
    [Tooltip("Prefab pre-llenado (alternativa simple y segura)")]
    public GameObject prefabLleno;

    [Header("Peso simulado")]
    public Vector2 rangoPesoKg = new Vector2(0.8f, 2.5f);

    public float GetPesoAleatorio() => Random.Range(rangoPesoKg.x, rangoPesoKg.y);
}
