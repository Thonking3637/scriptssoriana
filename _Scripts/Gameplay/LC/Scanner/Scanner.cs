using UnityEngine;
using TMPro;

public class Scanner : MonoBehaviour
{
    public TextMeshProUGUI scannedProductsText;
    public TextMeshProUGUI totalText;
    private float totalAmount = 0f;

    public void ScanProduct(Product product)
    {
        if (product == null) return;

        scannedProductsText.text += $"{product.productName} - {product.code} - {product.quantity} - ${product.price:F2}\n";

        totalAmount += product.price;
        totalText.text = $"${totalAmount:F2}";
    }
}
