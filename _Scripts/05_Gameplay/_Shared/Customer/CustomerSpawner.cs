using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomerSpawner : MonoBehaviour
{
    [Header("Customer Configuration")]
    public Transform spawnPoint;
    public Transform checkoutPoint;
    public Transform pinEntryPoint;
    public List<Transform> exitPath;

    private readonly List<GameObject> activeCustomers = new List<GameObject>();
    private int lastCustomerIndex = -1;
    private ObjectPoolManager poolManager;
    private string suspendedCustomerPrefabName;

    [Header("Supervisor Settings")]
    public GameObject supervisorPrefab;
    public Transform supervisorSpawnPoint;
    public List<Transform> supervisorPath;

    public float ClientRotation = 270;

    private void Awake()
    {
        if (ObjectPoolManager.Instance != null)
        {
            poolManager = ObjectPoolManager.Instance;
        }
        else
        {
            Debug.LogWarning("ObjectPoolManager.Instance no estaba listo en Awake. Buscando...");
            StartCoroutine(WaitForPoolManager());
        }
    }

    private IEnumerator WaitForPoolManager()
    {
        yield return new WaitUntil(() => ObjectPoolManager.Instance != null);
        poolManager = ObjectPoolManager.Instance;
    }


    public GameObject SpawnCustomer()
    {
        if (poolManager == null)
        {
            Debug.LogError("PoolManager no está listo todavía.");
            return null;
        }

        GameObject prefab = poolManager.GetRandomPrefabFromPool(PoolTag.Cliente, ref lastCustomerIndex);

        if (prefab == null)
        {
            Debug.LogError("No se encontró prefab válido en el Pool de Clientes.");
            return null;
        }

        GameObject customer = poolManager.GetFromPool(PoolTag.Cliente, prefab.name);

        var key = customer.GetComponent<PoolKey>();
        if (key == null) key = customer.AddComponent<PoolKey>();
        key.Key = prefab.name;

        if (customer == null)
        {
            Debug.LogError("No hay clientes disponibles en el pool.");
            return null;
        }

        customer.transform.SetParent(null);
        customer.transform.position = spawnPoint.position;
        customer.transform.rotation = Quaternion.Euler(0, ClientRotation, 0);
        DOTween.Kill(customer.transform);

        customer.SetActive(true);
        activeCustomers.Add(customer);

        CustomerMovement movement = customer.GetComponent<CustomerMovement>();
        Animator animator = customer.GetComponent<Animator>();

        if (movement == null || animator == null)
        {
            Debug.LogError("Cliente sin componente necesario.");
            return null;
        }

        movement.Initialize(checkoutPoint, pinEntryPoint, exitPath, animator, this);
        movement.MoveToCheckout();

        return customer;
    }

    public void RemoveCustomer(GameObject customer)
    {
        if (customer == null) return;

        activeCustomers.Remove(customer);

        var key = customer.GetComponent<PoolKey>();
        string prefabName = (key != null && !string.IsNullOrEmpty(key.Key)) ? key.Key : customer.name;

        ObjectPoolManager.Instance.ReturnToPool(PoolTag.Cliente, prefabName, customer);
    }

    public GameObject SpawnSupervisorSequence(Action onArrived, Action onSecondPoint = null, Action onExit = null)
    {
        GameObject supervisor = Instantiate(supervisorPrefab, supervisorSpawnPoint.position, Quaternion.identity);

        supervisor.transform.SetParent(null);
        DOTween.Kill(supervisor.transform);
        supervisor.SetActive(true);

        CustomerMovement movement = supervisor.GetComponent<CustomerMovement>();
        Animator animator = supervisor.GetComponent<Animator>();

        if (movement == null || animator == null)
        {
            Debug.LogError("Supervisor sin componentes necesarios.");
            return null;
        }

        movement.Initialize(null, null, new List<Transform> { supervisorPath[0], supervisorPath[1], supervisorPath[2] }, animator);

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
   
    public void SpawnClientesEnSecuencia(int cantidad, Transform checkoutPoint, Action<List<GameObject>> onComplete)
    {
        StartCoroutine(SpawnClientesSecuencialCoroutine(cantidad, checkoutPoint, onComplete));
    }

    private IEnumerator SpawnClientesSecuencialCoroutine(int cantidad, Transform checkoutPoint, Action<List<GameObject>> onComplete)
    {
        List<GameObject> clientes = new List<GameObject>();
        float distanciaEntreClientes = 1.5f;

        for (int i = 0; i < cantidad; i++)
        {
            int index = i;

            GameObject cliente = SpawnCustomerWithoutMoving();

            cliente.transform.position = spawnPoint.position;
            cliente.transform.rotation = Quaternion.Euler(0, ClientRotation, 0);

            var mover = cliente.GetComponent<CustomerMovement>();
            if (mover != null)
            {
                mover.rotationClient = ClientRotation;

                Vector3 destino = checkoutPoint.position + checkoutPoint.right * distanciaEntreClientes * index;

                bool llego = false;
                mover.MoveToPosition(destino, ClientRotation, -1f, () => llego = true);

                yield return new WaitUntil(() => llego);
            }

            clientes.Add(cliente);
        }

        onComplete?.Invoke(clientes);
    }

    public GameObject SpawnCustomerByName(string prefabName)
    {
        if (poolManager == null)
        {
            Debug.LogError("PoolManager no está listo todavía.");
            return null;
        }

        GameObject customer = poolManager.GetFromPool(PoolTag.Cliente, prefabName);
        if (customer == null)
        {
            Debug.LogError("No hay clientes disponibles en el pool.");
            return null;
        }

        var key = customer.GetComponent<PoolKey>();
        if (key == null) key = customer.AddComponent<PoolKey>();
        key.Key = prefabName;

        customer.transform.SetParent(null);
        customer.transform.position = spawnPoint.position;
        customer.transform.rotation = Quaternion.Euler(0, 270, 0);
        DOTween.Kill(customer.transform);

        customer.SetActive(true);
        activeCustomers.Add(customer);

        CustomerMovement movement = customer.GetComponent<CustomerMovement>();
        Animator animator = customer.GetComponent<Animator>();

        if (movement == null || animator == null)
        {
            Debug.LogError("Cliente sin componente necesario.");
            return null;
        }

        movement.Initialize(checkoutPoint, pinEntryPoint, exitPath, animator, this);
        movement.MoveToCheckout();

        return customer;
    }

    public GameObject SpawnCustomerWithoutMoving()
    {
        if (poolManager == null)
        {
            Debug.LogError("PoolManager no está listo todavía.");
            return null;
        }

        GameObject prefab = poolManager.GetRandomPrefabFromPool(PoolTag.Cliente, ref lastCustomerIndex);

        if (prefab == null)
        {
            Debug.LogError("No se encontró prefab válido en el Pool de Clientes.");
            return null;
        }

        GameObject customer = poolManager.GetFromPool(PoolTag.Cliente, prefab.name);

        if (customer == null)
        {
            Debug.LogError("No hay clientes disponibles en el pool.");
            return null;
        }

        var key = customer.GetComponent<PoolKey>();
        if (key == null) key = customer.AddComponent<PoolKey>();
        key.Key = prefab.name;

        customer.transform.SetParent(null);
        customer.transform.position = spawnPoint.position;
        customer.transform.rotation = Quaternion.Euler(0, ClientRotation, 0);
        DOTween.Kill(customer.transform);

        customer.SetActive(true);
        activeCustomers.Add(customer);

        CustomerMovement movement = customer.GetComponent<CustomerMovement>();
        Animator animator = customer.GetComponent<Animator>();

        if (movement == null || animator == null)
        {
            Debug.LogError("Cliente sin componente necesario.");
            return null;
        }

        movement.Initialize(checkoutPoint, pinEntryPoint, exitPath, animator, this);

        return customer;
    }
}

