using Unity.Netcode;
using UnityEngine;
using Cinemachine;

public class NetworkPlayer : NetworkBehaviour
{
    [Header("Network Variables")]
    private NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>();
    private NetworkVariable<Quaternion> networkRotation = new NetworkVariable<Quaternion>();
    private NetworkVariable<float> networkHealth = new NetworkVariable<float>();

    [Header("Camera Settings")]
    [SerializeField] private GameObject cinemachineCameraPrefab;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0, 10f, -8f);
    [SerializeField] private float cameraRotationX = 45f;
    [SerializeField] private float cameraRotationY = 45f;

    [Header("Local Components")]
    private PlayerMovement playerMovement;
    private ShootingSystem shootingSystem;
    private PlayerController playerController;
    private Transform playerTransform;
    private CinemachineVirtualCamera playerCamera;
    private Rigidbody rb;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        shootingSystem = GetComponent<ShootingSystem>();
        playerController = GetComponent<PlayerController>();
        playerTransform = transform;
        rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Local player setup
            ConfigureLocalPlayer();
        }
        else
        {
            // Remote player setup
            ConfigureRemotePlayer();
        }
    }

    private void ConfigureLocalPlayer()
    {
        // Enable local player components
        if (playerMovement != null) playerMovement.enabled = true;
        if (shootingSystem != null) shootingSystem.enabled = true;
        if (playerController != null) playerController.enabled = true;
        if (rb != null) rb.isKinematic = false;

        // Setup player camera
        SetupPlayerCamera();

        // Start position sync
        InvokeRepeating(nameof(SyncTransform), 0f, 1f/60f);
    }

    private void SetupPlayerCamera()
    {
        if (cinemachineCameraPrefab == null)
        {
            Debug.LogError("Cinemachine Camera Prefab is not assigned!");
            return;
        }

        // Create camera for this player
        GameObject cameraObj = Instantiate(cinemachineCameraPrefab);
        playerCamera = cameraObj.GetComponent<CinemachineVirtualCamera>();

        if (playerCamera != null)
        {
            // Configure camera
            playerCamera.Follow = playerTransform;
            playerCamera.transform.position = playerTransform.position + cameraOffset;
            playerCamera.transform.rotation = Quaternion.Euler(cameraRotationX, cameraRotationY, 0f);

            // Set camera priority (ensure local player's camera is active)
            playerCamera.Priority = 10;

            // Set a unique name for the camera
            cameraObj.name = $"PlayerCamera_{OwnerClientId}";

            // Update PlayerController's camera reference if needed
            if (playerController != null)
            {
                playerController.SetCameraTransform(playerCamera.transform);
            }
        }
        else
        {
            Debug.LogError("CinemachineVirtualCamera component not found in prefab!");
        }
    }

    private void ConfigureRemotePlayer()
    {
        // Disable local control components for remote players
        if (playerMovement != null) playerMovement.enabled = false;
        if (shootingSystem != null) shootingSystem.enabled = false;
        if (playerController != null) playerController.enabled = false;
        if (rb != null) rb.isKinematic = true;

        // If there's a camera, set its priority to 0 (inactive)
        if (playerCamera != null)
        {
            playerCamera.Priority = 0;
        }
    }

    private void SyncTransform()
    {
      if (!IsOwner) return;
    // Daha az ağ trafiği için threshold ekleyin
    if (Vector3.Distance(networkPosition.Value, transform.position) > 0.1f)
    {
        UpdateTransformServerRpc(transform.position, transform.rotation);
    }
    }

    [ServerRpc]
    private void UpdateTransformServerRpc(Vector3 position, Quaternion rotation)
    {
        networkPosition.Value = position;
        networkRotation.Value = rotation;
    }

    [ServerRpc]
    public void ShootServerRpc(Vector3 direction)
    {
        ShootClientRpc(direction);
    }

    [ClientRpc]
    private void ShootClientRpc(Vector3 direction)
    {
        if (!IsOwner && shootingSystem != null)
        {
            shootingSystem.Shoot(direction);
        }
    }

    private void Update()
    {
        if (!IsOwner)
        {
            // Interpolate remote player position and rotation
            playerTransform.position = Vector3.Lerp(playerTransform.position, networkPosition.Value, Time.deltaTime * 10f);
            playerTransform.rotation = Quaternion.Lerp(playerTransform.rotation, networkRotation.Value, Time.deltaTime * 10f);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            CancelInvoke(nameof(SyncTransform));
            
            // Clean up camera
            if (playerCamera != null)
            {
                Destroy(playerCamera.gameObject);
            }
        }
    }
} 