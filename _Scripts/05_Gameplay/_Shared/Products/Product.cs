using UnityEngine;

[CreateAssetMenu(fileName = "NewProduct", menuName = "Inventory/Product")]
public class Product : ScriptableObject
{
    public string productName;
    public float price;
    public string code;
    public int quantity;

    public void Initialize(string codigo, string nombre, float precio, int cantidad)
    {
        this.code = codigo;
        this.productName = nombre;
        this.price = precio;
        this.quantity = cantidad;
    }

    private void OnEnable()
    {
        GenerateRandomCode();
        GenerateRandomPrice();
    }

    private void GenerateRandomCode()
    {
        code = Random.Range(1000, 9999).ToString();
    }

    private void GenerateRandomPrice()
    {
        price = Random.Range(20, 50); 
    }

    public void RegenerateValues()
    {
        GenerateRandomCode();
        GenerateRandomPrice();
    }
}
