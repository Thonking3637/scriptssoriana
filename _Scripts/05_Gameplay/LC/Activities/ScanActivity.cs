using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;
using UnityEngine.UI;

public class ScanActivity : ActivityBase
{
    [Header("Configuración de Productos")]
    public Transform spawnPoint;
    public Transform endPoint;
    public ProductScanner scanner;

    [Header("Configuración de la Actividad")]
    public TextMeshProUGUI activityProductsText;
    public TextMeshProUGUI activityTotalPriceText;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI productsScannedText;

    [Header("Sub Botal Button")]
    public List<Button> subtotalbuttons;

    [Header("Configuración del Cliente")]
    public CustomerSpawner customerSpawner;

    private GameObject currentCustomer;
    private GameObject currentProduct;


    [HideInInspector] public int scannedCount = 0;
    [HideInInspector] public float timeElapsed = 0f;

    private int minProductsToScan = 18;
    private float totalActivityTime = 60f;
    private bool isActivityActive = true;
    private bool minProductsReached = false;
    private int lastProductIndex = -1;
    private float initialActivityTime = 60f;

    public override void StartActivity()
    {
        base.StartActivity();

        scanner.BindUI(this, activityProductsText, activityTotalPriceText, true);
        productNames = ObjectPoolManager.Instance.GetAvailablePrefabNames(PoolTag.Producto).ToArray();

        InitializeCommands();

        UpdateInstructionOnce(0, () =>
        {
            StartActivityScan();
        });
    }

    protected override void InitializeCommands()
    {
        base.InitializeCommands();

        CommandManager.CommandAction subtotalCommand = new CommandManager.CommandAction
        {
            command = "SUB_TOTAL",
            customAction = HandleSubtotalPressed,
            requiredActivity = "Day1_EscaneoProductos",
            commandButtons = subtotalbuttons,
        };
        commandManager.commandList.Add(subtotalCommand);
    }

    public void StartActivityScan()
    {
        currentCustomer = customerSpawner.SpawnCustomer();
        CustomerMovement customerMovement = currentCustomer.GetComponent<CustomerMovement>();

        cameraController.MoveToPosition("Actividad Escaneo", () =>
        {
            customerMovement.MoveToCheckout(() =>
            {
                UpdateInstructionOnce(1, () =>
                {
                    timerText.gameObject.SetActive(true);
                    productsScannedText.gameObject.SetActive(true);

                    timerText.enabled = true;
                    productsScannedText.enabled = true;

                    timerText.text = $"{totalActivityTime:F1} s";
                    productsScannedText.text = "0/18";

                    isActivityActive = true;

                    StartCoroutine(TimerCountdown());
                    SpawnNextProductFromPool();

                    if (activityMusicClip != null)
                    {
                        SoundManager.Instance.SetActivityMusic(activityMusicClip, 0.2f, false);
                    }
                });
            });
        });
    }

    public void HandleSubtotalPressed()
    {
        foreach (var button in subtotalbuttons)
        {
            button.gameObject.SetActive(true);
        }

        SoundManager.Instance.PlaySound("success");
    }

    private void SpawnNextProductFromPool()
    {
        if (!isActivityActive || currentProduct != null || scannedCount >= minProductsToScan) return;

        GameObject prefab = ObjectPoolManager.Instance.GetRandomPrefabFromPool(PoolTag.Producto, ref lastProductIndex);
        if (prefab == null)
        {
            Debug.LogWarning("No se encontró prefab válido para productos");
            return;
        }

        currentProduct = ObjectPoolManager.Instance.GetFromPool(PoolTag.Producto, prefab.name);
        if (currentProduct == null)
        {
            Debug.LogWarning($"No se encontró producto disponible para '{prefab.name}'");
            return;
        }

        currentProduct.transform.position = spawnPoint.position;
        currentProduct.transform.rotation = prefab.transform.rotation;
        currentProduct.transform.SetParent(null);
        currentProduct.SetActive(true);

        DragObject drag = currentProduct.GetComponent<DragObject>();
        drag?.SetOriginalPoolName(prefab.name);

        if (drag != null)
        {
            drag.OnScanned -= HandleProductScanned;
            drag.OnScanned += HandleProductScanned;
        }
    }

    private void HandleProductScanned(DragObject obj)
    {
        if (!isActivityActive || obj == null) return;

        obj.OnScanned -= HandleProductScanned;

        ObjectPoolManager.Instance.ReturnToPool(
            PoolTag.Producto,
            obj.OriginalPoolName,
            obj.gameObject
        );

        if (currentProduct == obj.gameObject)
            currentProduct = null;

        scannedCount++;
        UpdateScannedProductsUI();

        if (scannedCount >= minProductsToScan && !minProductsReached)
        {
            minProductsReached = true;
            SoundManager.Instance.PlaySound("success");
            EndActivity();
        }
        else
        {
            SpawnNextProductFromPool();
        }
    }

    public void RegisterProductScanned()
    {
        if (!isActivityActive) return;

        if (currentProduct != null)
        {
            ObjectPoolManager.Instance.ReturnToPool(PoolTag.Producto, currentProduct.name, currentProduct);
            currentProduct = null;
            scannedCount++;

            UpdateScannedProductsUI();

            if (scannedCount >= minProductsToScan && !minProductsReached)
            {
                minProductsReached = true;
                SoundManager.Instance.PlaySound("success");
                EndActivity();
            }
            else
            {
                SpawnNextProductFromPool();
            }
        }
    }

    private void UpdateScannedProductsUI()
    {
        if (productsScannedText != null)
        {
            productsScannedText.text = $"{scannedCount}/{minProductsToScan}";
        }
    }

    private IEnumerator TimerCountdown()
    {
        while (totalActivityTime > 0 && scannedCount < minProductsToScan)
        {
            yield return new WaitForSeconds(1f);
            totalActivityTime--;
            timerText.text = $"{totalActivityTime:F1} s";
        }

        EndActivity();
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // ✅ FIXED: Lógica de finalización modificada
    // ═════════════════════════════════════════════════════════════════════════════

    private void EndActivity()
    {
        StopAllCoroutines();
        isActivityActive = false;
        timeElapsed = initialActivityTime - totalActivityTime;

        CustomerMovement movement = currentCustomer.GetComponent<CustomerMovement>();
        movement?.MoveToExit();

        if (productsScannedText != null)
            productsScannedText.gameObject.SetActive(false);
        if (timerText != null)
            timerText.text = "";

        var adapter = GetComponent<ActivityMetricsAdapter>();
        if (adapter != null)
        {
            adapter.SetCustomMetrics(
                value1: scannedCount,
                total1: minProductsToScan,
                timeSeconds: timeElapsed,
                displayLine1: $"PRODUCTOS: {scannedCount}/{minProductsToScan}",
                displayLine2: $"TIEMPO: {timeElapsed:F1}s"
            );
            adapter.NotifyActivityCompleted();
        }
        else
        {
            base.CompleteActivity();
        }
    }

    private void NotifyAdapterCompleted()
    {
        SoundManager.Instance.RestorePreviousMusic();

        if (productsScannedText != null)
            productsScannedText.gameObject.SetActive(false);
        if (timerText != null)
            timerText.gameObject.SetActive(false);

        var adapter = GetComponent<ActivityMetricsAdapter>();
        if (adapter != null)
        {     
            Debug.Log($"[ScanActivity] {scannedCount}/{minProductsToScan} - {adapter.customMessage}");
            adapter.NotifyActivityCompleted();
        }
        else
        {
            base.CompleteActivity();
        }

    }

    protected override void Initialize()
    {

    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (scanner != null) scanner.UnbindUI(this);
    }
}