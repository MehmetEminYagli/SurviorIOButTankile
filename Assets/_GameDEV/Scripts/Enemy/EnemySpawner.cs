using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using System.Collections;

public class EnemySpawner : NetworkBehaviour
{
    [SerializeField] private EnemyFactory enemyFactory;
    [SerializeField] private int maxEnemies = 10;
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private float minSpawnDistance = 20f;
    [SerializeField] private float maxSpawnDistance = 30f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float maxRayDistance = 50f;
    [SerializeField] private int maxSpawnAttempts = 5;

    private float lastSpawnTime;
    [SerializeField] private List<GameObject> activeEnemies = new List<GameObject>();
    [SerializeField] private List<NetworkObject> activePlayers = new List<NetworkObject>();
    private bool isInitialized = false;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) 
        {
            enabled = false;
            return;
        }
        
        Debug.Log("EnemySpawner OnNetworkSpawn called on Server");
        InitializeComponents();
        
        if (!isInitialized)
        {
            Debug.LogError("EnemySpawner failed to initialize!");
            enabled = false;
            return;
        }

        enabled = true;
        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
        
        // Host bağlandığında hemen spawning'i başlat
        if (NetworkManager.Singleton.IsHost)
        {
            Debug.Log("Host detected, starting enemy spawning...");
            HandleClientConnected(NetworkManager.Singleton.LocalClientId);
        }
    }

    private void InitializeComponents()
    {
        if (!isInitialized)
        {
            if (enemyFactory == null)
            {
                enemyFactory = GetComponent<EnemyFactory>();
                if (enemyFactory == null)
                {
                    Debug.LogError("EnemyFactory component is missing!");
                    return;
                }
            }

            isInitialized = true;
            Debug.Log("EnemySpawner initialized successfully");
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        Debug.Log($"HandleClientConnected called for client {clientId}");
        
        // Biraz bekle ve sonra oyuncu listesini güncelle (PlayerObject'in oluşması için)
        StartCoroutine(DelayedUpdatePlayerList());
    }

    private System.Collections.IEnumerator DelayedUpdatePlayerList()
    {
        // PlayerObject'in oluşması için biraz bekle
        yield return new WaitForSeconds(0.5f);
        
        UpdatePlayerList();
        
        // Eğer en az bir oyuncu varsa ve spawning başlamamışsa başlat
        if (activePlayers.Count > 0 && !IsInvoking(nameof(CheckAndSpawnEnemy)))
        {
            Debug.Log($"Starting enemy spawning with {activePlayers.Count} players");
            StartSpawning();
        }
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;
        
        UpdatePlayerList();
        
        // Eğer hiç oyuncu kalmadıysa spawning'i durdur
        if (activePlayers.Count == 0)
        {
            CancelInvoke(nameof(CheckAndSpawnEnemy));
            Debug.Log("Stopping enemy spawning - no players remaining");
        }
    }

    private void UpdatePlayerList()
    {
        Debug.Log($"UpdatePlayerList called. Previous player count: {activePlayers.Count}");
        activePlayers.Clear();
        
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager.Singleton is null!");
            return;
        }

        Debug.Log($"Connected clients count: {NetworkManager.Singleton.ConnectedClients.Count}");
        
        // Önce host/server'ı ekle
        if (NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            activePlayers.Add(NetworkManager.Singleton.LocalClient.PlayerObject);
            Debug.Log($"Added local player (Host/Server) to active players list. PlayerObject name: {NetworkManager.Singleton.LocalClient.PlayerObject.name}");
        }
        
        // Sonra diğer bağlı oyuncuları ekle
        foreach (var client in NetworkManager.Singleton.ConnectedClients.Values)
        {
            if (client != null && client.PlayerObject != null && 
                (NetworkManager.Singleton.LocalClient == null || client.ClientId != NetworkManager.Singleton.LocalClient.ClientId))
            {
                activePlayers.Add(client.PlayerObject);
                Debug.Log($"Added player {client.ClientId} to active players list. PlayerObject name: {client.PlayerObject.name}");
            }
            else
            {
                if (client == null)
                    Debug.LogWarning($"Found null client in ConnectedClients");
                else if (client.PlayerObject == null)
                    Debug.LogWarning($"Client {client.ClientId} has null PlayerObject");
            }
        }
        
        Debug.Log($"UpdatePlayerList completed. Current active players count: {activePlayers.Count}");
        
        // Her oyuncunun bilgilerini yazdır
        for (int i = 0; i < activePlayers.Count; i++)
        {
            var player = activePlayers[i];
            Debug.Log($"Player {i}: ID={player.NetworkObjectId}, Name={player.name}, Position={player.transform.position}");
        }
    }

    private void StartSpawning()
    {
        if (!IsServer) return;

        Debug.Log("Starting enemy spawning system");
        InvokeRepeating(nameof(CheckAndSpawnEnemy), 1f, spawnInterval);
        // Düşman hedeflerini güncelleme sistemini başlat
        InvokeRepeating(nameof(UpdateEnemyTargets), 1f, 1f);
    }

    private void UpdateEnemyTargets()
    {
        if (!IsServer || activePlayers.Count == 0) return;

        // Null olan düşmanları listeden temizle
        activeEnemies.RemoveAll(enemy => enemy == null);

        foreach (var enemy in activeEnemies)
        {
            if (enemy == null) continue;

            Vector3 enemyPosition = enemy.transform.position;
            Transform nearestPlayer = FindNearestPlayerTransform(enemyPosition);
            
            if (nearestPlayer != null)
            {
                IEnemy enemyComponent = enemy.GetComponent<IEnemy>();
                if (enemyComponent != null)
                {
                    enemyComponent.SetTarget(nearestPlayer);
                }
            }
        }
    }

    private void CheckAndSpawnEnemy()
    {
        if (!IsServer || activePlayers.Count == 0) return;

        if (activeEnemies.Count < maxEnemies)
        {
            SpawnEnemy();
        }

        activeEnemies.RemoveAll(enemy => enemy == null);
    }

    private void SpawnEnemy()
    {
        if (!IsServer)
        {
            Debug.LogWarning("Attempting to spawn enemy on non-server instance");
            return;
        }

        Debug.Log($"SpawnEnemy called. Active enemies count: {activeEnemies.Count}, Max enemies: {maxEnemies}");

        if (!isInitialized)
        {
            Debug.LogError("EnemySpawner not initialized!");
            return;
        }

        Vector3? validSpawnPosition = GetValidSpawnPosition();
        if (!validSpawnPosition.HasValue)
        {
            Debug.LogWarning("Could not find valid spawn position after all attempts");
            return;
        }

        Debug.Log($"Valid spawn position found at {validSpawnPosition.Value}");

        // Ground check
        RaycastHit hit;
        if (Physics.Raycast(validSpawnPosition.Value + Vector3.up * 2f, Vector3.down, out hit, 5f, groundLayer))
        {
            Debug.Log($"Ground found at {hit.point}");
        }
        else
        {
            Debug.LogWarning("No ground found at spawn position!");
            return;
        }

        EnemyFactory.EnemyType enemyType = Random.value < 0.7f ? 
            EnemyFactory.EnemyType.Ranged : EnemyFactory.EnemyType.Melee;

        GameObject enemy = enemyFactory.CreateEnemy(enemyType, validSpawnPosition.Value);
        if (enemy != null)
        {
            NetworkObject networkObject = enemy.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                try
                {
                    // NetworkObject ayarlarını güncelle
                    networkObject.DontDestroyWithOwner = true;
                    networkObject.AutoObjectParentSync = false;
                    
                    // Spawn öncesi görünürlük ayarlarını yap
                    enemy.SetActive(true);
                    
                    // Tüm renderer'ları aktif et
                    var renderers = enemy.GetComponentsInChildren<Renderer>(true);
                    foreach (var renderer in renderers)
                    {
                        renderer.enabled = true;
                    }

                    // Collider'ları aktif et
                    var colliders = enemy.GetComponentsInChildren<Collider>(true);
                    foreach (var collider in colliders)
                    {
                        collider.enabled = true;
                    }
                    
                    // Spawn işlemi
                    networkObject.Spawn();
                    
                    // Spawn sonrası tüm istemcilerde görünürlüğü sağla
                    ForceEnemyVisibilityClientRpc(networkObject.NetworkObjectId);

                    activeEnemies.Add(enemy);
                    Debug.Log($"Successfully spawned and networked {enemyType} enemy at {validSpawnPosition.Value}");

                    Transform nearestPlayer = FindNearestPlayerTransform(validSpawnPosition.Value);
                    if (nearestPlayer != null)
                    {
                        IEnemy enemyComponent = enemy.GetComponent<IEnemy>();
                        if (enemyComponent != null)
                        {
                            enemyComponent.SetTarget(nearestPlayer);
                            Debug.Log($"Set target for enemy: {nearestPlayer.name}");
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to spawn enemy: {e.Message}");
                    Destroy(enemy);
                }
            }
        }
    }

    [ClientRpc]
    private void ForceEnemyVisibilityClientRpc(ulong networkObjectId)
    {
        try
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject networkObject))
            {
                if (networkObject != null && networkObject.gameObject != null)
                {
                    GameObject enemyObject = networkObject.gameObject;
                    
                    // GameObject'i aktif et
                    enemyObject.SetActive(true);
                    
                    // Tüm renderer'ları aktif et
                    var renderers = enemyObject.GetComponentsInChildren<Renderer>(true);
                    foreach (var renderer in renderers)
                    {
                        renderer.enabled = true;
                    }

                    // Collider'ları aktif et
                    var colliders = enemyObject.GetComponentsInChildren<Collider>(true);
                    foreach (var collider in colliders)
                    {
                        collider.enabled = true;
                    }
                    
                    Debug.Log($"Client {NetworkManager.Singleton.LocalClientId}: Forced visibility for enemy {networkObjectId}");
                }
            }
            else
            {
                Debug.LogWarning($"Client {NetworkManager.Singleton.LocalClientId}: Could not find enemy with ID {networkObjectId}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Client {NetworkManager.Singleton.LocalClientId}: Error forcing enemy visibility: {e.Message}");
        }
    }

    // Düşmanların görünürlüğünü kontrol etmek için yeni bir metod
    private void CheckEnemyVisibility()
    {
        if (!IsServer) return;

        foreach (var enemy in activeEnemies)
        {
            if (enemy == null) continue;

            NetworkObject networkObject = enemy.GetComponent<NetworkObject>();
            if (networkObject != null && !networkObject.IsSpawned)
            {
                Debug.Log($"Respawning unspawned enemy: {enemy.name}");
                try
                {
                    networkObject.DontDestroyWithOwner = true;
                    networkObject.AutoObjectParentSync = false;
                    networkObject.Spawn();
                    ForceEnemyVisibilityClientRpc(networkObject.NetworkObjectId);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to respawn enemy: {e.Message}");
                }
            }
        }
    }

    private void Start()
    {
        if (IsServer)
        {
            // TransientArtifact uyarılarından kaçınmak için kısa bir gecikme ekle
            StartCoroutine(DelayedStart());
        }
    }

    private IEnumerator DelayedStart()
    {
        // Bir frame bekle
        yield return null;
        
        // Her 5 saniyede bir düşmanların görünürlüğünü kontrol et
        InvokeRepeating(nameof(CheckEnemyVisibility), 5f, 5f);
    }

    private Transform FindNearestPlayerTransform(Vector3 position)
    {
        if (activePlayers.Count == 0) return null;

        Transform nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (var playerObject in activePlayers)
        {
            if (playerObject == null) continue;

            float distance = Vector3.Distance(position, playerObject.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = playerObject.transform;
            }
        }

        return nearest;
    }

    private Vector3? GetValidSpawnPosition()
    {
        if (activePlayers.Count == 0) return null;

        // Rastgele bir oyuncu seç
        NetworkObject randomPlayer = activePlayers[Random.Range(0, activePlayers.Count)];
        if (randomPlayer == null) return null;

        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            // Oyuncunun etrafında rastgele bir yön seç
            Vector3 randomDirection = Random.insideUnitCircle.normalized;
            float randomDistance = Random.Range(minSpawnDistance, maxSpawnDistance);
            
            // Spawn pozisyonunu oyuncunun konumuna göre hesapla
            Vector3 spawnPosition = randomPlayer.transform.position + new Vector3(randomDirection.x, 0, randomDirection.y) * randomDistance;

            RaycastHit hit;
            Vector3 rayStart = spawnPosition + Vector3.up * maxRayDistance;
            if (Physics.Raycast(rayStart, Vector3.down, out hit, maxRayDistance * 2f, groundLayer))
            {
                UnityEngine.AI.NavMeshHit navHit;
                if (UnityEngine.AI.NavMesh.SamplePosition(hit.point, out navHit, 2f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    return navHit.position;
                }
            }
        }

        return null;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
            CancelInvoke(nameof(CheckAndSpawnEnemy));
            CancelInvoke(nameof(UpdateEnemyTargets)); // Hedef güncelleme sistemini durdur
        }
    }

    public override void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }
}