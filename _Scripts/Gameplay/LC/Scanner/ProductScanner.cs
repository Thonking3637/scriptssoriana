using UnityEngine;
using System.Collections.Generic;
using TMPro;
using System.Reflection;
using System;


#if UNITY_EDITOR
using UnityEditor;
#endif

public class ProductScanner : MonoBehaviour
{
    [Header("UI (se setea por actividad)")]
    public TextMeshProUGUI scannedProductsText;
    public TextMeshProUGUI totalPriceText;

    private readonly List<Product> scannedProducts = new();
    private float totalPrice = 0f;

    private ActivityBase currentActivity;

    public Action<GameObject> onReceiptScanned;

    private Product lastScannedGroupedProduct;

    [Header("Compatibilidad")]
    [SerializeField] private bool useLegacyReflection = false;

    // ✅ NUEVO flujo
    public event Action<DragObject> OnProductScanned;

    // =========================
    // 🔹 BIND / UNBIND
    // =========================
    public void Bind(ActivityBase activity, TextMeshProUGUI productsText, TextMeshProUGUI priceText, bool clearUI = true)
    {
        currentActivity = activity;
        scannedProductsText = productsText;
        totalPriceText = priceText;

        if (clearUI)
            ClearUI();
    }

    public void Unbind(ActivityBase activity)
    {
        if (currentActivity == activity)
            currentActivity = null;

        scannedProductsText = null;
        totalPriceText = null;
    }

    // =========================
    // 🔹 SCAN
    // =========================
    public void RegisterProductScan(DragObject product)
    {
        if (product == null || product.productData == null)
            return;

        Product existingProduct =
            scannedProducts.Find(p => p.productName == product.productData.productName);

        if (existingProduct != null)
        {
            existingProduct.quantity++;
            lastScannedGroupedProduct = existingProduct;
        }
        else
        {
            Product newProduct = ScriptableObject.CreateInstance<Product>();
            newProduct.Initialize(
                product.productData.code,
                product.productData.productName,
                product.productData.price,
                1
            );

            SaveProduct(newProduct);
            scannedProducts.Add(newProduct);
            lastScannedGroupedProduct = newProduct;
        }

        totalPrice += product.productData.price;

        UpdateUI();

        // ✅ NUEVO: evento limpio
        OnProductScanned?.Invoke(product);

        // 🧓 LEGACY (solo si está activo)
        if (useLegacyReflection)
            CallRegisterProductScanned();
    }

    // =========================
    // 🔹 UI / TOTAL
    // =========================
    private void UpdateUI()
    {
        if (scannedProductsText == null || totalPriceText == null)
        {
            Debug.LogWarning("[ProductScanner] UI no bindeada para esta actividad.");
            return;
        }

        scannedProductsText.text = "";

        foreach (var product in scannedProducts)
        {
            float subtotal = product.price * product.quantity;
            scannedProductsText.text +=
                $"{product.code} - {product.productName} - {product.quantity} - ${subtotal:F2}\n";
        }

        totalPriceText.text = $"${totalPrice:F2}";
    }

    public void ClearUI()
    {
        scannedProducts.Clear();
        totalPrice = 0f;
        lastScannedGroupedProduct = null;

        if (scannedProductsText != null) scannedProductsText.text = "";
        if (totalPriceText != null) totalPriceText.text = "$0.00";
    }

    // =========================
    // 🔹 PRICE UPDATE
    // =========================
    public void UpdateLastProductPrice(float newPrice)
    {
        if (lastScannedGroupedProduct == null) return;

        lastScannedGroupedProduct.price = newPrice;
        RecalculateTotalAndRefreshUI();
    }

    public void RecalculateTotalAndRefreshUI()
    {
        totalPrice = 0f;

        if (scannedProductsText != null)
            scannedProductsText.text = "";

        foreach (var product in scannedProducts)
        {
            float subtotal = product.price * product.quantity;
            totalPrice += subtotal;

            if (scannedProductsText != null)
                scannedProductsText.text +=
                    $"{product.code} - {product.productName} - {product.quantity} - ${subtotal:F2}\n";
        }

        if (totalPriceText != null)
            totalPriceText.text = $"${totalPrice:F2}";
    }

    // =========================
    // 🔹 RECEIPT
    // =========================
    public void RegisterReceiptScan(GameObject receiptObject)
    {
        onReceiptScanned?.Invoke(receiptObject);
    }

    // =========================
    // 🧓 LEGACY REFLECTION
    // =========================
    private void CallRegisterProductScanned()
    {
        if (currentActivity == null) return;

        MethodInfo method =
            currentActivity.GetType().GetMethod("RegisterProductScanned");

        if (method != null)
            method.Invoke(currentActivity, null);
        else
            Debug.LogWarning(
                $"[ProductScanner] {currentActivity.name} no tiene RegisterProductScanned()."
            );
    }

    // =========================
    // 💾 SAVE PRODUCT
    // =========================
    private void SaveProduct(Product newProduct)
    {
#if UNITY_EDITOR
        string path =
            $"Assets/Resources/ScannedProducts/{newProduct.productName}.asset";
        AssetDatabase.CreateAsset(newProduct, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
#else
        string jsonData = JsonUtility.ToJson(newProduct);
        PlayerPrefs.SetString(newProduct.productName, jsonData);
        PlayerPrefs.Save();
#endif
    }


    public void BindUI(ActivityBase activity, TextMeshProUGUI productsText, TextMeshProUGUI totalText, bool clear = true)
    {
        currentActivity = activity;
        scannedProductsText = productsText;
        totalPriceText = totalText;

        if (clear)
            ClearUI();
    }

    public void UnbindUI(ActivityBase activity)
    {
        if (currentActivity == activity)
            currentActivity = null;

        scannedProductsText = null;
        totalPriceText = null;
    }
}
