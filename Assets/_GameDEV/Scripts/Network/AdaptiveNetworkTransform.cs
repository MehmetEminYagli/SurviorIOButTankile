using UnityEngine;
using Unity.Netcode;

public class AdaptiveNetworkTransform : NetworkBehaviour
{
    [Header("Transform Sync Settings")]
    [SerializeField] private float positionThreshold = 0.01f;
    [SerializeField] private float rotationThreshold = 0.1f;
    [SerializeField] private float interpolationSpeed = 15f;

    private NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>();
    private NetworkVariable<Quaternion> networkRotation = new NetworkVariable<Quaternion>();
    
    private Vector3 lastSentPosition;
    private Quaternion lastSentRotation;
    
    private AdaptiveThresholdManager thresholdManager;
    private NetworkPerformanceManager performanceManager;

    private void Start()
    {
        thresholdManager = FindObjectOfType<AdaptiveThresholdManager>();
        performanceManager = FindObjectOfType<NetworkPerformanceManager>();

        if (!thresholdManager || !performanceManager)
        {
            Debug.LogError("Gerekli manager'lar bulunamadı!");
            return;
        }

        lastSentPosition = transform.position;
        lastSentRotation = transform.rotation;
    }

    private void Update()
    {
        if (IsServer)
        {
            ServerUpdate();
        }
        else if (IsClient)
        {
            ClientUpdate();
        }
    }

    private void ServerUpdate()
    {
        // Dinamik threshold değerlerini al
        float currentThreshold = thresholdManager.GetGlobalThreshold();
        float adjustedPositionThreshold = positionThreshold * (1f + currentThreshold);
        float adjustedRotationThreshold = rotationThreshold * (1f + currentThreshold);

        // Pozisyon kontrolü
        float positionDifference = Vector3.Distance(transform.position, lastSentPosition);
        if (positionDifference > adjustedPositionThreshold)
        {
            networkPosition.Value = transform.position;
            lastSentPosition = transform.position;
        }

        // Rotasyon kontrolü
        float rotationDifference = Quaternion.Angle(transform.rotation, lastSentRotation);
        if (rotationDifference > adjustedRotationThreshold)
        {
            networkRotation.Value = transform.rotation;
            lastSentRotation = transform.rotation;
        }
    }

    private void ClientUpdate()
    {
        if (!IsOwner)
        {
            // Tick rate'e göre interpolasyon hızını ayarla
            float currentTickRate = performanceManager.GetCurrentTickRate();
            float adjustedSpeed = interpolationSpeed * (60f / currentTickRate);

            // Pozisyon interpolasyonu
            transform.position = Vector3.Lerp(
                transform.position,
                networkPosition.Value,
                Time.deltaTime * adjustedSpeed
            );

            // Rotasyon interpolasyonu
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                networkRotation.Value,
                Time.deltaTime * adjustedSpeed
            );
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            networkPosition.Value = transform.position;
            networkRotation.Value = transform.rotation;
        }
    }

    // Debug bilgilerini göster
    private void OnGUI()
    {
        if (!Application.isPlaying || !IsSpawned) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 100));
        
        if (IsServer)
        {
            GUILayout.Label($"Server Threshold: {thresholdManager.GetGlobalThreshold():F3}");
            GUILayout.Label($"Server Tick Rate: {performanceManager.GetCurrentTickRate():F1}");
        }
        else
        {
            GUILayout.Label($"Client Threshold: {thresholdManager.GetThresholdForClient(NetworkManager.LocalClientId):F3}");
            GUILayout.Label($"Client Latency: {performanceManager.GetCurrentLatency():F1}ms");
            GUILayout.Label($"Packet Loss: {performanceManager.GetCurrentPacketLoss():P2}");
        }

        GUILayout.EndArea();
    }
} 