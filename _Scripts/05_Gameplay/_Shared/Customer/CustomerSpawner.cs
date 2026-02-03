using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CustomerSpawner OPTIMIZADO
/// - Cache de componentes para evitar GetComponent en cada spawn
/// - PoolKey ya debe estar en los prefabs (no AddComponent en runtime)
/// - DOTween.Kill optimizado
/// </summary>
public class CustomerSpawner : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════════════════
    // CONFIGURACIÓN
    // ═══════════════════════════════════════════════════════════════════════════

    [Header("Customer Configuration")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform checkoutPoint;
    [SerializeField] private Transform pinEntryPoint;
    [SerializeField] private List<Transform> exitPath;
    [SerializeField] private float clientRotation = 270f;

    [Header("Supervisor Settings")]
    [SerializeField] private GameObject supervisorPrefab;
    [SerializeField] private Transform supervisorSpawnPoint;
    [SerializeField] private List<Transform> supervisorPath;

    // ═══════════════════════════════════════════════════════════════════════════
    // PROPIEDADES PÚBLICAS (para compatibilidad)
    // ═══════════════════════════════════════════════════════════════════════════

    public Transform SpawnPoint => spawnPoint;
    public Transform CheckoutPoint => checkoutPoint;
    public Transform PinEntryPoint => pinEntryPoint;
    public List<Transform> ExitPath => exitPath;
    public float ClientRotation
    {
        get => clientRotation;
        set => clientRotation = value;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ESTADO INTERNO
    // ═══════════════════════════════════════════════════════════════════════════

    private readonly List<GameObject> _activeCustomers = new List<GameObject>();
    private int _lastCustomerIndex = -1;
    private ObjectPoolManager _poolManager;

    // ═══════════════════════════════════════════════════════════════════════════
    // CACHE DE COMPONENTES (evita GetComponent repetidos)
    // ═══════════════════════════════════════════════════════════════════════════

    private readonly Dictionary<GameObject, CachedCustomerComponents> _componentCache
        = new Dictionary<GameObject, CachedCustomerComponents>();

    private struct CachedCustomerComponents
    {
        public CustomerMovement Movement;
        public Animator Animator;
        public Client Client;
        public PoolKey PoolKey;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (ObjectPoolManager.Instance != null)
        {
            _poolManager = ObjectPoolManager.Instance;
        }
        else
        {
            StartCoroutine(WaitForPoolManager());
        }
    }

    private IEnumerator WaitForPoolManager()
    {
        yield return new WaitUntil(() => ObjectPoolManager.Instance != null);
        _poolManager = ObjectPoolManager.Instance;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SPAWN PRINCIPAL (OPTIMIZADO)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Spawnea un cliente aleatorio y lo mueve al checkout.
    /// Retorna el GameObject y los componentes cacheados.
    /// </summary>
    public GameObject SpawnCustomer()
    {
        if (_poolManager == null)
        {
            Debug.LogError("[CustomerSpawner] PoolManager no está listo.");
            return null;
        }

        // Obtener prefab aleatorio
        GameObject prefab = _poolManager.GetRandomPrefabFromPool(PoolTag.Cliente, ref _lastCustomerIndex);
        if (prefab == null)
        {
            Debug.LogError("[CustomerSpawner] No se encontró prefab en Pool de Clientes.");
            return null;
        }

        // Obtener del pool
        GameObject customer = _poolManager.GetFromPool(PoolTag.Cliente, prefab.name);
        if (customer == null)
        {
            Debug.LogError("[CustomerSpawner] No hay clientes disponibles en el pool.");
            return null;
        }

        // Configurar y activar
        return SetupAndActivateCustomer(customer, prefab.name, true);
    }

    /// <summary>
    /// Spawnea un cliente específico por nombre.
    /// </summary>
    public GameObject SpawnCustomerByName(string prefabName)
    {
        if (_poolManager == null)
        {
            Debug.LogError("[CustomerSpawner] PoolManager no está listo.");
            return null;
        }

        GameObject customer = _poolManager.GetFromPool(PoolTag.Cliente, prefabName);
        if (customer == null)
        {
            Debug.LogError($"[CustomerSpawner] No hay cliente '{prefabName}' disponible.");
            return null;
        }

        return SetupAndActivateCustomer(customer, prefabName, true);
    }

    /// <summary>
    /// Spawnea un cliente sin moverlo automáticamente.
    /// </summary>
    public GameObject SpawnCustomerWithoutMoving()
    {
        if (_poolManager == null)
        {
            Debug.LogError("[CustomerSpawner] PoolManager no está listo.");
            return null;
        }

        GameObject prefab = _poolManager.GetRandomPrefabFromPool(PoolTag.Cliente, ref _lastCustomerIndex);
        if (prefab == null)
        {
            Debug.LogError("[CustomerSpawner] No se encontró prefab en Pool de Clientes.");
            return null;
        }

        GameObject customer = _poolManager.GetFromPool(PoolTag.Cliente, prefab.name);
        if (customer == null)
        {
            Debug.LogError("[CustomerSpawner] No hay clientes disponibles en el pool.");
            return null;
        }

        return SetupAndActivateCustomer(customer, prefab.name, false);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SETUP OPTIMIZADO (CON CACHE)
    // ═══════════════════════════════════════════════════════════════════════════

    private GameObject SetupAndActivateCustomer(GameObject customer, string prefabName, bool moveToCheckout)
    {
        // Obtener componentes del cache o cachearlos
        var components = GetOrCacheComponents(customer);

        if (components.Movement == null || components.Animator == null)
        {
            Debug.LogError("[CustomerSpawner] Cliente sin componentes necesarios.");
            return null;
        }

        // Configurar PoolKey (ya debe existir en el prefab)
        if (components.PoolKey != null)
        {
            components.PoolKey.Key = prefabName;
        }
        else
        {
            Debug.LogWarning($"[CustomerSpawner] Cliente '{prefabName}' no tiene PoolKey. Agrégalo al prefab.");
        }

        // Posicionar
        customer.transform.SetParent(null);
        customer.transform.SetPositionAndRotation(
            spawnPoint.position,
            Quaternion.Euler(0, clientRotation, 0)
        );

        // Matar tweens anteriores de este objeto específicamente
        // Más eficiente que DOTween.Kill(transform) que busca en todos
        customer.transform.DOKill();

        // Activar
        customer.SetActive(true);
        _activeCustomers.Add(customer);

        // Inicializar movimiento
        components.Movement.Initialize(checkoutPoint, pinEntryPoint, exitPath, components.Animator, this);

        // Mover si es necesario
        if (moveToCheckout)
        {
            components.Movement.MoveToCheckout();
        }

        return customer;
    }

    /// <summary>
    /// Obtiene los componentes del cache o los cachea si no existen.
    /// </summary>
    private CachedCustomerComponents GetOrCacheComponents(GameObject customer)
    {
        if (_componentCache.TryGetValue(customer, out var cached))
        {
            return cached;
        }

        // Primera vez: cachear componentes
        var newCache = new CachedCustomerComponents
        {
            Movement = customer.GetComponent<CustomerMovement>(),
            Animator = customer.GetComponent<Animator>(),
            Client = customer.GetComponent<Client>(),
            PoolKey = customer.GetComponent<PoolKey>()
        };

        _componentCache[customer] = newCache;
        return newCache;
    }

    /// <summary>
    /// Obtiene los componentes cacheados de un cliente activo.
    /// Útil para las actividades que necesitan acceso a Movement, Client, etc.
    /// </summary>
    public bool TryGetCachedComponents(GameObject customer, out CustomerMovement movement, out Client client)
    {
        movement = null;
        client = null;

        if (customer == null) return false;

        if (_componentCache.TryGetValue(customer, out var cached))
        {
            movement = cached.Movement;
            client = cached.Client;
            return movement != null;
        }

        // Fallback: cachear y retornar
        var components = GetOrCacheComponents(customer);
        movement = components.Movement;
        client = components.Client;
        return movement != null;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // REMOVER CLIENTE
    // ═══════════════════════════════════════════════════════════════════════════

    public void RemoveCustomer(GameObject customer)
    {
        if (customer == null) return;

        _activeCustomers.Remove(customer);

        // Obtener nombre del pool desde cache
        string prefabName = customer.name;
        if (_componentCache.TryGetValue(customer, out var cached) && cached.PoolKey != null)
        {
            prefabName = cached.PoolKey.Key;
        }

        // Matar tweens antes de retornar al pool
        customer.transform.DOKill();

        ObjectPoolManager.Instance.ReturnToPool(PoolTag.Cliente, prefabName, customer);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SPAWN EN SECUENCIA
    // ═══════════════════════════════════════════════════════════════════════════

    public void SpawnClientesEnSecuencia(int cantidad, Transform targetCheckoutPoint, Action<List<GameObject>> onComplete)
    {
        StartCoroutine(SpawnClientesSecuencialCoroutine(cantidad, targetCheckoutPoint, onComplete));
    }

    private IEnumerator SpawnClientesSecuencialCoroutine(int cantidad, Transform targetCheckoutPoint, Action<List<GameObject>> onComplete)
    {
        List<GameObject> clientes = new List<GameObject>();
        float distanciaEntreClientes = 1.5f;

        for (int i = 0; i < cantidad; i++)
        {
            GameObject cliente = SpawnCustomerWithoutMoving();
            if (cliente == null) continue;

            cliente.transform.position = spawnPoint.position;
            cliente.transform.rotation = Quaternion.Euler(0, clientRotation, 0);

            if (TryGetCachedComponents(cliente, out var mover, out _))
            {
                mover.rotationClient = clientRotation;

                Vector3 destino = targetCheckoutPoint.position + targetCheckoutPoint.right * distanciaEntreClientes * i;

                bool llego = false;
                mover.MoveToPosition(destino, clientRotation, -1f, () => llego = true);

                yield return new WaitUntil(() => llego);
            }

            clientes.Add(cliente);
        }

        onComplete?.Invoke(clientes);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SUPERVISOR (NO USA POOL - Es único)
    // ═══════════════════════════════════════════════════════════════════════════

    public GameObject SpawnSupervisorSequence(Action onArrived, Action onSecondPoint = null, Action onExit = null)
    {
        if (supervisorPrefab == null || supervisorSpawnPoint == null)
        {
            Debug.LogError("[CustomerSpawner] Supervisor no configurado.");
            return null;
        }

        GameObject supervisor = Instantiate(supervisorPrefab, supervisorSpawnPoint.position, Quaternion.identity);
        supervisor.transform.SetParent(null);
        supervisor.SetActive(true);

        CustomerMovement movement = supervisor.GetComponent<CustomerMovement>();
        Animator animator = supervisor.GetComponent<Animator>();

        if (movement == null || animator == null)
        {
            Debug.LogError("[CustomerSpawner] Supervisor sin componentes necesarios.");
            Destroy(supervisor);
            return null;
        }

        movement.Initialize(null, null, new List<Transform> { supervisorPath[0], supervisorPath[1], supervisorPath[2] }, animator);

        // Secuencia de movimiento
        movement.MoveToPosition(supervisorPath[0].position, -90, -1f, () =>
        {
            onArrived?.Invoke();

            DOVirtual.DelayedCall(1f, () =>
            {
                movement.MoveToPosition(supervisorPath[1].position, -90, -1f, () =>
                {
                    onSecondPoint?.Invoke();

                    DOVirtual.DelayedCall(1f, () =>
                    {
                        movement.MoveToPosition(supervisorPath[2].position, -90, -1f, () =>
                        {
                            onExit?.Invoke();
                            Destroy(supervisor);
                        });
                    });
                });
            });
        });

        return supervisor;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // LIMPIEZA
    // ═══════════════════════════════════════════════════════════════════════════

    private void OnDestroy()
    {
        _componentCache.Clear();
        _activeCustomers.Clear();
    }
}