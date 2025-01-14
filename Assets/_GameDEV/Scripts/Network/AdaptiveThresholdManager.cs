using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;

public class AdaptiveThresholdManager : NetworkBehaviour
{
    public static AdaptiveThresholdManager Instance { get; private set; }

    [Header("Threshold Settings")]
    [SerializeField] private float baseThreshold = 0.1f;
    [SerializeField] private float minThreshold = 0.05f;
    [SerializeField] private float maxThreshold = 0.5f;

    [Header("Network Conditions")]
    [SerializeField] private float congestionThreshold = 100f; // ms
    [SerializeField] private int bufferSize = 10;

    private NetworkPerformanceManager performanceManager;
    private Dictionary<ulong, float> clientThresholds = new Dictionary<ulong, float>();
    private Dictionary<ulong, Queue<float>> latencyBuffers = new Dictionary<ulong, Queue<float>>();
    private NetworkVariable<float> globalThreshold = new NetworkVariable<float>();

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

        performanceManager = GetComponent<NetworkPerformanceManager>();
        if (!performanceManager)
        {
            Debug.LogError("NetworkPerformanceManager bulunamadı!");
            return;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            globalThreshold.Value = baseThreshold;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        if (!clientThresholds.ContainsKey(clientId))
        {
            clientThresholds[clientId] = baseThreshold;
            latencyBuffers[clientId] = new Queue<float>();
        }
        UpdateClientThresholdClientRpc(clientId, baseThreshold);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        clientThresholds.Remove(clientId);
        latencyBuffers.Remove(clientId);
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned) return;

        foreach (var clientId in NetworkManager.ConnectedClientsIds)
        {
            if (clientId == NetworkManager.LocalClientId) continue;

            UpdateClientThreshold(clientId);
        }

        UpdateGlobalThreshold();
    }

    private void UpdateClientThreshold(ulong clientId)
    {
        if (!clientThresholds.ContainsKey(clientId) || !latencyBuffers.ContainsKey(clientId))
        {
            OnClientConnected(clientId);
            return;
        }

        var rtt = NetworkManager.NetworkConfig.NetworkTransport.GetCurrentRtt(clientId);
        var buffer = latencyBuffers[clientId];

        buffer.Enqueue(rtt);
        if (buffer.Count > bufferSize)
        {
            buffer.Dequeue();
        }

        float avgLatency = 0f;
        foreach (var latency in buffer)
        {
            avgLatency += latency;
        }
        avgLatency /= buffer.Count;

        float currentThreshold = clientThresholds[clientId];
        float targetThreshold = baseThreshold;

        if (avgLatency > congestionThreshold)
        {
            targetThreshold = Mathf.Min(currentThreshold * 1.2f, maxThreshold);
        }
        else if (avgLatency < congestionThreshold * 0.5f)
        {
            targetThreshold = Mathf.Max(currentThreshold * 0.8f, minThreshold);
        }

        clientThresholds[clientId] = targetThreshold;
        UpdateClientThresholdClientRpc(clientId, targetThreshold);
    }

    private void UpdateGlobalThreshold()
    {
        if (clientThresholds.Count == 0) return;

        float totalThreshold = 0f;
        foreach (var threshold in clientThresholds.Values)
        {
            totalThreshold += threshold;
        }

        globalThreshold.Value = totalThreshold / clientThresholds.Count;
    }

    [ClientRpc]
    private void UpdateClientThresholdClientRpc(ulong clientId, float newThreshold)
    {
        if (NetworkManager.LocalClientId == clientId)
        {
            Debug.Log($"Client {clientId} threshold updated to: {newThreshold:F3}");
        }
    }

    public float GetThresholdForClient(ulong clientId)
    {
        if (clientThresholds.TryGetValue(clientId, out float threshold))
        {
            return threshold;
        }
        return baseThreshold;
    }

    public float GetGlobalThreshold()
    {
        return globalThreshold.Value;
    }

    public override void OnDestroy()
    {
        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        base.OnDestroy();
    }
} 