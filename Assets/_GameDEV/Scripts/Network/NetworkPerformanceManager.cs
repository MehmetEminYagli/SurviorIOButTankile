using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;

public class NetworkPerformanceManager : NetworkBehaviour
{
    public static NetworkPerformanceManager Instance { get; private set; }

    [Header("Tick Rate Settings")]
    [SerializeField] private float baseTickRate = 64f;
    [SerializeField] private float minTickRate = 20f;
    [SerializeField] private float maxTickRate = 128f;
    
    [Header("Network Quality Thresholds")]
    [SerializeField] private float goodLatencyThreshold = 50f;  // ms
    [SerializeField] private float badLatencyThreshold = 150f;  // ms
    [SerializeField] private float packetLossThreshold = 0.1f;  // 10%

    [Header("Adaptive Settings")]
    [SerializeField] private float adaptationSpeed = 1f;
    [SerializeField] private float measurementInterval = 1f;

    [Header("References")]
    [SerializeField] private NetworkManager networkManager;

    private float currentTickRate;
    private float currentLatency;
    private float currentPacketLoss;
    private float measurementTimer;
    private Queue<float> latencyMeasurements = new Queue<float>();
    private NetworkVariable<float> networkTickRate = new NetworkVariable<float>();
    private Dictionary<ulong, Queue<float>> clientLatencyHistory = new Dictionary<ulong, Queue<float>>();
    private const int LATENCY_HISTORY_SIZE = 10;

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

        // NetworkManager referansını otomatik bul
        if (networkManager == null)
        {
            networkManager = FindObjectOfType<NetworkManager>();
        }

        if (networkManager == null)
        {
            Debug.LogError("NetworkManager bulunamadı! Lütfen NetworkManager referansını inspector'dan atayın.");
            enabled = false;
            return;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (!IsServer) return;
        
        currentTickRate = baseTickRate;
        networkTickRate.Value = currentTickRate;
        Application.targetFrameRate = Mathf.CeilToInt(maxTickRate);
    }

    private void Update()
    {
        if (!IsServer || !networkManager.IsListening) return;

        measurementTimer += Time.deltaTime;
        if (measurementTimer >= measurementInterval)
        {
            measurementTimer = 0f;
            UpdateNetworkMetrics();
            AdjustTickRate();
        }
    }

    private void UpdateNetworkMetrics()
    {
        if (!IsServer) return;

        currentLatency = CalculateAverageLatency();
        currentPacketLoss = EstimatePacketLoss();

        UpdateNetworkMetricsClientRpc(currentLatency, currentPacketLoss);
    }

    private float CalculateAverageLatency()
    {
        float totalLatency = 0f;
        var connectedClients = networkManager.ConnectedClientsIds;
        int clientCount = 0;
        
        foreach (var clientId in connectedClients)
        {
            if (clientId == networkManager.LocalClientId) continue;
            
            var rtt = networkManager.NetworkConfig.NetworkTransport.GetCurrentRtt(clientId);
            if (rtt >= 0)
            {
                if (!clientLatencyHistory.ContainsKey(clientId))
                {
                    clientLatencyHistory[clientId] = new Queue<float>();
                }

                var history = clientLatencyHistory[clientId];
                history.Enqueue(rtt);
                if (history.Count > LATENCY_HISTORY_SIZE)
                {
                    history.Dequeue();
                }

                totalLatency += rtt;
                clientCount++;
            }
        }

        var connectedClientsList = connectedClients.ToList();
        var disconnectedClients = clientLatencyHistory.Keys
            .Where(clientId => !connectedClientsList.Contains(clientId))
            .ToList();

        foreach (var clientId in disconnectedClients)
        {
            clientLatencyHistory.Remove(clientId);
        }

        return clientCount > 0 ? totalLatency / clientCount : 0f;
    }

    private float EstimatePacketLoss()
    {
        if (!IsServer || networkManager.NetworkConfig?.NetworkTransport == null)
            return 0f;

        float totalPacketLoss = 0f;
        int clientCount = 0;

        foreach (var clientId in networkManager.ConnectedClientsIds)
        {
            if (clientId == networkManager.LocalClientId) continue;

            if (clientLatencyHistory.TryGetValue(clientId, out Queue<float> history) && history.Count > 0)
            {
                float latencyVariance = CalculateLatencyVariance(history);
                float estimatedLoss = Mathf.Clamp01(latencyVariance / badLatencyThreshold);
                totalPacketLoss += estimatedLoss;
                clientCount++;
            }
        }

        return clientCount > 0 ? totalPacketLoss / clientCount : 0f;
    }

    private float CalculateLatencyVariance(Queue<float> latencyHistory)
    {
        if (latencyHistory.Count < 2) return 0f;

        float sum = 0f;
        float sumSquared = 0f;
        int count = latencyHistory.Count;

        foreach (float latency in latencyHistory)
        {
            sum += latency;
            sumSquared += latency * latency;
        }

        float mean = sum / count;
        float variance = (sumSquared / count) - (mean * mean);
        return Mathf.Sqrt(Mathf.Max(0f, variance));
    }

    private void AdjustTickRate()
    {
        if (!IsServer) return;

        float targetTickRate = baseTickRate;

        if (currentLatency <= goodLatencyThreshold)
        {
            targetTickRate *= 1.1f;
        }
        else if (currentLatency >= badLatencyThreshold)
        {
            targetTickRate *= 0.9f;
        }

        if (currentPacketLoss > packetLossThreshold)
        {
            targetTickRate *= 0.95f;
        }

        targetTickRate = Mathf.Clamp(targetTickRate, minTickRate, maxTickRate);
        currentTickRate = Mathf.Lerp(currentTickRate, targetTickRate, Time.deltaTime * adaptationSpeed);
        
        networkTickRate.Value = currentTickRate;
        UpdateTickRateClientRpc(currentTickRate);
    }

    [ClientRpc]
    private void UpdateNetworkMetricsClientRpc(float latency, float packetLoss)
    {
        Debug.Log($"Network Metrics - Latency: {latency:F2}ms, Packet Loss: {packetLoss:P2}");
    }

    [ClientRpc]
    private void UpdateTickRateClientRpc(float newTickRate)
    {
        if (IsServer) return;
        Time.fixedDeltaTime = 1f / newTickRate;
    }

    public float GetCurrentTickRate() => currentTickRate;
    public float GetCurrentLatency() => currentLatency;
    public float GetCurrentPacketLoss() => currentPacketLoss;
} 