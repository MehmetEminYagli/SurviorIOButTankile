using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using System;

public class NetworkRoomManager : NetworkBehaviour
{
    public static NetworkRoomManager Instance { get; private set; }

    [Header("Room Settings")]
    [SerializeField] private int minPlayersToStart = 2;
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform[] spawnPoints;

    private NetworkVariable<int> connectedPlayers = new NetworkVariable<int>();
    private Dictionary<ulong, NetworkPlayer> players = new Dictionary<ulong, NetworkPlayer>();
    private HashSet<ulong> spawnedPlayers = new HashSet<ulong>();

    public event Action<int> OnPlayerCountChanged;
    public event Action OnGameStarted;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            // Disable auto-spawn on NetworkManager
            NetworkManager.Singleton.NetworkConfig.PlayerPrefab = null;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        connectedPlayers.OnValueChanged += HandlePlayerCountChanged;
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        // Check if player is already spawned
        if (spawnedPlayers.Contains(clientId))
        {
            Debug.LogWarning($"Player {clientId} is already spawned!");
            return;
        }

        // Increment player count
        connectedPlayers.Value++;

        // Spawn player for the client
        SpawnPlayerForClient(clientId);

        // Check if we can start the game
        CheckGameStart();
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        // Remove player from tracking
        if (players.ContainsKey(clientId))
        {
            players.Remove(clientId);
            spawnedPlayers.Remove(clientId);
        }

        // Decrement player count
        connectedPlayers.Value--;
    }

    private void HandlePlayerCountChanged(int previousValue, int newValue)
    {
        OnPlayerCountChanged?.Invoke(newValue);
    }

    private void SpawnPlayerForClient(ulong clientId)
    {
        // Check if player is already spawned
        if (spawnedPlayers.Contains(clientId))
        {
            Debug.LogWarning($"Attempted to spawn player {clientId} multiple times!");
            return;
        }

        // Find an available spawn point
        Transform spawnPoint = GetRandomSpawnPoint();

        // Spawn the player
        GameObject playerObj = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
        NetworkObject networkObject = playerObj.GetComponent<NetworkObject>();
        
        if (networkObject != null)
        {
            networkObject.SpawnAsPlayerObject(clientId);
            
            // Track the player
            NetworkPlayer networkPlayer = playerObj.GetComponent<NetworkPlayer>();
            if (networkPlayer != null)
            {
                players[clientId] = networkPlayer;
                spawnedPlayers.Add(clientId);
            }
        }
        else
        {
            Debug.LogError("NetworkObject component missing from player prefab!");
            Destroy(playerObj);
        }
    }

    private Transform GetRandomSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("No spawn points configured!");
            return transform;
        }

        return spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
    }

    private void CheckGameStart()
    {
        if (connectedPlayers.Value >= minPlayersToStart)
        {
            StartGameServerRpc();
        }
    }

    [ServerRpc]
    private void StartGameServerRpc()
    {
        StartGameClientRpc();
    }

    [ClientRpc]
    private void StartGameClientRpc()
    {
        OnGameStarted?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        }

        connectedPlayers.OnValueChanged -= HandlePlayerCountChanged;
    }

    public bool CanStartGame()
    {
        return connectedPlayers.Value >= minPlayersToStart;
    }

    public NetworkPlayer GetPlayer(ulong clientId)
    {
        return players.TryGetValue(clientId, out NetworkPlayer player) ? player : null;
    }
} 