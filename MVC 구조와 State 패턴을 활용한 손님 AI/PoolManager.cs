using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class PoolManager : Singleton<PoolManager>
{
    // pool settings
    private const int DEFAULT_CAPACITY = 10;
    private const int MAX_SIZE = 30;
    
    private readonly Dictionary<GameObject, IObjectPool<GameObject>> pools = new();
    private Transform poolContainer;

    protected override void Awake()
    {
        base.Awake();
        poolContainer = new GameObject("PoolContainer").transform;
        poolContainer.SetParent(transform);
    }

    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (!pools.ContainsKey(prefab))
        {
            CreatePoolFor(prefab);
        }
        
        GameObject instance = pools[prefab].Get();
        instance.transform.SetPositionAndRotation(position, rotation);
        return instance;
    }

    public void Release(GameObject prefab, GameObject instance)
    {
        if (!pools.ContainsKey(prefab))
        {
            Destroy(instance);
            return;
        }
        pools[prefab].Release(instance);
    }
    
    private void CreatePoolFor(GameObject prefab)
    {
        var newPool = new ObjectPool<GameObject>(
            createFunc: () => Instantiate(prefab, poolContainer),
            actionOnGet: (obj) => 
            {
                obj.SetActive(true);
                obj.GetComponent<IPoolable>()?.OnGetFromPool();
            },
            actionOnRelease: (obj) => 
            {
                obj.GetComponent<IPoolable>()?.OnReturnToPool();
                obj.SetActive(false);
            },
            actionOnDestroy: (obj) => Destroy(obj),
            collectionCheck: true,
            defaultCapacity: DEFAULT_CAPACITY,
            maxSize: MAX_SIZE
        );
        pools.Add(prefab, newPool);
    }
}