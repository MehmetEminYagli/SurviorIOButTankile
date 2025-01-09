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
    
    public Lobby currentLobby { get; private set; }
    private float heartbeatTimer;
    private float lobbyUpdateTimer;
    private const float LOBBY_HEARTBEAT_INTERVAL = 15f;
    private const float LOBBY_UPDATE_INTERVAL = 3f;
    private float lastLobbyUpdateTime = 0f;
    private string playerName = "Player";
    
    [SerializeField] private PlayerMaterialsData playerMaterials;
    
    private Dictionary<string, int> playerMaterialSelections = new Dictionary<string, int>();
    private int localPlayerMaterialIndex = 0;

    private Queue<int> materialUpdateQueue = new Queue<int>();
    private bool isProcessingMaterialUpdate = false;
    private const float MATERIAL_UPDATE_INTERVAL = 1f;
    private float lastMaterialUpdateTime = 0f;

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
        ProcessMaterialUpdateQueue();
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
                { "Nickname", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, playerName) },
                { "MaterialIndex", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, "0") }
            };

            // Create lobby options with visibility
            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = false, // Lobiyi public yap
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
            
            // Lobi oluşturulduktan hemen sonra bir kez daha güncelle
            await UpdateLobby();
            
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
                Filters = new List<QueryFilter>()  // Filtreleri kaldıralım
            };

            QueryResponse queryResponse;
            try
            {
                queryResponse = await LobbyService.Instance.QueryLobbiesAsync(options);
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"Failed to query lobbies: {e.Message}");
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
                Debug.LogError($"No lobby found with code: {lobbyCode}");
                return false;
            }

            // Eğer oyuncu zaten bir lobideyse ve farklı bir lobiye katılmaya çalışıyorsa
            if (currentLobby != null)
            {
                if (currentLobby.Id == targetLobby.Id)
                {
                    Debug.LogWarning("Player is already in this lobby!");
                    return false;
                }

                Debug.Log("Leaving current lobby before joining new one...");
                try
                {
                    await LeaveLobby();
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to leave current lobby: {e.Message}");
                    return false;
                }
            }

            // Create player data
            var playerData = new Dictionary<string, PlayerDataObject>
            {
                { "Nickname", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, playerName) },
                { "MaterialIndex", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, "0") }
            };

            JoinLobbyByIdOptions joinOptions = new JoinLobbyByIdOptions
            {
                Player = new Player { Data = playerData }
            };

            try
            {
                currentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(targetLobby.Id, joinOptions);
                Debug.Log($"Successfully joined lobby: {targetLobby.Name}");
            }
            catch (LobbyServiceException e)
            {
                if (e.Message.Contains("already a member"))
                {
                    Debug.LogWarning("Player is already a member of this lobby");
                    return false;
                }
                Debug.LogError($"Failed to join lobby: {e.Message}");
                return false;
            }

            if (!currentLobby.Data.ContainsKey("RelayJoinCode"))
            {
                Debug.LogError("Relay join code not found in lobby data");
                return false;
            }

            string relayJoinCode = currentLobby.Data["RelayJoinCode"].Value;
            Debug.Log($"Got relay join code: {relayJoinCode}");

            try
            {
                var joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);
                var relayServerData = new RelayServerData(joinAllocation, "dtls");
                NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);
                
                NetworkManager.Singleton.StartClient();
                Debug.Log("Successfully started network client");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to setup relay: {e.Message}");
                return false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Unexpected error while joining lobby: {e.Message}\nStack trace: {e.StackTrace}");
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

    private async void HandleLobbyPollForUpdates()
    {
        if (currentLobby != null)
        {
            lobbyUpdateTimer -= Time.deltaTime;
            if (lobbyUpdateTimer <= 0f)
            {
                float lobbyUpdateTimerMax = LOBBY_UPDATE_INTERVAL;
                lobbyUpdateTimer = lobbyUpdateTimerMax;
                await UpdateLobby();
            }
        }
    }

    private async Task UpdateLobby()
    {
        try
        {
            // Rate limit kontrolü ekleyelim
            if (Time.time - lastLobbyUpdateTime < LOBBY_UPDATE_INTERVAL)
            {
                return;
            }

            if (currentLobby != null)
            {
                currentLobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
                lastLobbyUpdateTime = Time.time;
                
                // Materyal seçimlerini güncelle
                foreach (var player in currentLobby.Players)
                {
                    if (player.Data != null && 
                        player.Data.ContainsKey("Nickname") && 
                        player.Data.ContainsKey("MaterialIndex"))
                    {
                        string playerNickname = player.Data["Nickname"].Value;
                        if (int.TryParse(player.Data["MaterialIndex"].Value, out int materialIndex))
                        {
                            playerMaterialSelections[playerNickname] = materialIndex;
                        }
                    }
                }
            }
        }
        catch (LobbyServiceException e)
        {
            if (e.Message.Contains("Rate limit"))
            {
                // Rate limit aşıldıysa sessizce devam et
                return;
            }
            Debug.LogError($"Failed to update lobby: {e.Message}");
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

    public void SelectMaterial(int materialIndex)
    {
        if (materialIndex >= 0 && materialIndex < playerMaterials.availableMaterials.Count)
        {
            localPlayerMaterialIndex = materialIndex;
            
            // Hemen local dictionary'yi güncelle
            if (!string.IsNullOrEmpty(playerName))
            {
                playerMaterialSelections[playerName] = materialIndex;
            }

            // Güncelleme kuyruğuna ekle
            if (!materialUpdateQueue.Contains(materialIndex))
            {
                materialUpdateQueue.Enqueue(materialIndex);
            }
        }
    }

    private void ProcessMaterialUpdateQueue()
    {
        if (materialUpdateQueue.Count > 0 && !isProcessingMaterialUpdate && 
            Time.time - lastMaterialUpdateTime >= MATERIAL_UPDATE_INTERVAL)
        {
            isProcessingMaterialUpdate = true;
            int materialIndex = materialUpdateQueue.Dequeue();
            UpdatePlayerMaterialDataAsync(materialIndex);
        }
    }

    private async void UpdatePlayerMaterialDataAsync(int materialIndex)
    {
        try 
        {
            if (currentLobby == null) return;

            var playerData = new Dictionary<string, PlayerDataObject>
            {
                { "Nickname", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, playerName) },
                { "MaterialIndex", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, materialIndex.ToString()) }
            };

            await LobbyService.Instance.UpdatePlayerAsync(
                currentLobby.Id,
                AuthenticationService.Instance.PlayerId,
                new UpdatePlayerOptions { Data = playerData }
            );

            lastMaterialUpdateTime = Time.time;
        }
        catch (LobbyServiceException e)
        {
            if (!e.Message.Contains("Rate limit"))
            {
                Debug.LogError($"Failed to update player material data: {e.Message}");
            }
            // Rate limit hatası durumunda, güncellemeyi tekrar kuyruğa ekle
            else if (materialUpdateQueue.Count == 0)
            {
                materialUpdateQueue.Enqueue(materialIndex);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to update player material data: {e.Message}");
        }
        finally
        {
            isProcessingMaterialUpdate = false;
        }
    }

    public int GetPlayerMaterialIndex(string playerName)
    {
        // Önce local dictionary'den kontrol et
        if (playerMaterialSelections.ContainsKey(playerName))
        {
            return playerMaterialSelections[playerName];
        }

        // Eğer local'de yoksa lobby'den kontrol et
        if (currentLobby != null)
        {
            foreach (var player in currentLobby.Players)
            {
                if (player.Data != null && 
                    player.Data.ContainsKey("Nickname") && 
                    player.Data["Nickname"].Value == playerName &&
                    player.Data.ContainsKey("MaterialIndex"))
                {
                    if (int.TryParse(player.Data["MaterialIndex"].Value, out int materialIndex))
                    {
                        playerMaterialSelections[playerName] = materialIndex; // Cache the result
                        return materialIndex;
                    }
                }
            }
        }
        return 0;
    }

    public int GetLocalPlayerMaterialIndex()
    {
        return localPlayerMaterialIndex;
    }

    public Material GetMaterialByIndex(int index)
    {
        if (index >= 0 && index < playerMaterials.availableMaterials.Count)
        {
            return playerMaterials.availableMaterials[index].material;
        }
        return null;
    }

    public Color GetPreviewColorByIndex(int index)
    {
        if (index >= 0 && index < playerMaterials.availableMaterials.Count)
        {
            return playerMaterials.availableMaterials[index].previewColor;
        }
        return Color.white;
    }

    public int GetMaterialCount()
    {
        return playerMaterials != null ? playerMaterials.availableMaterials.Count : 0;
    }
} 