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

        Vector3 spawnPos = GetNextSpawnPoint();
        GameObject playerInstance = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();

        if (networkObject != null)
        {
            networkObject.SpawnAsPlayerObject(clientId);
            spawnedPlayers.Add(clientId);
            
            // MaterialManager'ı bul
            MaterialManager materialManager = playerInstance.GetComponent<MaterialManager>();
            if (materialManager != null)
            {
                // Her client için kendi materyal indeksini al
                int selectedMaterialIndex = LobbyManager.Instance.GetPlayerMaterialIndex(clientId);
                
                // Materyali uygula
                materialManager.ApplyMaterialByIndex(selectedMaterialIndex);
                
                // Materyalin uygulanmasını bekle
                materialManager.ForceUpdateMaterials();
                
                // Rengi doğrudan LobbyManager'dan al
                Color playerColor = LobbyManager.Instance.GetPreviewColorByIndex(selectedMaterialIndex);
                Debug.Log($"Selected material index for client {clientId}: {selectedMaterialIndex}");
                Debug.Log($"Player color from LobbyManager: R:{playerColor.r}, G:{playerColor.g}, B:{playerColor.b}, A:{playerColor.a}");

                // Alpha değerini 1 olarak ayarla
                playerColor.a = 1f;

                // Spawn efektini tüm clientlara bildir
                NotifySpawnEffectClientRpc(clientId, spawnPos, selectedMaterialIndex, playerColor);
            }
            else
            {
                Debug.LogWarning("MaterialManager component not found on player instance!");
            }

            Debug.Log($"Spawned player for client {clientId} at position {spawnPos}");
        }
        else
        {
            Debug.LogError($"NetworkObject component not found on player prefab!");
            Destroy(playerInstance);
        }
    }

    [ClientRpc]
    private void NotifySpawnEffectClientRpc(ulong clientId, Vector3 spawnPos, int materialIndex, Color playerColor)
    {
        if (SpawnEffectManager.Instance != null)
        {
            // Eğer bu client kendi efektini spawn ediyorsa, kendi rengini kullan
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                Color localColor = LobbyManager.Instance.GetPreviewColorByIndex(
                    LobbyManager.Instance.GetLocalPlayerMaterialIndex());
                localColor.a = 1f;
                SpawnEffectManager.Instance.PlaySpawnEffect(clientId, spawnPos, localColor);
            }
            else
            {
                // Diğer oyuncular için server'dan gelen rengi kullan
                SpawnEffectManager.Instance.PlaySpawnEffect(clientId, spawnPos, playerColor);
            }
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