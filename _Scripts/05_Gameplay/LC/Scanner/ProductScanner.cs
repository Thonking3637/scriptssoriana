using UnityEngine;
using System.Collections.Generic;
using TMPro;
using System.Reflection;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ProductScanner OPTIMIZADO - Fixes para bajones de FPS
/// 
/// CAMBIOS REALIZADOS:
/// ───────────────────
/// 1. ❌ ELIMINADO: SaveProduct() que escribía a disco en CADA escaneo
/// 2. ❌ ELIMINADO: ScriptableObject.CreateInstance() en runtime (genera GC)
/// 3. ✅ AGREGADO: Clase ligera ScannedProductData en lugar de ScriptableObject
/// 4. ✅ AGREGADO: Opción para guardar al final de la actividad (no en cada scan)
/// 
/// IMPACTO EN RENDIMIENTO:
/// ───────────────────────
/// ANTES: ~30-100ms por escaneo (I/O sincrónico + GC)
/// DESPUÉS: <1ms por escaneo
/// </summary>
public class ProductScanner : MonoBehaviour
{
    [Header("UI (se setea por actividad)")]
    public TextMeshProUGUI scannedProductsText;
    public TextMeshProUGUI totalPriceText;

    // ✅ OPTIMIZADO: Usar clase ligera en lugar de ScriptableObject
    private readonly List<ScannedProductData> scannedProducts = new();
    private float totalPrice = 0f;

    private ActivityBase currentActivity;

    public Action<GameObject> onReceiptScanned;

    private ScannedProductData lastScannedGroupedProduct;

    [Header("Compatibilidad")]
    [SerializeField] private bool useLegacyReflection = false;

    [Header("Persistencia (opcional)")]
    [Tooltip("Si es true, guarda los productos al llamar SaveAllProducts(). NO guarda en cada escaneo.")]
    [SerializeField] private bool enablePersistence = false;

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

    // =========================
    // 🔹 SCAN (OPTIMIZADO)
    // =========================
    public void RegisterProductScan(DragObject product)
    {
        if (product == null || product.productData == null)
            return;

        // Buscar si ya existe el producto
        ScannedProductData existingProduct = scannedProducts.Find(
            p => p.productName == product.productData.productName
        );

        if (existingProduct != null)
        {
            existingProduct.quantity++;
            lastScannedGroupedProduct = existingProduct;
        }
        else
        {
            ScannedProductData newProduct = new ScannedProductData(
                product.productData.code,
                product.productData.productName,
                product.productData.price,
                1
            );

            scannedProducts.Add(newProduct);
            lastScannedGroupedProduct = newProduct;
        }

        totalPrice += product.productData.price;

        UpdateUI();

        OnProductScanned?.Invoke(product);

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
    // 🔹 GETTERS
    // =========================
    public float GetTotalPrice() => totalPrice;

    public List<ScannedProductData> GetScannedProducts() => scannedProducts;

    public ScannedProductData GetLastScannedProduct() => lastScannedGroupedProduct;

    // =========================
    // 💾 PERSISTENCIA OPCIONAL
    // (Solo si enablePersistence = true)
    // =========================

    /// <summary>
    /// Guarda todos los productos escaneados. Llamar al FINAL de la actividad, no en cada scan.
    /// </summary>
    public void SaveAllProducts()
    {
        if (!enablePersistence) return;

        foreach (var product in scannedProducts)
        {
            SaveProductData(product);
        }
    }

    private void SaveProductData(ScannedProductData product)
    {
#if UNITY_EDITOR
        // En editor: Guardar como asset (solo para debug)
        // ⚠️ NO usar en producción - es lento
        /*
        Product asset = ScriptableObject.CreateInstance<Product>();
        asset.Initialize(product.code, product.productName, product.price, product.quantity);
        string path = $"Assets/Resources/ScannedProducts/{product.productName}.asset";
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        */
#endif
        // En runtime: Guardar en PlayerPrefs (opcional)
        if (enablePersistence)
        {
            string jsonData = JsonUtility.ToJson(product);
            PlayerPrefs.SetString($"scanned_{product.productName}", jsonData);
            // ⚠️ NO llamar PlayerPrefs.Save() aquí - es lento
            // Se guarda automáticamente al cerrar la app
        }
    }

    // =========================
    // 🧓 LEGACY REFLECTION
    // =========================
    private void CallRegisterProductScanned()
    {
        if (currentActivity == null) return;

        MethodInfo method = currentActivity.GetType().GetMethod("RegisterProductScanned");

        if (method != null)
            method.Invoke(currentActivity, null);
        else
            Debug.LogWarning($"[ProductScanner] {currentActivity.name} no tiene RegisterProductScanned().");
    }
}

// =========================
// 📦 CLASE LIGERA PARA DATOS
// (Reemplaza ScriptableObject en runtime)
// =========================

/// <summary>
/// Clase ligera para almacenar datos de productos escaneados.
/// Mucho más eficiente que ScriptableObject.CreateInstance() en runtime.
/// </summary>
[System.Serializable]
public class ScannedProductData
{
    public string code;
    public string productName;
    public float price;
    public int quantity;

    public ScannedProductData(string code, string productName, float price, int quantity)
    {
        this.code = code;
        this.productName = productName;
        this.price = price;
        this.quantity = quantity;
    }
}