using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using Unity.Netcode;
using Unity.Services.Relay.Models;
using Unity.Services.Relay;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using System;
using Unity.VisualScripting;

public class TankLobby : NetworkBehaviour {
    public static TankLobby Singleton { get; private set; } = null;
    public bool IsInTankLobby { get { return lobby != null; } }

    public string LobbyName { get { return lobby?.Name; } }
    public string LobbyCode { get { return lobby?.LobbyCode; } }
    public string LobbyId { get { return lobby?.Id; } }
    public int? MaxPlayerCount { get { return (lobby?.MaxPlayers); } }
    public int? AvailableSlots { get { return (lobby?.AvailableSlots); } }
    public bool? IsPrivate { get { return lobby?.IsPrivate; } }
    public List<Player> LobbyPlayers { get { return lobby?.Players; } }
    public bool IsLobbyHost { get { return lobby?.HostId == GameNetcodeManager.Singleton?.PlayerId; } }
    //public bool IsInMatch { get { return TankRelay.MatchLogic != null; } }

    private Lobby lobby;
    private ILobbyEvents lobbyEvents;
    private const float heartbeatTimerMax = 15f;
    private float heartbeatTimer;
    private bool processingLobbyCreationOrHost = false;
    private bool launchingMatch = false;

    public const string LOBBYDATAMAPKEY = "mapname";
    public const string LOBBYDATARELAYJOINCODE = "relayJoinCode";
    public const string PLAYERDATAUSERNAME = "customUsername";

    public Action LobbyChanged;
    public Action<string> NameChanged;
    public Action<List<LobbyPlayerJoined>> PlayersJoined;
    public Action<List<Player>> PlayersLeft;
    public Action LobbyDeleted;
    public Action<bool> IsPrivateChanged;
    public Action<bool> HasPasswordChanged;
    public Action<Dictionary<string, ChangedOrRemovedLobbyValue<DataObject>>> LobbyDataChangedOrRemoved;
    public Action<string> HostIdChanged;
    public Action JoinedLobby;
    public Action KickedFromLobby;
    public Action<Player, string> PlayerChangedUsername;

    /*
    public event Action<ulong, ConnectionStatus> OnClientConnection;
    */

    void Awake() {
        if (Singleton != null) {
            Destroy(this);
            return;
        }

        Singleton = this;

        JoinedLobby += OnJoinedLobby;
    }

    void Update() {
        // MOVE THIS LOGIC ELSEWHERE, THIS IS HERE FOR DEBUGGING ===========================================================================
        /*
        if (Input.GetButtonDown("PTT")) {
            TankVivox.TransmissionButtonPressed();
        } else if (Input.GetButtonUp("PTT")) {
            TankVivox.TransmissionButtonReleased();
        }
        */

        if (!IsInTankLobby || !IsLobbyHost) return;
        HandleHeartbeat(Time.deltaTime);
    }

    public override void OnDestroy() {
        JoinedLobby -= OnJoinedLobby;
    }

    /// <summary>
    /// Instantiate and connect TankLobby.CurrentTankLobby as a host.  Destroys any previous TankLobby.CurrentTankLobby.
    /// </summary>
    /// <param name="lobbyName"></param>
    /// <param name="maxPlayerCount"></param>
    /// <param name="isPrivate"></param>
    /// <param name="password"></param>
    /// <returns></returns>
    public async Task<bool> CreateTankLobby(string lobbyName, int maxPlayerCount, bool isPrivate, string password = null) {
        if (processingLobbyCreationOrHost) return false;
        processingLobbyCreationOrHost = true;
        if (IsInTankLobby) {
            await LeaveCurrentLobby();
        }
        
        //TankLobby newTankLobby = new TankLobby();

        bool successfulCreation = await StartLobby(lobbyName, maxPlayerCount, isPrivate, password);
        if (successfulCreation) {
            //CurrentTankLobby = newTankLobby;
            await SubscribeToLobbyEvents();

            /*
            TankRelay.CreateTankRelayHostResults results = await TankRelay.CreateTankRelayHost(maxPlayerCount, CurrentTankLobby.LobbyId, CurrentTankLobby.PlayerId);

            if (results.Successful) {
                // Initialize the lobby custom data
                UpdateLobbyOptions lobbyOptions = new UpdateLobbyOptions();
                lobbyOptions.Data = new Dictionary<string, DataObject>();
                lobbyOptions.Data.Add(LOBBYDATARELAYJOINCODE, new DataObject(DataObject.VisibilityOptions.Member, results.JoinCode));
                lobbyOptions.Data.Add(LOBBYDATAMAPKEY, new DataObject(DataObject.VisibilityOptions.Public, "NoMap"));

                try {
                    await LobbyService.Instance.UpdateLobbyAsync(CurrentTankLobby.LobbyId, lobbyOptions);
                } catch (LobbyServiceException e) {
                    Debug.Log(e);
                }
            }

            if (results.Successful) {
                await CurrentTankLobby.SetPlayerAllocationId(results.AllocationId);
            }
            */

            TriggerJoinedLobby();
        } else await EndLobby();
        processingLobbyCreationOrHost = false;
        return successfulCreation;
    }

    public async Task<bool> JoinLobbyById(string id) {
        if (processingLobbyCreationOrHost || id == null) return false;
        if (IsInTankLobby) await LeaveCurrentLobby();
        processingLobbyCreationOrHost = true;
        bool successful = false;
        try {
            Lobby joinedLobby = await Lobbies.Instance.JoinLobbyByIdAsync(id);
            //CurrentTankLobby = new TankLobby(joinedLobby);
            lobby = joinedLobby;
            await TankVivox.JoinTextAndAudioChannel(lobby.LobbyCode);
            await SubscribeToLobbyEvents();
            await TryJoinTankLobbyRelay();

            /*
            // Join the relay for the lobby
            TankRelay.JoinTankRelayHostResults results = await TankRelay.JoinTankRelayHost(CurrentTankLobby.GetLobbyRelayJoinCode());
            Debug.Log("Joined Relay: " + results.Successful);
            if (results.Successful) {
                // Set this Player's allocationId on the lobby
                await CurrentTankLobby.SetPlayerAllocationId(results.AllocationId);
            }
            */

            TriggerJoinedLobby();
            successful = true;
        } catch (LobbyServiceException e) {
            Debug.Log(e);
        }

        processingLobbyCreationOrHost = false;
        return successful;
    }

    public async Task<bool> JoinLobbyByCode(string code) {
        if (processingLobbyCreationOrHost || code == null) return false;
        if (IsInTankLobby) await LeaveCurrentLobby();
        processingLobbyCreationOrHost = true;
        bool successful = false;
        try {
            Lobby joinedLobby = await Lobbies.Instance.JoinLobbyByCodeAsync(code);
            lobby = joinedLobby;
            await TankVivox.JoinTextAndAudioChannel(lobby.LobbyCode);
            await SubscribeToLobbyEvents();
            await TryJoinTankLobbyRelay();

            /*
            // Join the relay for the lobby
            TankRelay.JoinTankRelayHostResults results = await TankRelay.JoinTankRelayHost(CurrentTankLobby.GetLobbyRelayJoinCode());
            Debug.Log("Joined Relay: " + results.Successful);
            if (results.Successful) {
                // Set this Player's allocationId on the lobby
                await CurrentTankLobby.SetPlayerAllocationId(results.AllocationId);
            }
            */

            TriggerJoinedLobby();
            successful = true;
        } catch (LobbyServiceException e) {
            Debug.Log(e);
        }

        processingLobbyCreationOrHost = false;
        return successful;
    }

    public async Task<bool> QuickJoinAnyLobby() {
        if (processingLobbyCreationOrHost) return false;
        if (IsInTankLobby) await LeaveCurrentLobby();
        processingLobbyCreationOrHost = true;
        bool successful = false;
        try {
            Lobby joinedLobby = await Lobbies.Instance.QuickJoinLobbyAsync();
            lobby = joinedLobby;
            await TankVivox.JoinTextAndAudioChannel(lobby?.LobbyCode);
            await SubscribeToLobbyEvents();
            await TryJoinTankLobbyRelay();

            /*
            // Join the relay for the lobby
            TankRelay.JoinTankRelayHostResults results = await TankRelay.JoinTankRelayHost(CurrentTankLobby.GetLobbyRelayJoinCode());
            Debug.Log("Joined Relay: " + results.Successful);
            if (results.Successful) {
                // Set this Player's allocationId on the lobby
                await CurrentTankLobby.SetPlayerAllocationId(results.AllocationId);
            }
            */

            //await CurrentTankLobby.StartClientRelay();
            TriggerJoinedLobby();
            successful = true;
        } catch (LobbyServiceException e) {
            Debug.Log(e);
        }

        processingLobbyCreationOrHost = false;
        return successful;
    }

    /// <summary>
    /// Get all publicly visible lobbies.
    /// </summary>
    /// <returns>List of Lobby objects.</returns>
    public async Task<List<Lobby>> GetAllLobbies() {
        if (!IsSignedIn()) return null;

        List<Lobby> lobbyList = null;
        try {
            QueryResponse response = await Lobbies.Instance.QueryLobbiesAsync();
            lobbyList = response.Results;
        } catch(LobbyServiceException e) {
            Debug.Log(e);
        }

        return lobbyList;
    }

    /// <summary>
    /// Disconnect from TankTeam.CurrentLobby, regardless if Host or Client TankLobby.
    /// Lobby system will automatically assign a new Host.
    /// </summary>
    /// <returns></returns>
    public async Task LeaveCurrentLobby() {
        if (processingLobbyCreationOrHost || !IsInTankLobby) return;
        processingLobbyCreationOrHost = true;

        try {
            await LobbyService.Instance.RemovePlayerAsync(LobbyId, GameNetcodeManager.Singleton?.PlayerId);
        } catch (System.ArgumentNullException e) {
            Debug.Log(e);
        } catch (LobbyServiceException e) {
            Debug.Log(e);
        }

        await DisposeTankLobby();
        processingLobbyCreationOrHost = false;
    }

    /// <summary>
    /// Removes self from the lobby and ends any existing TankRelay connection.
    /// </summary>
    private async Task DisposeTankLobby() {
        //if (CurrentTankLobby == null) return;
        Debug.Log("Disposing TankLobby");
        try {
            if(LobbyId != null && GameNetcodeManager.Singleton?.PlayerId != null)
                await LobbyService.Instance.RemovePlayerAsync(LobbyId, GameNetcodeManager.Singleton?.PlayerId);
        } catch (LobbyServiceException) {
            Debug.Log("Attempted to RemovePlayerAsync and got LobbyServiceExcepion.  Player may have already been removed.  Not a big problem.");
        }

        await TankVivox.LeaveChannel(lobby?.LobbyCode);
        await TankRelay.Singleton.ShutdownRelay();
        await UnsubscribeFromLobbyEvents();
        lobby = null;
    }

    private bool IsSignedIn() {
        return AuthenticationService.Instance.IsSignedIn;
    }

    // ==================================================================================================================

    // ==================================================================================================================

    // ==================================================================================================================

    /// <summary>
    /// Begins a Relay session, sets the hosts Player AllocationId, updates the lobby with the new Relay joinCode,
    /// and network-loads the map associated with the provided mapName.  Only works for Lobby Host.
    /// </summary>
    /// <param name="mapName"></param>
    /// <returns>True if successful, false otherwise.</returns>
    public async Task<bool> LaunchMatch(string mapName) {
        if (launchingMatch || !IsInTankLobby || !IsLobbyHost || !TankMapLoader.Singleton.IsMapNameValid(mapName)) return false;


        launchingMatch = true;

        TankRelay.CreateTankRelayResults results = await TankRelay.Singleton.CreateTankRelay(MaxPlayerCount.Value);

        if (!results.Successful) {
            launchingMatch = false;
            return false;
        }

        bool didMapLoad = TankMapLoader.Singleton.LoadMapNetwork(mapName);

        if (!didMapLoad) {
            await TankRelay.Singleton.ShutdownRelay();
            launchingMatch = false;
            return false;
        }

        await SetPlayerAllocationId(results.AllocationId);

        await SetLobbyDataRelayJoinCode(results.JoinCode);

        launchingMatch = false;

        return true;
    }

    private async Task SetPlayerAllocationId(Guid allocationId) {
        // Set player data allocationId
        UpdatePlayerOptions playerOptions = new UpdatePlayerOptions();
        playerOptions.AllocationId = allocationId.ToString();

        try {
            await LobbyService.Instance.UpdatePlayerAsync(LobbyId, GameNetcodeManager.Singleton?.PlayerId, playerOptions);
        } catch (ArgumentNullException e) {
            Debug.Log(e);
        } catch (LobbyServiceException e) {
            Debug.Log(e);
        }
    }

    /// <summary>
    /// Gets the JoinCode for the TankRelayHost associated with this TankLobby. Returns null if no JoinCode has been set in the lobby.Data Dictionary by the Host.
    /// </summary>
    /// <returns></returns>
    private string GetLobbyDataRelayJoinCode() {
        if (!IsInTankLobby || lobby.Data == null || lobby.Data == null) return null;
        DataObject relayJoinCodeData;
        bool gotRelayJoinCode = lobby.Data.TryGetValue(LOBBYDATARELAYJOINCODE, out relayJoinCodeData);
        if (!gotRelayJoinCode) return null;

        string relayJoinCode = relayJoinCodeData.Value;
        return relayJoinCode;
    }

    /// <summary>
    /// Sets the Lobby Relay JoinCode in lobby.Data.  Only callable as a Host, while in a Lobby, and after creating a RelayHost Allocation and getting the associated JoinCode.
    /// </summary>
    /// <param name="joinCode"></param>
    private async Task<bool> SetLobbyDataRelayJoinCode(string joinCode) {
        if (!IsInTankLobby || !IsLobbyHost) return false;

        UpdateLobbyOptions lobbyOptions = new UpdateLobbyOptions();
        lobbyOptions.Data = new Dictionary<string, DataObject>();
        lobbyOptions.Data.Add(LOBBYDATARELAYJOINCODE, new DataObject(DataObject.VisibilityOptions.Member, joinCode));

        try {
            await LobbyService.Instance.UpdateLobbyAsync(LobbyId, lobbyOptions);
            return true;
        } catch (LobbyServiceException e) {
            Debug.Log(e);
        }

        return false;
    }

    /// <summary>
    /// Set the current MapName in the lobby.Data.  Only callable as Host and while in a Lobby.
    /// </summary>
    /// <param name="mapName"></param>
    /// <returns></returns>
    public async Task<bool> SetLobbyDataMapName(string mapName) {
        if (!IsInTankLobby || !IsLobbyHost) return false;

        UpdateLobbyOptions lobbyOptions = new UpdateLobbyOptions();
        lobbyOptions.Data = new Dictionary<string, DataObject> {
            { LOBBYDATAMAPKEY, new DataObject(DataObject.VisibilityOptions.Public, mapName) }
        };

        try {
            await LobbyService.Instance.UpdateLobbyAsync(LobbyId, lobbyOptions);
            Debug.Log("set lobby data map name");
            return true;
        } catch (LobbyServiceException e) {
            Debug.Log(e);
        }

        return false;
    }

    public string GetLobbyDataMapName(Lobby anyLobby) {
        if (anyLobby == null || anyLobby.Data == null) return null;
        DataObject lobbyMapNameData;

        bool gotLobbyMapName = anyLobby.Data.TryGetValue(LOBBYDATAMAPKEY, out lobbyMapNameData);
        if (!gotLobbyMapName) return null;

        string relayJoinCode = lobbyMapNameData.Value;
        return relayJoinCode;
    }

    /// <summary>
    /// Set your username in the currently connected Lobby
    /// </summary>
    /// <param name="desiredUsername"></param>
    private async Task SetPlayerDataCustomUsername() {
        string username = await AuthenticationService.Instance.GetPlayerNameAsync();//AuthenticationService.Instance.PlayerName;
        Debug.Log("SetPlayerDataCustomUsername() called, username: " + username);
        UpdatePlayerOptions options = new UpdatePlayerOptions();
        options.Data = new Dictionary<string, PlayerDataObject> {
            { PLAYERDATAUSERNAME, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, username) }
        };
        Debug.Log("PlayerId used for updating player: " + GameNetcodeManager.Singleton?.PlayerId);
        await Lobbies.Instance.UpdatePlayerAsync(LobbyId, GameNetcodeManager.Singleton?.PlayerId, options);
    }

    /// <summary>
    /// Sets all entries in lobby.Data to null. Only works for Host.
    /// </summary>
    private async Task ResetLobbyData() {
        if (!IsInTankLobby || !IsLobbyHost) return;

        UpdateLobbyOptions lobbyOptions = new UpdateLobbyOptions();
        lobbyOptions.Data = new Dictionary<string, DataObject>();
        lobbyOptions.Data.Add(LOBBYDATARELAYJOINCODE, null);
        lobbyOptions.Data.Add(LOBBYDATAMAPKEY, null);

        try {
            await LobbyService.Instance.UpdateLobbyAsync(LobbyId, lobbyOptions);
        } catch (LobbyServiceException e) {
            Debug.Log(e);
        }
    }

    /// <summary>
    /// Attempt to join the TankRelay associated with the current TankLobby.
    /// </summary>
    /// <returns>TankRelay.JoinTankRelayResults with pertinent information within.</returns>
    private async Task<TankRelay.JoinTankRelayResults> TryJoinTankLobbyRelay() {
        string relayCode = GetLobbyDataRelayJoinCode();
        if (relayCode != null) {
            TankRelay.JoinTankRelayResults results = await TankRelay.Singleton.JoinTankRelay(relayCode);
            if(results.Successful)
                await SetPlayerAllocationId(results.AllocationId);
            return results;
        }

        return default;
    }

    private async Task SubscribeToLobbyEvents() {
        if (!IsInTankLobby) return;
        LobbyEventCallbacks callbacks = new LobbyEventCallbacks();
        callbacks.LobbyChanged += OnLobbyChanged;
        callbacks.LobbyEventConnectionStateChanged += OnLobbyConnectionStateChanged;
        callbacks.KickedFromLobby += OnKickedFromLobby;

        try {
            lobbyEvents = await Lobbies.Instance.SubscribeToLobbyEventsAsync(LobbyId, callbacks);
        } catch (System.Exception e) {
            Debug.Log(e);
        }
    }

    private async Task UnsubscribeFromLobbyEvents() {
        if (lobbyEvents == null) return;

        await lobbyEvents.UnsubscribeAsync();
    }

    /// <summary>
    /// Creates initial connection to open the lobby up for connections.  This does *not* start a match.  A lobby must be created before a match can be started.
    /// AuthenticationService.Instance.IsSignedIn must be true to succeed.  Use a GameNetcodeManager.SignInToUnityServices() function to sign in first.
    /// </summary>
    private async Task<bool> StartLobby(string lobbyName, int maxPlayerCount, bool isPrivate, string password = null) {
        if (!IsSignedIn()) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        if (IsInTankLobby) await DisposeTankLobby();
        if (IsInTankLobby || string.IsNullOrWhiteSpace(lobbyName) || !IsSignedIn()) return false;

        try {
            CreateLobbyOptions options = new CreateLobbyOptions();
            options.IsPrivate = isPrivate;
            if (password != null) options.Password = password;
            lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayerCount, options);

            // Join a channel for this lobby
            //await TankVivox.JoinTextAndAudioChannel(lobby.LobbyCode);
            

            heartbeatTimer = heartbeatTimerMax;
            return true;
        } catch (InvalidOperationException e) {
            Debug.Log(e);
        } catch(LobbyServiceException e) {
            Debug.Log(e);
        }

        return false;
    }

    private async Task<bool> EndLobby() {
        if (!IsLobbyHost || lobby == null || lobby.Id == null) return false;

        try {
            await LobbyService.Instance.DeleteLobbyAsync(LobbyId);
            await DisposeTankLobby();
        } catch (LobbyServiceException e) {
            Debug.Log(e);
        }

        return true;
    }

    /// <summary>
    /// Kick the provided Player.  Only usable if IsLobbyHost.
    /// </summary>
    /// <param name="player"></param>
    public async void KickPlayer(Player player) {
        if (player == null || !IsInTankLobby || !IsLobbyHost) return;
        
        try {
            await LobbyService.Instance.RemovePlayerAsync(LobbyId, player.Id);
        } catch (ArgumentNullException e) {
            Debug.Log(e);
        } catch (LobbyServiceException e) {
            Debug.Log(e);
        }
    }

    /// <summary>
    /// Event handler for joining lobby
    /// </summary>
    private void OnJoinedLobby() {
        //SetPlayerDataCustomUsername();
    }

    private void HandleHeartbeat(float deltaTime) {
        if (!IsInTankLobby || !IsLobbyHost) return;
        
        heartbeatTimer -= deltaTime;
        if(heartbeatTimer < 0f) {
            SendHeartbeat();
            heartbeatTimer = heartbeatTimerMax;
        }
    }

    private async void SendHeartbeat() {
        if (!IsInTankLobby || !IsLobbyHost) return;

        await LobbyService.Instance.SendHeartbeatPingAsync(LobbyId);
    }

    private Player GetPlayerByPlayerIndex(int playerIndex) {
        return LobbyPlayers[playerIndex];
    }

    private List<Player> GetPlayerListByIndices(List<int> indices) {
        if (indices == null || !IsInTankLobby) return new List<Player>();
        List<Player> indexedPlayers = new List<Player>();
        List<Player> allPlayers = LobbyPlayers;
        for(int i = 0; i < indices.Count; i++) {
            indexedPlayers.Add(allPlayers[indices[i]]);
        }

        return indexedPlayers;
    }

    /// <summary>
    /// Pulls Lobby data from Unity servers using GetLobbyAsync() using the local LobbyId
    /// </summary>
    private async Task ManuallyUpdateLobby() {
        Lobby updatedLobby = await Lobbies.Instance.GetLobbyAsync(LobbyId);
        if (updatedLobby != null) {
            lobby = updatedLobby;
            LobbyChanged?.Invoke();
        }
    }

    // LOBBY EVENT HANDLERS ======================================================
    private void OnLobbyChanged(ILobbyChanges changes) {
        if (lobby == null) return;

        List<Player> playersThatLeft = null;
        if (changes.PlayerLeft.Changed) {
            playersThatLeft = GetPlayerListByIndices(changes.PlayerLeft.Value);

            // If player leaves lobby, remove them from the Relay
            /*
            for (int i = 0; i < playersThatLeft.Count; i++) {
                //NetworkManager.Singleton.DisconnectClient(playersThatLeft[i].)
            }
            */
        }
        Debug.Log("lobby changes occurred");
        changes.ApplyToLobby(lobby);

        if (changes.Name.Changed) {
            TriggerNameChanged(changes.Name.Value);
        }

        if(changes.PlayerJoined.Changed) {
            Debug.Log("player joined");
            TriggerPlayersJoined(changes.PlayerJoined.Value);
        }

        if (changes.PlayerLeft.Changed) {
            TriggerPlayersLeft(playersThatLeft);
        }

        if (changes.LobbyDeleted) {
            TriggerLobbyDeleted();
        }

        if (changes.IsPrivate.Changed) {
            TriggerIsPrivateChanged(changes.IsPrivate.Value);
        }

        if (changes.HasPassword.Changed) {
            TriggerHasPasswordChanged(changes.HasPassword.Value);
        }

        if (changes.Data.Changed) {
            if (changes.Data.Value.ContainsKey(LOBBYDATARELAYJOINCODE) && !IsLobbyHost) {
                _ = TryJoinTankLobbyRelay();
            }

            TriggerLobbyDataChangedOrRemoved(changes.Data.Value);
        }

        if (changes.PlayerData.Changed) {
            Debug.Log("Player data change occurred");
            foreach(int key in changes.PlayerData.Value.Keys) {
                LobbyPlayerChanges playerChanges = changes.PlayerData.Value[key];
                Debug.Log(key + " : " + playerChanges);
                Debug.Log(playerChanges.ChangedData);
                if (playerChanges.ChangedData.Value == null) continue;
                foreach(var key2 in playerChanges.ChangedData.Value.Keys) {
                    ChangedOrRemovedLobbyValue<PlayerDataObject> changedPlayerDataUnparsed = playerChanges.ChangedData.Value[key2];
                    PlayerDataObject changedPlayerData = changedPlayerDataUnparsed.Value;
                    Debug.Log("Key: " + key2 + " ----- Value: " + changedPlayerData.Value);
                    Player player = GetPlayerByPlayerIndex(playerChanges.PlayerIndex);
                    Debug.Log(player);

                    if(key2 == PLAYERDATAUSERNAME && player != null) {
                        PlayerChangedUsername?.Invoke(player, changedPlayerData.Value);
                    }
                }
            }
        }

        if (changes.HostId.Changed) {
            TriggerHostIdChanged(changes.HostId.Value);
        }

        LobbyChanged?.Invoke();
    }

    private async void OnKickedFromLobby() {
        //TankRelay.DisposeCurrentTankRelay();
        Debug.Log("OnKickedFromLobby() called.");
        await DisposeTankLobby();

        TriggerKickedFromLobby();
    }

    private async void OnLobbyConnectionStateChanged(LobbyEventConnectionState state) {
        Debug.Log("Lobby connection state changed: " + state.ToString());
        if(state == LobbyEventConnectionState.Subscribed) {
            await SetPlayerDataCustomUsername();
            await ManuallyUpdateLobby();
        }
    }

    private void TriggerJoinedLobby() {
        JoinedLobby?.Invoke();
    }

    private void TriggerKickedFromLobby() {
        KickedFromLobby?.Invoke();
    }

    private void TriggerNameChanged(string newName) {
        NameChanged?.Invoke(newName);
    }

    private void TriggerPlayersJoined(List<LobbyPlayerJoined> playersThatJoined) {
        PlayersJoined?.Invoke(playersThatJoined);
    }

    private void TriggerPlayersLeft(List<Player> playersThatLeft) {
        PlayersLeft?.Invoke(playersThatLeft);
    }

    private void TriggerLobbyDeleted() {
        LobbyDeleted?.Invoke();
    }

    private void TriggerIsPrivateChanged(bool newIsPrivate) {
        IsPrivateChanged?.Invoke(newIsPrivate);
    }

    private void TriggerHasPasswordChanged(bool newHasPassword) {
        HasPasswordChanged?.Invoke(newHasPassword);
    }

    private void TriggerLobbyDataChangedOrRemoved(Dictionary<string, ChangedOrRemovedLobbyValue<DataObject>> changedOrRemovedLobbyData) {
        LobbyDataChangedOrRemoved?.Invoke(changedOrRemovedLobbyData);
    }

    private void TriggerHostIdChanged(string newHostId) {
        HostIdChanged?.Invoke(newHostId);
    }
}