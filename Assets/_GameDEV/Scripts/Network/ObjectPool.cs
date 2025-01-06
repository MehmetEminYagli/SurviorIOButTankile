using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;

public class ObjectPool : NetworkBehaviour
{
    [System.Serializable]
    public class Pool
    {
        public string tag;
        public NetworkObject prefab;
        public int size;
    }

    [Header("Pool Settings")]
    [SerializeField] private List<Pool> pools;
    
    private Dictionary<string, Queue<NetworkObject>> poolDictionary;
    private Dictionary<NetworkObject, string> activeObjects;

    public static ObjectPool Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        poolDictionary = new Dictionary<string, Queue<NetworkObject>>();
        activeObjects = new Dictionary<NetworkObject, string>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            InitializePools();
        }
    }

    private void InitializePools()
    {
        foreach (Pool pool in pools)
        {
            Queue<NetworkObject> objectPool = new Queue<NetworkObject>();

            for (int i = 0; i < pool.size; i++)
            {
                NetworkObject obj = CreateNewPoolObject(pool.prefab);
                objectPool.Enqueue(obj);
            }

            poolDictionary.Add(pool.tag, objectPool);
        }
    }

    private NetworkObject CreateNewPoolObject(NetworkObject prefab)
    {
        NetworkObject obj = Instantiate(prefab);
        obj.gameObject.SetActive(false);
        obj.transform.SetParent(transform);
        return obj;
    }

    [ServerRpc(RequireOwnership = false)]
    public void SpawnFromPoolServerRpc(string tag, Vector3 position, Quaternion rotation, ServerRpcParams serverRpcParams = default)
    {
        if (!poolDictionary.ContainsKey(tag))
        {
            Debug.LogWarning($"Pool with tag {tag} doesn't exist.");
            return;
        }

        Queue<NetworkObject> pool = poolDictionary[tag];
        NetworkObject objectToSpawn;

        if (pool.Count == 0)
        {
            Pool poolInfo = pools.Find(p => p.tag == tag);
            objectToSpawn = CreateNewPoolObject(poolInfo.prefab);
        }
        else
        {
            objectToSpawn = pool.Dequeue();
        }

        objectToSpawn.transform.position = position;
        objectToSpawn.transform.rotation = rotation;
        objectToSpawn.gameObject.SetActive(true);
        objectToSpawn.Spawn();

        activeObjects[objectToSpawn] = tag;
        SpawnedObjectClientRpc(objectToSpawn.NetworkObjectId, position, rotation);
    }

    [ClientRpc]
    private void SpawnedObjectClientRpc(ulong networkId, Vector3 position, Quaternion rotation)
    {
        if (!IsServer)
        {
            NetworkObject obj = NetworkManager.Singleton.SpawnManager.SpawnedObjects[networkId];
            obj.transform.position = position;
            obj.transform.rotation = rotation;
            obj.gameObject.SetActive(true);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ReturnToPoolServerRpc(NetworkObjectReference objectRef)
    {
        if (objectRef.TryGet(out NetworkObject networkObject))
        {
            ReturnToPool(networkObject);
        }
    }

    private void ReturnToPool(NetworkObject obj)
    {
        if (!activeObjects.ContainsKey(obj))
        {
            Debug.LogWarning("Trying to return an object that wasn't spawned from the pool.");
            return;
        }

        string tag = activeObjects[obj];
        obj.gameObject.SetActive(false);
        poolDictionary[tag].Enqueue(obj);
        activeObjects.Remove(obj);
        
        if (obj.IsSpawned)
        {
            obj.Despawn();
        }
    }

    public void RegisterPrefab(string tag, NetworkObject prefab, int initialSize)
    {
        if (poolDictionary.ContainsKey(tag))
        {
            Debug.LogWarning($"Pool with tag {tag} already exists.");
            return;
        }

        Pool newPool = new Pool { tag = tag, prefab = prefab, size = initialSize };
        pools.Add(newPool);

        if (IsServer && IsSpawned)
        {
            Queue<NetworkObject> objectPool = new Queue<NetworkObject>();
            for (int i = 0; i < initialSize; i++)
            {
                NetworkObject obj = CreateNewPoolObject(prefab);
                objectPool.Enqueue(obj);
            }
            poolDictionary.Add(tag, objectPool);
        }
    }
} 