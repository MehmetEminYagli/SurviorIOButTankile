using Unity.Netcode;
using UnityEngine;
using System;

[RequireComponent(typeof(NetworkManager))]
public class NetworkManagerController : MonoBehaviour
{
    public static NetworkManagerController Instance { get; private set; }

    [Header("Network Settings")]
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private string gameVersion = "1.0";

    public event Action OnClientConnected;
    public event Action OnClientDisconnected;
    public event Action OnHostStarted;

    private NetworkManager networkManager;
    private int currentConnections = 0;

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
            return;
        }

        InitializeNetworkManager();
    }

    private void InitializeNetworkManager()
    {
        networkManager = GetComponent<NetworkManager>();
        
        if (networkManager == null)
        {
            Debug.LogError("NetworkManager component is missing! Adding one...");
            networkManager = gameObject.AddComponent<NetworkManager>();
        }

        // Configure NetworkManager settings
        networkManager.ConnectionApprovalCallback += ApproveConnection;
        SetupNetworkCallbacks();
    }

    private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        // Check if we can accept more players
        bool shouldApprove = currentConnections < maxPlayers;
        response.Approved = shouldApprove;
        
        if (shouldApprove)
        {
            currentConnections++;
            response.CreatePlayerObject = true;
        }
        else
        {
            Debug.LogWarning($"Connection rejected: Max players ({maxPlayers}) reached");
        }
    }

    private void SetupNetworkCallbacks()
    {
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }
        else
        {
            Debug.LogError("NetworkManager is null in SetupNetworkCallbacks!");
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        Debug.Log($"Client connected: {clientId}");
        OnClientConnected?.Invoke();
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client disconnected: {clientId}");
        currentConnections = Mathf.Max(0, currentConnections - 1);
        OnClientDisconnected?.Invoke();
    }

    public void StartHost() 
    {
        if (networkManager != null)
        {
            currentConnections = 1; // Host counts as first connection
            networkManager.StartHost();
            OnHostStarted?.Invoke();
        }
        else
        {
            Debug.LogError("Cannot start host: NetworkManager is null!");
        }
    }

    public void StartClient()
    {
        if (networkManager != null)
        {
            networkManager.StartClient();
        }
        else
        {
            Debug.LogError("Cannot start client: NetworkManager is null!");
        }
    }

    public void DisconnectClient()
    {
        if (networkManager != null)
        {
            networkManager.Shutdown();
            currentConnections = 0;
        }
        else
        {
            Debug.LogError("Cannot disconnect: NetworkManager is null!");
        }
    }

    private void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.ConnectionApprovalCallback -= ApproveConnection;
            networkManager.OnClientConnectedCallback -= HandleClientConnected;
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }
} 