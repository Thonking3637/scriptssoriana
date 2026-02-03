using UnityEngine;
using System.Collections.Generic;

public enum PoolTag
{
    Cliente,
    Producto,
    Ticket
}

[System.Serializable]
public class Pool
{
    public PoolTag tag;
    public List<GameObject> prefabs;
    public int size;
}

public class ObjectPoolManager : MonoBehaviour
{
    public static ObjectPoolManager Instance;

    [SerializeField] private List<Pool> pools;

    private Dictionary<PoolTag, List<GameObject>> prefabMap = new();
    private Dictionary<PoolTag, Dictionary<string, Queue<GameObject>>> poolDictionary = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        InitializePools();
    }

    private void InitializePools()
    {
        foreach (Pool pool in pools)
        {
            prefabMap[pool.tag] = new List<GameObject>();
            poolDictionary[pool.tag] = new Dictionary<string, Queue<GameObject>>();

            foreach (GameObject prefab in pool.prefabs)
            {
                string key = prefab.name;
                prefabMap[pool.tag].Add(prefab);
                poolDictionary[pool.tag][key] = new Queue<GameObject>();

                for (int i = 0; i < pool.size; i++)
                {
                    GameObject obj = Instantiate(prefab);
                    obj.name = key;
                    obj.SetActive(false);
                    obj.transform.SetParent(this.transform);
                    poolDictionary[pool.tag][key].Enqueue(obj);
                }
            }
        }
    }

    public GameObject GetFromPool(PoolTag tag, string prefabName)
    {
        if (poolDictionary.TryGetValue(tag, out var prefabDict) && prefabDict.TryGetValue(prefabName, out var queue))
        {
            if (queue.Count > 0)
            {
                GameObject obj = queue.Dequeue();
                return obj;
            }
        }
        return null;
    }

    public void ReturnToPool(PoolTag tag, string prefabName, GameObject obj)
    {
        obj.SetActive(false);
        obj.transform.SetParent(this.transform);

        if (poolDictionary.ContainsKey(tag) && poolDictionary[tag].ContainsKey(prefabName))
        {
            poolDictionary[tag][prefabName].Enqueue(obj);
        }
    }

    public GameObject GetRandomPrefabFromPool(PoolTag tag, ref int lastIndex)
    {
        if (!prefabMap.ContainsKey(tag) || prefabMap[tag].Count == 0)
            return null;

        int newIndex;
        do
        {
            newIndex = Random.Range(0, prefabMap[tag].Count);
        } while (newIndex == lastIndex && prefabMap[tag].Count > 1);

        lastIndex = newIndex;
        return prefabMap[tag][newIndex];
    }

    public void ReturnToPool(GameObject obj)
    {
        foreach (var tag in poolDictionary.Keys)
        {
            foreach (var kvp in poolDictionary[tag])
            {
                string prefabName = kvp.Key;
                if (obj.name == prefabName)
                {
                    obj.SetActive(false);
                    obj.transform.SetParent(this.transform);
                    poolDictionary[tag][prefabName].Enqueue(obj);
                    return;
                }
            }
        }

        Debug.LogWarning($"No matching pool found to return: {obj.name}");
    }

    public List<string> GetAvailablePrefabNames(PoolTag tag)
    {
        if (!poolDictionary.ContainsKey(tag)) return new List<string>();

        var names = new List<string>();
        foreach (var kvp in poolDictionary[tag])
        {
            names.Add(kvp.Key);
        }
        return names;
    }

    public List<GameObject> GetAllUniquePrefabs(PoolTag tag)
    {
        if (prefabMap.ContainsKey(tag))
        {
            return prefabMap[tag];
        }

        return new List<GameObject>();
    }

}
