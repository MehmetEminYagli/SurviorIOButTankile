using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Authentication;
using Unity.Services.Core;
using System;

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

    [Header("Material Selection")]
    [SerializeField] private Button nextMaterialButton;
    [SerializeField] private Button previousMaterialButton;
    [SerializeField] private Image materialPreviewImage;
    
    private int currentMaterialIndex = 0;

    private float refreshTimer = 0f;
    private const float REFRESH_RATE = 1.5f;

    [System.Serializable]
    private class PlayerListItem
    {
        public TextMeshProUGUI nameText;
        public Image colorImage;
    }

    private Dictionary<string, PlayerListItem> playerListItems = new Dictionary<string, PlayerListItem>();

    private void Start()
    {
        SetupUI();
        ValidateReferences();

        if (nextMaterialButton != null)
            nextMaterialButton.onClick.AddListener(NextMaterial);
        
        if (previousMaterialButton != null)
            previousMaterialButton.onClick.AddListener(PreviousMaterial);
            
        if (materialPreviewImage == null)
        {
            Debug.LogError("Material Preview Image is not assigned!");
        }
        else
        {
            materialPreviewImage.color = Color.white;
            UpdateMaterialPreview();
        }

        // Game Scene'den geldiğimizde auth durumunu kontrol et
        CheckAuthenticationStatus();
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
        if (string.IsNullOrEmpty(lobbyCodeInput.text))
        {
            Debug.LogWarning("Please enter a lobby code");
            return;
        }

        try
        {
            // Join butonu ve input field'ı devre dışı bırak
            joinLobbyButton.interactable = false;
            lobbyCodeInput.interactable = false;

            Debug.Log($"Attempting to join lobby with code: {lobbyCodeInput.text}");
            bool success = await LobbyManager.Instance.JoinLobbyByCode(lobbyCodeInput.text);
            
            if (success)
            {
                ShowLobbyRoom();
                UpdateLobbyRoomUI(LobbyManager.Instance.currentLobby.Name, lobbyCodeInput.text);
                RefreshPlayerList();
            }
            else
            {
                Debug.LogError($"Failed to join lobby with code: {lobbyCodeInput.text}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error joining lobby: {e.Message}");
        }
        finally
        {
            // UI elementlerini tekrar aktif et
            joinLobbyButton.interactable = true;
            lobbyCodeInput.interactable = true;
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

        List<string> players = LobbyManager.Instance.GetPlayersInLobby();
        Dictionary<string, PlayerListItem> newPlayerListItems = new Dictionary<string, PlayerListItem>();

        foreach (string playerName in players)
        {
            PlayerListItem item;
            GameObject playerItem;

            if (playerListItems.ContainsKey(playerName))
            {
                item = playerListItems[playerName];
                playerItem = item.nameText.transform.parent.gameObject;
            }
            else
            {
                playerItem = Instantiate(playerListItemPrefab, playerListContent);
                item = new PlayerListItem
                {
                    nameText = playerItem.GetComponentInChildren<TextMeshProUGUI>(),
                    colorImage = playerItem.GetComponentInChildren<Image>()
                };
            }

            if (item.nameText != null)
            {
                item.nameText.text = playerName;
            }

            if (item.colorImage != null)
            {
                int playerMaterialIndex = LobbyManager.Instance.GetPlayerMaterialIndex(playerName);
                Color playerColor = LobbyManager.Instance.GetPreviewColorByIndex(playerMaterialIndex);
                item.colorImage.color = new Color(playerColor.r, playerColor.g, playerColor.b, 1f);
            }

            newPlayerListItems[playerName] = item;
        }

        // Eski oyuncuları temizle
        foreach (var kvp in playerListItems)
        {
            if (!newPlayerListItems.ContainsKey(kvp.Key))
            {
                if (kvp.Value.nameText != null)
                {
                    Destroy(kvp.Value.nameText.transform.parent.gameObject);
                }
            }
        }

        playerListItems = newPlayerListItems;
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

    private void NextMaterial()
    {
        currentMaterialIndex = (currentMaterialIndex + 1) % LobbyManager.Instance.GetMaterialCount();
        UpdateMaterialSelection();
    }

    private void PreviousMaterial()
    {
        currentMaterialIndex--;
        if (currentMaterialIndex < 0)
            currentMaterialIndex = LobbyManager.Instance.GetMaterialCount() - 1;
        UpdateMaterialSelection();
    }

    private void UpdateMaterialSelection()
    {
        if (LobbyManager.Instance != null)
        {
            // Materyal seçimini güncelle ve kuyruğa ekle
            LobbyManager.Instance.SelectMaterial(currentMaterialIndex);
            UpdateMaterialPreview();
            
            // Hemen local oyuncunun rengini güncelle
            UpdateLocalPlayerColor();
        }
    }

    private void UpdateLocalPlayerColor()
    {
        string localPlayerName = LobbyManager.Instance.GetPlayerName();
        if (playerListItems.ContainsKey(localPlayerName))
        {
            PlayerListItem item = playerListItems[localPlayerName];
            if (item.colorImage != null)
            {
                Color newColor = LobbyManager.Instance.GetPreviewColorByIndex(currentMaterialIndex);
                item.colorImage.color = new Color(newColor.r, newColor.g, newColor.b, 1f);
            }
        }
    }

    private void UpdateMaterialPreview()
    {
        if (materialPreviewImage != null && LobbyManager.Instance != null)
        {
            Color previewColor = LobbyManager.Instance.GetPreviewColorByIndex(currentMaterialIndex);
            Debug.Log($"Updating material preview color to: {previewColor}");
            materialPreviewImage.color = new Color(previewColor.r, previewColor.g, previewColor.b, 1f);
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
        
        if (nextMaterialButton != null)
            nextMaterialButton.onClick.RemoveListener(NextMaterial);
        
        if (previousMaterialButton != null)
            previousMaterialButton.onClick.RemoveListener(PreviousMaterial);
    }

    private void CheckAuthenticationStatus()
    {
        if (Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn)
        {
            ShowMainMenu();
        }
        else
        {
            ShowAuthPanel();
        }
    }
} 