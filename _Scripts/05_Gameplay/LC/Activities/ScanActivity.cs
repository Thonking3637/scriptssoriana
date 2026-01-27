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

    [Header("Paneles de Resultado")]
    public GameObject failurePanel;
    public TextMeshProUGUI failureText;
    public TextMeshProUGUI successText;
    public TextMeshProUGUI remainingTimeText;
    public Button continueButton;
    public Button restartButton;
    public List<Button> subtotalbuttons;

    [Header("Configuración del Cliente")]
    public CustomerSpawner customerSpawner;
    private GameObject currentCustomer;

    private GameObject currentProduct;
    private int scannedCount = 0;
    private int minProductsToScan = 18;
    private float totalActivityTime = 60f;
    private bool isActivityActive = true;
    private bool minProductsReached = false;
    private int lastProductIndex = -1;

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
            Debug.LogWarning("❌ No se encontró prefab válido para productos");
            return;
        }

        currentProduct = ObjectPoolManager.Instance.GetFromPool(PoolTag.Producto, prefab.name);
        if (currentProduct == null)
        {
            Debug.LogWarning($"❌ No se encontró producto disponible para '{prefab.name}'");
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

        obj.OnScanned -= HandleProductScanned; // anti pool-dup

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

    private void EndActivity()
    {
        StopAllCoroutines();
        isActivityActive = false;

        CustomerMovement movement = currentCustomer.GetComponent<CustomerMovement>();
        movement?.MoveToExit();

        if (scannedCount < minProductsToScan)
        {
            ShowFailurePanel();
        }
        else
        {
            cameraController.MoveToPosition("Actividad Escaneo Subtotal", () =>
            {
                AnimateButtonsSequentiallyWithActivation(subtotalbuttons, ShowSuccessPanel);
            });
        }
    }

    private void ShowFailurePanel()
    {
        SoundManager.Instance.RestorePreviousMusic();
        SoundManager.Instance.PlaySound("tryagain");

        failureText.text = $"{scannedCount}/{minProductsToScan}";
        failurePanel.SetActive(true);
        failurePanel.transform.localScale = Vector3.zero;
        failurePanel.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);

        restartButton.onClick.RemoveAllListeners();
        restartButton.onClick.AddListener(() =>
        {
            failurePanel.transform.DOScale(Vector3.zero, 0.3f).OnComplete(() =>
            {
                failurePanel.SetActive(false);
                RestartActivity();
            });
        });
    }

    private void ShowSuccessPanel()
    {
        SoundManager.Instance.RestorePreviousMusic();
        productsScannedText.text = "0/18";
        productsScannedText.gameObject.SetActive(false);
        timerText.gameObject.SetActive(false);
        successText.text = $"{scannedCount}";
        remainingTimeText.text = $"{totalActivityTime:F1} s";
      
        cameraController.MoveToPosition("Actividad Escaneo Successful", () =>
        {
            SoundManager.Instance.PlaySound("win");

            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(() =>
            {
                ContinueToNextActivity();
            });
        });
    }

    private void ContinueToNextActivity()
    {
        cameraController.MoveToPosition("Iniciando Juego", () =>
        {
            CompleteActivity();
        });
    }

    private void RestartActivity()
    {
        scannedCount = 0;
        totalActivityTime = 60f;
        isActivityActive = true;
        minProductsReached = false;
        timerText.text = $"{totalActivityTime:F1} s";
        productsScannedText.text = "0/18";

        if (currentProduct != null)
        {
            ObjectPoolManager.Instance.ReturnToPool(PoolTag.Producto, currentProduct.name, currentProduct);
            currentProduct = null;
        }

        StartActivity();
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
