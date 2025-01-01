using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NetworkUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button disconnectButton;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject gamePanel;

    private NetworkManagerController networkManager;
    private NetworkRoomManager roomManager;
    private bool isInitialized = false;

    private void Start()
    {
        InitializeComponents();
        SetupUI();
    }

    private void InitializeComponents()
    {
        if (!isInitialized)
        {
            // Try to get NetworkManagerController
            networkManager = NetworkManagerController.Instance;
            if (networkManager == null)
            {
                Debug.LogError("NetworkManagerController instance not found! Make sure it exists in the scene.");
                enabled = false;
                return;
            }

            // Try to get NetworkRoomManager
            roomManager = NetworkRoomManager.Instance;
            if (roomManager == null)
            {
                Debug.LogWarning("NetworkRoomManager instance not found. Some features might not work.");
            }

            isInitialized = true;
        }
    }

    private void OnEnable()
    {
        if (isInitialized)
        {
            SubscribeToEvents();
        }
    }

    private void OnDisable()
    {
        if (isInitialized)
        {
            UnsubscribeFromEvents();
        }
    }

    private void SetupUI()
    {
        if (!isInitialized) return;

        hostButton?.onClick.AddListener(OnHostButtonClicked);
        clientButton?.onClick.AddListener(OnClientButtonClicked);
        disconnectButton?.onClick.AddListener(OnDisconnectButtonClicked);

        // Initial UI state
        UpdateUI(false);
    }

    private void SubscribeToEvents()
    {
        if (networkManager != null)
        {
            networkManager.OnClientConnected += HandleClientConnected;
            networkManager.OnClientDisconnected += HandleClientDisconnected;
            networkManager.OnHostStarted += HandleHostStarted;
        }

        if (roomManager != null)
        {
            roomManager.OnPlayerCountChanged += HandlePlayerCountChanged;
            roomManager.OnGameStarted += HandleGameStarted;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (networkManager != null)
        {
            networkManager.OnClientConnected -= HandleClientConnected;
            networkManager.OnClientDisconnected -= HandleClientDisconnected;
            networkManager.OnHostStarted -= HandleHostStarted;
        }

        if (roomManager != null)
        {
            roomManager.OnPlayerCountChanged -= HandlePlayerCountChanged;
            roomManager.OnGameStarted -= HandleGameStarted;
        }
    }

    private void OnHostButtonClicked()
    {
        if (!isInitialized || networkManager == null) return;

        networkManager.StartHost();
        UpdateUI(true);
        UpdateStatus("Starting Host...");
    }

    private void OnClientButtonClicked()
    {
        if (!isInitialized || networkManager == null) return;

        networkManager.StartClient();
        UpdateUI(true);
        UpdateStatus("Connecting to Host...");
    }

    private void OnDisconnectButtonClicked()
    {
        if (!isInitialized || networkManager == null) return;

        networkManager.DisconnectClient();
        UpdateUI(false);
        UpdateStatus("Disconnected");
    }

    private void HandleClientConnected()
    {
        UpdateStatus("Connected!");
    }

    private void HandleClientDisconnected()
    {
        UpdateUI(false);
        UpdateStatus("Disconnected");
    }

    private void HandleHostStarted()
    {
        UpdateStatus("Host Started");
    }

    private void HandlePlayerCountChanged(int playerCount)
    {
        UpdatePlayerCount(playerCount);
    }

    private void HandleGameStarted()
    {
        ShowGameUI();
    }

    private void UpdateUI(bool connected)
    {
        if (hostButton != null) hostButton.gameObject.SetActive(!connected);
        if (clientButton != null) clientButton.gameObject.SetActive(!connected);
        if (disconnectButton != null) disconnectButton.gameObject.SetActive(connected);
    }

    private void UpdateStatus(string status)
    {
        if (statusText != null)
        {
            statusText.text = status;
        }
    }

    private void UpdatePlayerCount(int count)
    {
        if (playerCountText != null)
        {
            playerCountText.text = $"Players: {count}";
        }
    }

    private void ShowGameUI()
    {
        if (menuPanel != null) menuPanel.SetActive(false);
        if (gamePanel != null) gamePanel.SetActive(true);
    }
} 