using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Unity.Services.Lobbies.Models;
using System.Threading.Tasks;

public class LobbyUI : MonoBehaviour
{
    [Header("Authentication")]
    [SerializeField] private GameObject authPanel;
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private Button confirmNicknameButton;

    [Header("Main Menu")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private Button createLobbyButton;
    [SerializeField] private Button joinLobbyButton;
    [SerializeField] private Button refreshLobbiesButton;
    [SerializeField] private TMP_InputField lobbyCodeInput;
    [SerializeField] private Transform lobbyListContent;
    [SerializeField] private GameObject lobbyListItemPrefab;

    [Header("Lobby Room")]
    [SerializeField] private GameObject lobbyRoomPanel;
    [SerializeField] private TextMeshProUGUI lobbyNameText;
    [SerializeField] private TextMeshProUGUI lobbyCodeText;
    [SerializeField] private Transform playerListContent;
    [SerializeField] private GameObject playerListItemPrefab;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveLobbyButton;

    private float refreshTimer = 0f;
    private const float REFRESH_RATE = 1.5f;

    private void Start()
    {
        SetupUI();
        ValidateReferences();
    }

    private void ValidateReferences()
    {
        if (lobbyListContent == null)
            Debug.LogError("LobbyListContent is not assigned in the inspector!");
        if (lobbyListItemPrefab == null)
            Debug.LogError("LobbyListItemPrefab is not assigned in the inspector!");
        if (playerListContent == null)
            Debug.LogError("PlayerListContent is not assigned in the inspector!");
        if (playerListItemPrefab == null)
            Debug.LogError("PlayerListItemPrefab is not assigned in the inspector!");
    }

    private void Update()
    {
        if (lobbyRoomPanel != null && lobbyRoomPanel.activeSelf)
        {
            refreshTimer -= Time.deltaTime;
            if (refreshTimer <= 0f)
            {
                refreshTimer = REFRESH_RATE;
                RefreshPlayerList();
            }
        }
    }

    private void SetupUI()
    {
        // Auth Panel
        confirmNicknameButton.onClick.AddListener(OnConfirmNicknameClicked);

        // Main Menu
        createLobbyButton.onClick.AddListener(OnCreateLobbyClicked);
        joinLobbyButton.onClick.AddListener(OnJoinLobbyClicked);
        refreshLobbiesButton.onClick.AddListener(RefreshLobbyList);

        // Lobby Room
        startGameButton.onClick.AddListener(OnStartGameClicked);
        leaveLobbyButton.onClick.AddListener(OnLeaveLobbyClicked);

        // Initial state
        ShowAuthPanel();
    }

    private void ShowAuthPanel()
    {
        authPanel.SetActive(true);
        mainMenuPanel.SetActive(false);
        lobbyRoomPanel.SetActive(false);
    }

    private void ShowMainMenu()
    {
        authPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
        lobbyRoomPanel.SetActive(false);
        RefreshLobbyList();
    }

    private void ShowLobbyRoom()
    {
        authPanel.SetActive(false);
        mainMenuPanel.SetActive(false);
        lobbyRoomPanel.SetActive(true);
        
        // Start Game butonu sadece lobi sahibinde görünür olsun
        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(LobbyManager.Instance.IsLobbyHost());
        }
    }

    private async void OnConfirmNicknameClicked()
    {
        if (string.IsNullOrEmpty(nicknameInput.text)) return;

        bool success = await LobbyManager.Instance.AuthenticatePlayer(nicknameInput.text);
        if (success)
        {
            ShowMainMenu();
        }
    }

    private async void OnCreateLobbyClicked()
    {
        string lobbyName = $"{LobbyManager.Instance.GetPlayerName()}'s Lobby";
        string lobbyCode = await LobbyManager.Instance.CreateLobby(lobbyName);
        
        if (!string.IsNullOrEmpty(lobbyCode))
        {
            UpdateLobbyRoomUI(lobbyName, lobbyCode);
            ShowLobbyRoom();
        }
    }

    private async void OnJoinLobbyClicked()
    {
        if (string.IsNullOrEmpty(lobbyCodeInput.text)) return;

        bool success = await LobbyManager.Instance.JoinLobbyByCode(lobbyCodeInput.text);
        if (success)
        {
            ShowLobbyRoom();
        }
    }

    private async void RefreshLobbyList()
    {
        if (lobbyListContent == null || lobbyListItemPrefab == null)
        {
            Debug.LogError("Lobby list references are missing!");
            return;
        }

        // Clear existing lobby list
        foreach (Transform child in lobbyListContent)
        {
            Destroy(child.gameObject);
        }

        List<Lobby> lobbies = await LobbyManager.Instance.GetLobbiesList();
        
        foreach (Lobby lobby in lobbies)
        {
            string lobbyCode = null;
            if (lobby.Data != null && lobby.Data.ContainsKey("LobbyCode"))
            {
                lobbyCode = lobby.Data["LobbyCode"].Value;
            }

            if (string.IsNullOrEmpty(lobbyCode))
            {
                Debug.LogWarning($"Skipping lobby {lobby.Name} because it has no code");
                continue;
            }

            GameObject lobbyItem = Instantiate(lobbyListItemPrefab, lobbyListContent);
            
            // Set lobby info text
            TextMeshProUGUI infoText = lobbyItem.GetComponentInChildren<TextMeshProUGUI>();
            if (infoText != null)
            {
                // Get real-time player count from the lobby
                int currentPlayers = lobby.Players?.Count ?? 0;
                infoText.text = $"{lobby.Name} ({currentPlayers}/{lobby.MaxPlayers})";
            }
            
            // Setup join button
            Button joinButton = lobbyItem.GetComponentInChildren<Button>();
            if (joinButton != null)
            {
                string lobbyCodeCopy = lobbyCode;
                string lobbyNameCopy = lobby.Name;
                
                Debug.Log($"Setting up join button for lobby: {lobbyNameCopy} with code: {lobbyCodeCopy}");
                
                joinButton.onClick.RemoveAllListeners();
                joinButton.onClick.AddListener(async () => 
                {
                    Debug.Log($"Attempting to join lobby {lobbyNameCopy} with code: {lobbyCodeCopy}");
                    bool success = await LobbyManager.Instance.JoinLobbyByCode(lobbyCodeCopy);
                    
                    if (success)
                    {
                        ShowLobbyRoom();
                        UpdateLobbyRoomUI(lobbyNameCopy, lobbyCodeCopy);
                        RefreshPlayerList();
                    }
                    else
                    {
                        Debug.LogError($"Failed to join lobby {lobbyNameCopy}");
                    }
                });
            }
        }
    }

    private void RefreshPlayerList()
    {
        if (playerListContent == null || playerListItemPrefab == null)
        {
            Debug.LogError("Player list references are missing!");
            return;
        }

        // Clear existing player list
        foreach (Transform child in playerListContent)
        {
            Destroy(child.gameObject);
        }

        List<string> players = LobbyManager.Instance.GetPlayersInLobby();
        foreach (string playerName in players)
        {
            GameObject playerItem = Instantiate(playerListItemPrefab, playerListContent);
            playerItem.GetComponentInChildren<TextMeshProUGUI>().text = playerName;
        }
    }

    private void UpdateLobbyRoomUI(string lobbyName, string lobbyCode)
    {
        lobbyNameText.text = lobbyName;
        lobbyCodeText.text = $"Kod: {lobbyCode}";
        
        // Start Game butonu görünürlüğünü güncelle
        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(LobbyManager.Instance.IsLobbyHost());
        }
        
        RefreshPlayerList();
    }

    private void OnStartGameClicked()
    {
        if (lobbyRoomPanel != null)
        {
            lobbyRoomPanel.SetActive(false);
        }
        
        LobbyManager.Instance.StartGame();
    }

    private async void OnLeaveLobbyClicked()
    {
        bool success = await LobbyManager.Instance.LeaveLobby();
        if (success)
        {
            // Clear player list before showing main menu
            if (playerListContent != null)
            {
                foreach (Transform child in playerListContent)
                {
                    Destroy(child.gameObject);
                }
            }
            ShowMainMenu();
        }
    }

    private void OnDestroy()
    {
        if (confirmNicknameButton != null)
            confirmNicknameButton.onClick.RemoveAllListeners();
        
        if (createLobbyButton != null)
            createLobbyButton.onClick.RemoveAllListeners();
        
        if (joinLobbyButton != null)
            joinLobbyButton.onClick.RemoveAllListeners();
        
        if (refreshLobbiesButton != null)
            refreshLobbiesButton.onClick.RemoveAllListeners();
        
        if (startGameButton != null)
            startGameButton.onClick.RemoveAllListeners();
        
        if (leaveLobbyButton != null)
            leaveLobbyButton.onClick.RemoveAllListeners();
    }
} 