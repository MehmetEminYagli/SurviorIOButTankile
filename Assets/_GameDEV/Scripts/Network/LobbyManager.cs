using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }
    
    [SerializeField] private int maxPlayers = 4;
    
    private Lobby currentLobby;
    private float heartbeatTimer;
    private float lobbyUpdateTimer;
    private const float LOBBY_HEARTBEAT_INTERVAL = 15f;
    private const float LOBBY_UPDATE_INTERVAL = 1.5f;
    private string playerName = "Player";
    
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

    private async void Start()
    {
        await UnityServices.InitializeAsync();
    }

    private void Update()
    {
        HandleLobbyHeartbeat();
        HandleLobbyPollForUpdates();
    }

    public async Task<bool> AuthenticatePlayer(string playerNickname)
    {
        try
        {
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                playerName = playerNickname;
                return true;
            }
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"Authentication failed: {e.Message}");
            return false;
        }
    }

    public async Task<string> CreateLobby(string lobbyName)
    {
        try
        {
            // Generate a random lobby code
            string lobbyCode = GenerateRandomLobbyCode();

            // Create Relay allocation using Unity.Services.Relay
            var allocation = await Unity.Services.Relay.RelayService.Instance.CreateAllocationAsync(maxPlayers);
            string joinCode = await Unity.Services.Relay.RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // Create player data
            var playerData = new Dictionary<string, PlayerDataObject>
            {
                { "Nickname", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, playerName) }
            };

            // Create lobby
            CreateLobbyOptions options = new CreateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { "RelayJoinCode", new DataObject(DataObject.VisibilityOptions.Public, joinCode) },
                    { "LobbyCode", new DataObject(DataObject.VisibilityOptions.Public, lobbyCode) }
                },
                Player = new Player
                {
                    Data = playerData
                }
            };

            currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
            Debug.Log($"Created lobby with code: {lobbyCode}");

            // Setup Relay
            RelayServerData relayServerData = new RelayServerData(allocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);
            NetworkManager.Singleton.StartHost();

            return lobbyCode;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to create lobby: {e.Message}");
            return null;
        }
    }

    public async Task<bool> JoinLobbyByCode(string lobbyCode)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(lobbyCode))
            {
                Debug.LogError("Cannot join lobby: Lobby code is empty or null");
                return false;
            }

            Debug.Log($"Attempting to join lobby with code: {lobbyCode}");

            // Query lobbies to find the one with matching code
            QueryLobbiesOptions options = new QueryLobbiesOptions
            {
                Count = 25,
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(
                        field: QueryFilter.FieldOptions.AvailableSlots,
                        op: QueryFilter.OpOptions.GT,
                        value: "0"
                    )
                }
            };

            QueryResponse queryResponse = await LobbyService.Instance.QueryLobbiesAsync(options);
            
            if (queryResponse.Results.Count == 0)
            {
                Debug.LogError("No lobby found with the given code");
                return false;
            }

            // Find the lobby with matching code
            Lobby targetLobby = null;
            foreach (var lobby in queryResponse.Results)
            {
                if (lobby.Data != null && 
                    lobby.Data.ContainsKey("LobbyCode") && 
                    lobby.Data["LobbyCode"].Value == lobbyCode)
                {
                    targetLobby = lobby;
                    break;
                }
            }

            if (targetLobby == null)
            {
                Debug.LogError("No lobby found with the given code");
                return false;
            }

            // Create player data
            var playerData = new Dictionary<string, PlayerDataObject>
            {
                { "Nickname", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, playerName) }
            };

            JoinLobbyByIdOptions joinOptions = new JoinLobbyByIdOptions
            {
                Player = new Player
                {
                    Data = playerData
                }
            };

            currentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(targetLobby.Id, joinOptions);
            
            if (currentLobby == null)
            {
                Debug.LogError("Failed to join lobby: Lobby not found");
                return false;
            }

            if (!currentLobby.Data.ContainsKey("RelayJoinCode"))
            {
                Debug.LogError("Failed to join lobby: Relay join code not found");
                return false;
            }

            string relayJoinCode = currentLobby.Data["RelayJoinCode"].Value;
            Debug.Log($"Got relay join code: {relayJoinCode}");

            var joinAllocation = await Unity.Services.Relay.RelayService.Instance.JoinAllocationAsync(relayJoinCode);
            RelayServerData relayServerData = new RelayServerData(joinAllocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);
            
            NetworkManager.Singleton.StartClient();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to join lobby: {e.Message}");
            return false;
        }
    }

    public async Task<List<Lobby>> GetLobbiesList()
    {
        try
        {
            QueryLobbiesOptions options = new QueryLobbiesOptions
            {
                Count = 25,
                Filters = new List<QueryFilter>()
            };

            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(options);
            return response.Results;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to get lobbies: {e.Message}");
            return new List<Lobby>();
        }
    }

    private void HandleLobbyHeartbeat()
    {
        if (currentLobby != null && IsLobbyHost())
        {
            heartbeatTimer -= Time.deltaTime;
            if (heartbeatTimer <= 0f)
            {
                float heartbeatTimerMax = LOBBY_HEARTBEAT_INTERVAL;
                heartbeatTimer = heartbeatTimerMax;
                LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);
            }
        }
    }

    private void HandleLobbyPollForUpdates()
    {
        if (currentLobby != null)
        {
            lobbyUpdateTimer -= Time.deltaTime;
            if (lobbyUpdateTimer <= 0f)
            {
                float lobbyUpdateTimerMax = LOBBY_UPDATE_INTERVAL;
                lobbyUpdateTimer = lobbyUpdateTimerMax;
                UpdateLobby();
            }
        }
    }

    private async void UpdateLobby()
    {
        try
        {
            currentLobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to update lobby: {e.Message}");
        }
    }

    public bool IsLobbyHost()
    {
        return currentLobby != null && currentLobby.HostId == AuthenticationService.Instance.PlayerId;
    }

    private string GenerateRandomLobbyCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        char[] code = new char[6];
        System.Random random = new System.Random();

        for (int i = 0; i < code.Length; i++)
        {
            code[i] = chars[random.Next(chars.Length)];
        }

        return new string(code);
    }

    public string GetPlayerName()
    {
        return playerName;
    }

    public void StartGame()
    {
        if (IsLobbyHost())
        {
            try
            {
                // Tüm oyuncular için sahne değişimini başlat
                NetworkManager.Singleton.SceneManager.LoadScene("GameScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
                
                // Sahne değişimi tamamlandığında çağrılacak event
                NetworkManager.Singleton.SceneManager.OnLoadComplete += SceneManager_OnLoadComplete;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to start game: {e.Message}");
            }
        }
    }

    private void SceneManager_OnLoadComplete(ulong clientId, string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode)
    {
        if (sceneName == "GameScene" && IsLobbyHost())
        {
            // NetworkSpawner'ı bul ve başlat
            NetworkSpawner spawner = FindObjectOfType<NetworkSpawner>();
            if (spawner != null)
            {
                Debug.Log("Scene loaded, NetworkSpawner found and ready.");
            }
            else
            {
                Debug.LogError("NetworkSpawner not found in the scene!");
            }
        }
        
        // Event'i bir kere kullandıktan sonra kaldır
        NetworkManager.Singleton.SceneManager.OnLoadComplete -= SceneManager_OnLoadComplete;
    }

    public List<string> GetPlayersInLobby()
    {
        if (currentLobby == null) return new List<string>();

        List<string> playerNames = new List<string>();
        foreach (Player player in currentLobby.Players)
        {
            if (player.Data != null && player.Data.ContainsKey("Nickname"))
            {
                playerNames.Add(player.Data["Nickname"].Value);
            }
            else
            {
                playerNames.Add("Unknown Player");
            }
        }
        return playerNames;
    }

    public async Task<bool> LeaveLobby()
    {
        try
        {
            if (currentLobby != null)
            {
                string playerId = AuthenticationService.Instance.PlayerId;
                await LobbyService.Instance.RemovePlayerAsync(currentLobby.Id, playerId);
                
                // Disconnect from network session
                if (NetworkManager.Singleton.IsConnectedClient)
                {
                    NetworkManager.Singleton.Shutdown();
                }
                
                currentLobby = null;
                return true;
            }
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to leave lobby: {e.Message}");
            return false;
        }
    }

    public int GetCurrentLobbyPlayerCount()
    {
        return currentLobby?.Players.Count ?? 0;
    }
} 