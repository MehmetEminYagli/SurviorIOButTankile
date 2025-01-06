using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class NetworkSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform[] spawnPoints;
    
    private List<Transform> availableSpawnPoints;
    private HashSet<ulong> spawnedPlayers = new HashSet<ulong>();

    [Header("Object Pooling")]
    [SerializeField] private ObjectPool objectPoolPrefab;

    private void Start()
    {
        if (IsServer)
        {
            InitializeSpawnPoints();
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            SpawnExistingPlayers();
        }
    }

    private void InitializeSpawnPoints()
    {
        availableSpawnPoints = new List<Transform>(spawnPoints);
        if (availableSpawnPoints.Count == 0)
        {
            Debug.LogError("No spawn points assigned to NetworkSpawner!");
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        
        if (!spawnedPlayers.Contains(clientId))
        {
            SpawnPlayer(clientId);
        }
    }

    private void SpawnExistingPlayers()
    {
        if (!IsServer) return;

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (!spawnedPlayers.Contains(clientId))
            {
                SpawnPlayer(clientId);
            }
        }
    }

    private void SpawnPlayer(ulong clientId)
    {
        if (!IsServer) return;

        Vector3 spawnPosition = GetNextSpawnPoint();
        GameObject playerInstance = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
        
        NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            networkObject.SpawnAsPlayerObject(clientId, true);
            spawnedPlayers.Add(clientId);
            Debug.Log($"Spawned player for client {clientId} at position {spawnPosition}");
        }
        else
        {
            Debug.LogError($"PlayerPrefab does not have a NetworkObject component!");
            Destroy(playerInstance);
        }
    }

    private Vector3 GetNextSpawnPoint()
    {
        if (availableSpawnPoints.Count == 0)
        {
            InitializeSpawnPoints();
        }

        if (availableSpawnPoints.Count == 0)
        {
            Debug.LogWarning("No spawn points available, using default position!");
            return Vector3.zero;
        }

        int randomIndex = Random.Range(0, availableSpawnPoints.Count);
        Transform spawnPoint = availableSpawnPoints[randomIndex];
        availableSpawnPoints.RemoveAt(randomIndex);

        return spawnPoint.position;
    }

    public override void OnDestroy()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        }
        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (IsServer)
        {
            // Object Pool'u oluştur
            if (objectPoolPrefab != null)
            {
                NetworkObject poolObj = Instantiate(objectPoolPrefab).GetComponent<NetworkObject>();
                poolObj.Spawn();
            }
        }
    }
} 