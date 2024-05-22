using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class TankRelay : NetworkBehaviour {
    [SerializeField] private TankNetworkPlayer _tankNetworkPlayerPrefab;
    [SerializeField] private TankAIPlayer _tankAIPlayerPrefab;

    public static TankRelay Singleton { get; private set; } = null;

    public AMatchLogic MatchLogic { get; private set; }

    /// <summary>
    /// Contains all TankNetworkPlayers, all entries exist in TankPlayerDictionary as well. Key is ClientID and value is associated TankNetworkPlayer.
    /// </summary>
    private Dictionary<ulong, TankNetworkPlayer> RelayTankPlayerDictionary = new Dictionary<ulong, TankNetworkPlayer>();

    /// <summary>
    /// Contains all TankAIPlayers, all entries exist in TankPlayerDictionary as well.
    /// </summary>
    private List<TankAIPlayer> RelayTankAIPlayerList = new List<TankAIPlayer>();

    /// <summary>
    /// <para>Functional on: Server, Client</para>
    /// Contains all ATankPlayers. Key is TankPlayerID and value is associated ATankPlayer.
    /// </summary>
    private Dictionary<int, ATankPlayer> TankPlayerDictionary = new Dictionary<int, ATankPlayer>();

    private Dictionary<ulong, string> ClientIDToUsername = new Dictionary<ulong, string>();

    /// <summary>
    /// Count of all TankNetworkPlayers connected to this Relay
    /// </summary>
    public int TankNetworkPlayerCount { get { return RelayTankPlayerDictionary.Count; } }

    /// <summary>
    /// Count of all TankAIPlayers existing on this Relay
    /// </summary>
    public int TankAIPlayerCount { get { return RelayTankAIPlayerList.Count; } }

    /// <summary>
    /// Count of all ATankPlayers existing on this Relay (all Players, AI, etc. combined)
    /// </summary>
    public int TankPlayerCount { get { return TankPlayerDictionary.Count; } }

    private Action<ulong, ConnectionStatus> OnClientConnection;

    /// <summary>
    /// Invoked when an ATankPlayer is instantiated and registered with this TankRelay.
    /// </summary>
    public static Action<ATankPlayer> TankPlayerRegistered;

    /// <summary>
    /// Invoked when an ATankPlayer is unregistered with this TankRelay.
    /// </summary>
    public static Action<ATankPlayer> TankPlayerUnregistered;

    /// <summary>
    /// Invoked when a player completes loading the scene.
    /// </summary>
    public static Action<TankNetworkPlayer> TankPlayerLoadComplete;

    /// <summary>
    /// Called when the connection is stopped. Unsubscribing is not necessary.
    /// </summary>
    /// <returns>True if was running as a Host, false otherwise.</returns>
    public static Action<bool> ClientStopped;

    public int MaxConnectedClients { get; private set; } = 10;

    public struct CreateTankRelayResults {
        private bool successful;
        private Guid allocationId;
        private string joinCode;

        public bool Successful { get { return successful; } }
        public Guid AllocationId { get { return allocationId; } }
        public string JoinCode { get { return joinCode; } }

        public CreateTankRelayResults(Guid _allocationId, string _joinCode) {
            successful = true;
            allocationId = _allocationId;
            joinCode = _joinCode;
        }
    }

    public struct JoinTankRelayResults {
        private bool successful;
        private Guid allocationId;

        public bool Successful { get { return successful; } }
        public Guid AllocationId { get { return allocationId; } }

        public JoinTankRelayResults(Guid _allocationId, string _joinCode) {
            successful = true;
            allocationId = _allocationId;
        }
    }

    public enum ConnectionStatus {
        Connected,
        Disconnected
    }

    //private TankRelay() { }

    void Awake() {
        if(Singleton != null) {
            Destroy(this);
            return;
        }

        Singleton = this;

        ATankPlayer.TankPlayerCreated += OnTankPlayerCreated;
        ATankPlayer.TankPlayerDestroyed += OnTankPlayerDestroyed;
    }

    void Update() {
        
    }

    public override void OnDestroy() {
        base.OnDestroy();

        ATankPlayer.TankPlayerCreated -= OnTankPlayerCreated;
        ATankPlayer.TankPlayerDestroyed -= OnTankPlayerDestroyed;
    }

    public async Task<CreateTankRelayResults> CreateTankRelay(int maxPlayerCount) {
        if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient) await ShutdownRelay();

        bool signedIn = await SignInUnityServicesIfNotSignedIn();
        if (!signedIn) return new CreateTankRelayResults();

        maxPlayerCount = Mathf.Clamp(maxPlayerCount, 1, 24);

        string playerName = "Not Set";
        try {
            playerName = await AuthenticationService.Instance.GetPlayerNameAsync();
        } catch (AuthenticationException e) {
            Debug.Log(e.StackTrace);
        } catch (RequestFailedException e) {
            Debug.Log(e.StackTrace);
        }
        NetworkManager.Singleton.NetworkConfig.ConnectionData = Encoding.ASCII.GetBytes(playerName);

        // Create the allocation
        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayerCount - 1);

        // Update the UnityTransport Relay with the allocation
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(allocation, "dtls"));

        // Get the joinCode for this allocation
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

        SubscribeToEventsServer();

        // Since the NetworkManager UnityTransport is now aware of the allocation, Start the NetworkManager Host
        string startHostResult = NetworkManager.Singleton.StartHost() ? joinCode : null;

        if (startHostResult == null) {
            await ShutdownRelay();
            return new CreateTankRelayResults();
        }

        // Server started successfully
        MaxConnectedClients = maxPlayerCount;

        // Have to subscribe to this AFTER the server has been started, otherwise the networked SceneManager is null
        NetworkManager.Singleton.SceneManager.OnSceneEvent += NetworkedSceneManager_OnSceneEvent;

        return new CreateTankRelayResults(allocation.AllocationId, joinCode);
    }

    /// <summary>
    /// Join an existing Relay Allocation with given Relay joinCode.
    /// </summary>
    /// <param name="joinCode"></param>
    /// <returns></returns>
    public async Task<JoinTankRelayResults> JoinTankRelay(string joinCode) {
        if (string.IsNullOrWhiteSpace(joinCode)) return default;

        if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient) await ShutdownRelay();

        bool signedIn = await SignInUnityServicesIfNotSignedIn();
        if (!signedIn) return default;

        // Set connection data to send username in payload to server
        string playerName = "Not Set";
        try {
            playerName = await AuthenticationService.Instance.GetPlayerNameAsync();
        } catch(AuthenticationException e) {
            Debug.Log(e.StackTrace);
        } catch(RequestFailedException e) {
            Debug.Log(e.StackTrace);
        }

        NetworkManager.Singleton.NetworkConfig.ConnectionData = Encoding.ASCII.GetBytes(playerName);

        // Join via joinCode
        JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

        NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));

        bool isClientConnected = NetworkManager.Singleton.StartClient();

        if (!isClientConnected) return default;

        SubscribeToEventsClient();

        JoinTankRelayResults results = new JoinTankRelayResults(joinAllocation.AllocationId, joinCode);

        return results;
    }

    /// <summary>
    /// Shutdown the Relay connection, regardless if client or server.
    /// </summary>
    public async Task ShutdownRelay() {
        Debug.Log("Shutting Down Relay");
        RelayTankPlayerDictionary.Clear();

        UnsubscribeFromNetworkManagerEvents();

        NetworkManager.Singleton.Shutdown();

        // return when Shutdown is complete
        Task waitTask = Task.Run(async () => {
            while (NetworkManager.Singleton.ShutdownInProgress) await Task.Delay(10);
        });

        if (waitTask != await Task.WhenAny(waitTask, Task.Delay(200))) return;
    }

    /// <summary>
    /// Get a TankNetworkPlayer associated with the provided clientId.
    /// </summary>
    /// <param name="clientId"></param>
    /// <returns>NetworkPlayer instance, null if not found.</returns>
    public TankNetworkPlayer GetTankPlayer(ulong clientId) {
        TankNetworkPlayer tankPlayer;
        RelayTankPlayerDictionary.TryGetValue(clientId, out tankPlayer);
        return tankPlayer;
    }

    /// <summary>
    /// <para>Functional on: Server, Client</para>
    /// Get an ATankPlayer associated with the provided TankPlayerID.
    /// </summary>
    /// <param name="tankPlayerID"></param>
    /// <returns></returns>
    public ATankPlayer GetTankPlayer(int tankPlayerID) {
        ATankPlayer tankPlayer;
        TankPlayerDictionary.TryGetValue(tankPlayerID, out tankPlayer);
        return tankPlayer;
    }

    /// <summary>
    /// <para>Functional on: Server, Client</para>
    /// Get a List of all ATankPlayers registered with this TankRelay.
    /// This function uses ToList(), so best to not call it at high intervals (like in Update).
    /// </summary>
    /// <returns>List of all ATankPlayers</returns>
    public List<ATankPlayer> GetTankPlayers() {
        return TankPlayerDictionary.Values.ToList();
    }

    // ===============================================================================

    // PRIVATE MEMBERS ===============================================================

    // ===============================================================================

    /// <summary>
    /// Used to register a Client with clientId.
    /// </summary>
    /// <param name="clientId"></param>
    /// <returns>The created NetworkPlayer. Null if failed.</returns>
    private TankNetworkPlayer RegisterTankNetworkPlayer(ulong clientId) {
        if (_tankNetworkPlayerPrefab == null) {
            Debug.Log("Couldn't register TankNetworkPlayer, TankNetworkPlayer prefab is null.");
            return null;
        }

        bool clientIdIsUsed = RelayTankPlayerDictionary.ContainsKey(clientId);
        TankNetworkPlayer tankNetworkPlayer = null;

        Debug.Log("RegisterTankNetworkPlayer was called.");
        if (!clientIdIsUsed) {
            //tankPlayer = new TankNetworkPlayer(clientId);
            tankNetworkPlayer = Instantiate(_tankNetworkPlayerPrefab);
            tankNetworkPlayer.GetComponent<NetworkObject>()?.Spawn();
            tankNetworkPlayer.ClientID = clientId;
            string username;
            tankNetworkPlayer.Username = (ClientIDToUsername.TryGetValue(clientId, out username)) ? username : "NoName";
            RelayTankPlayerDictionary.Add(clientId, tankNetworkPlayer);
            //TankPlayerDictionary.Add(tankNetworkPlayer.TankPlayerID, tankNetworkPlayer);
            Debug.Log("TankNetworkPlayer Registered");
        }

        return tankNetworkPlayer;
    }

    /// <summary>
    /// Used to unregister a Client with clientId who just disconnected from the server/host
    /// </summary>
    /// <param name="clientId"></param>
    private void UnregisterTankNetworkPlayer(ulong clientId) {
        TankNetworkPlayer tankPlayerThatLeft;
        if (!RelayTankPlayerDictionary.TryGetValue(clientId, out tankPlayerThatLeft)) return;
        RelayTankPlayerDictionary.Remove(clientId);
        //TankPlayerDictionary.Remove(tankPlayerThatLeft.TankPlayerID);
        NetworkObject NO = tankPlayerThatLeft.GetComponent<NetworkObject>();
        if (NO == null || !NO.IsSpawned) return;
        NO.Despawn();
        //if (tankPlayerThatLeft != null) TankPlayerUnregistered?.Invoke(tankPlayerThatLeft);

        // Fill empty slots with AI
        //if (MatchLogic?.SpawnAIEnabled == true) FillEmptyPlayerSlotsWithAI();
    }

    /// <summary>
    /// Create and register a TankAIPlayer
    /// </summary>
    private void RegisterTankAIPlayer() {
        TankAIPlayer tankAIPlayer = new TankAIPlayer();
        RelayTankAIPlayerList.Add(tankAIPlayer);
        //TankPlayerDictionary.Add(tankAIPlayer.TankPlayerID, tankAIPlayer);
        TankPlayerRegistered?.Invoke(tankAIPlayer);
        Debug.Log("TankAIPlayer Registered");
    }

    /// <summary>
    /// Unregister a TankAIPlayer with provided TankPlayerID
    /// </summary>
    /// <param name="tankPlayerID"></param>
    private void UnregisterTankAIPlayer(int tankPlayerID) {
        ATankPlayer tankAIPlayerToRemove = TankPlayerDictionary[tankPlayerID];
        if (!(tankAIPlayerToRemove is TankAIPlayer)) return;

        RelayTankAIPlayerList.Remove((TankAIPlayer)tankAIPlayerToRemove);
        //TankPlayerDictionary.Remove(tankPlayerID);
        if (tankAIPlayerToRemove != null) TankPlayerUnregistered?.Invoke(tankAIPlayerToRemove);
    }

    private void OnTankPlayerCreated(ATankPlayer player) {
        TankPlayerDictionary.Add(player.TankPlayerID, player);
        Debug.Log("OnTankPlayerCreated() invoked event.");
        TankPlayerRegistered?.Invoke(player);
    }

    private void OnTankPlayerDestroyed(ATankPlayer player) {
        TankPlayerDictionary.Remove(player.TankPlayerID);
        Debug.Log("OnTankPlayerDestroyed() invoked event.");
        TankPlayerUnregistered?.Invoke(player);
    }

    /// <summary>
    /// Fill server with AI players until at MaxConnectedClients limit
    /// </summary>
    private void FillEmptyPlayerSlotsWithAI() {
        for(int i = TankPlayerCount; i < MaxConnectedClients; i++) {
            RegisterTankAIPlayer();
        }
    }

    private void UnregisterTankAIPlayers(int count) {
        for(int i = 0; i < count; i++) {
            if (TankAIPlayerCount <= 0) break;
            TankAIPlayer player = RelayTankAIPlayerList[RelayTankAIPlayerList.Count-1];
            UnregisterTankAIPlayer(player.TankPlayerID);
        }
    }

    public void EnableAIPlayers() {
        //FillEmptyPlayerSlotsWithAI();
    }

    public void DisableAIPlayers() {
        for(int i = RelayTankAIPlayerList.Count-1; i > -1; i--) {
            UnregisterTankAIPlayer(RelayTankAIPlayerList[i].TankPlayerID);
        }
    }

    private void SubscribeToEventsServer() {
        // Subscribe to connection events on the NetworkManager
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedCallback;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectCallback;

        // Subscribe to own handler for connection events.
        // Subcribing before starting host so hosts client connection is registered.
        OnClientConnection += OnClientConnectionHandler;

        // Set the ConnectionApprovalCallback and OnSceneEvent listener
        NetworkManager.Singleton.ConnectionApprovalCallback = ConnectionApprovalCallback;

        // Client disconnection callback
        NetworkManager.Singleton.OnClientStopped += ClientStoppedCallback;

        AMatchLogic.MatchLogicLoaded += OnMatchLogicLoaded;
        AMatchLogic.MatchEnded += OnMatchEnded;
        AMatchLogic.PostMatchEnded += OnPostMatchEnded;

        TankTeam.PlayerLeftTeam += OnTankPlayerLeftTeam;
        TankRelay.TankPlayerUnregistered += OnTankPlayerUnregistered;
    }

    private void SubscribeToEventsClient() {
        NetworkManager.Singleton.SceneManager.OnSceneEvent += NetworkedSceneManager_OnSceneEvent;

        // Subscribe to connection events on the NetworkManager
        //NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedCallback;
        //NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectCallback;

        // Client disconnection callback
        NetworkManager.Singleton.OnClientStopped += ClientStoppedCallback;
    }

    private void UnsubscribeFromNetworkManagerEvents() {
        // Subscribe to connection events on the NetworkManager
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedCallback;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectCallback;

        // Subscribe to own handler for connection events.
        // Subcribing before starting host so hosts client connection is registered.
        OnClientConnection -= OnClientConnectionHandler;

        // Set the ConnectionApprovalCallback and OnSceneEvent listener
        //NetworkManager.Singleton.ConnectionApprovalCallback = ConnectionApprovalCallback;

        if(NetworkManager.Singleton.SceneManager != null)
            NetworkManager.Singleton.SceneManager.OnSceneEvent -= NetworkedSceneManager_OnSceneEvent;

        // Client disconnection callback
        NetworkManager.Singleton.OnClientStopped -= ClientStoppedCallback;

        AMatchLogic.MatchLogicLoaded -= OnMatchLogicLoaded;
        AMatchLogic.MatchEnded -= OnMatchEnded;
        AMatchLogic.PostMatchEnded -= OnPostMatchEnded;

        TankTeam.PlayerLeftTeam -= OnTankPlayerLeftTeam;
        TankRelay.TankPlayerUnregistered -= OnTankPlayerUnregistered;
    }

    private void OnTankPlayerLeftTeam(ATankPlayer tankPlayer, TankTeam team) {
        if (tankPlayer == null) return;

        /*
        if(tankPlayer is TankAIPlayer) {
            UnregisterTankAIPlayer(tankPlayer.TankPlayerID);
        }
        else if((tankPlayer is TankNetworkPlayer) && MatchLogic.SpawnAIEnabled) {
            RegisterTankAIPlayer();
        }
        */
    }

    private void OnTankPlayerUnregistered(ATankPlayer tankPlayer) {
        //tankPlayer.KillPlayerGameObject();
    }

    private void OnMatchEnded() {
        Debug.Log("OnMatchEnded() called in TankRelay.");

        // Begin voting process

        // Choose 3 random maps to be selectable for voting
        string[] randomMapNames = TankMapLoader.Singleton.GetRandomMapNames(3);

        FixedString128Bytes[] fixedStringMapNames = TankGameUtilities.ToFixedString128BytesArray(randomMapNames);

        MapVote.Singleton.StartVote(randomMapNames);
        GameNetcodeManager.Singleton.StartMapVoteClientRpc(fixedStringMapNames);

        /*
        MapVote.Singleton.CastVote(0, 0);
        MapVote.Singleton.CastVote(0, 2);
        MapVote.Singleton.CastVote(0, 2);
        */
    }

    private async void OnPostMatchEnded() {
        Debug.Log("OnPostMatchEnded() called in TankRelay.");

        // Clear previous match logic if it exists
        MatchLogic.Dispose();
        MatchLogic = null;

        // Get the winner of the vote
        int winningMapIndex = MapVote.Singleton.GetWinningMapIndex();
        int[] voteCounts = MapVote.Singleton.GetVoteCounts();
        string winningMapName = null;
        if(winningMapIndex > -1) {
            string[] mapNames = MapVote.Singleton.GetVoteMapNames();
            winningMapName = mapNames[winningMapIndex];
            Debug.Log($"Winning mapname is {winningMapName} with number of votes {voteCounts[winningMapIndex]}");
        }
        /*
        string winningMapName = MapVote.Singleton.GetWinningMap();
        if(winningMapName != null) {
            Debug.Log($"Map vote ended, winning map is: {winningMapName}");
        }
        */


        //await ShutdownRelay();

        //TankMapLoader.LoadMainMenuNetwork();
        if(winningMapName == null) {
            Debug.Log("WINNING MAP NAME WAS NULL IN OnPostMatchEnded()!");

        } else {

            bool mapLoaded = TankMapLoader.Singleton.LoadMapNetwork(winningMapName);
        }

        /*
        if (TankLobby.MaxPlayerCount != null) {
            TankMapLoader.LoadMainMenuNetwork();
            mapLoaded = TankMapLoader.LoadMapNetwork(winningMapName);
        }
        */

        //if (!mapLoaded) await ShutdownRelay();
    }

    private void ConnectionApprovalCallback(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response) {
        string sentUsername = Encoding.ASCII.GetString(request.Payload);
        if(sentUsername != null) {
            Debug.Log("ConnectionApprovalCallback received username: " + sentUsername);
            ClientIDToUsername.Add(request.ClientNetworkId, sentUsername);
        }

        response.Approved = true;
        response.CreatePlayerObject = false;
    }

    private void ClientStoppedCallback(bool wasHost) {
        Debug.Log("TankRelay.OnClientStopped() called.  wasHost: " + wasHost);
        ClientStopped?.Invoke(wasHost);
    }

    private void OnClientConnectedCallback(ulong clientId) {
        OnClientConnection?.Invoke(clientId, ConnectionStatus.Connected);
    }

    private void OnClientDisconnectCallback(ulong clientId) {
        OnClientConnection?.Invoke(clientId, ConnectionStatus.Disconnected);
    }

    // This function is subscribed to OnClientConnection, which is called by the above two events that are both triggered by NetworkManager.Singleton
    private void OnClientConnectionHandler(ulong clientId, ConnectionStatus connectionStatus) {
        Debug.Log("OnClientConnectionHandler triggered");
        if (NetworkManager.Singleton.IsServer) {
            switch (connectionStatus) {
                case ConnectionStatus.Connected:
                //TankNetworkPlayer tankPlayer = RegisterTankNetworkPlayer(clientId);

                /*
                if (tankPlayer == null) tankPlayer = GetTankPlayer(clientId);
                Debug.Log("Player connected to Relay with clientId " + clientId);
                Debug.Log($"tankPlayer: {tankPlayer} --- MatchLogic: {MatchLogic}");
                */

                /*
                if (tankPlayer != null && MatchLogic != null) {
                    MatchLogic.TeamManager.AutoAssignPlayerToTeam(tankPlayer);
                    tankPlayer.SetPlayerPrefab(MatchLogic.DefaultPlayerPrefab);
                }
                */

                // Remove an AI player. None will be removed if none exist
                //UnregisterTankAIPlayers(1);

                TankNetworkPlayer tankPlayer = RegisterTankNetworkPlayer(clientId);

                    break;
                case ConnectionStatus.Disconnected:
                    UnregisterTankNetworkPlayer(clientId);
                    ClientIDToUsername.Remove(clientId);
                    Debug.Log("Player disconnected from Relay with clientId " + clientId);
                    break;
            }
        } else if (NetworkManager.Singleton.IsClient) {
            Debug.Log("Client disconnected from relay");
        }
    }

    private void OnMatchLogicLoaded(AMatchLogic logic) {
        MatchLogic?.Dispose();
        MatchLogic = null;
        if (logic == null) {
            Debug.Log("TankRelay.OnMatchLogicLoaded(): The matchlogic extracted was invalid (null)!");
            return;
        }
        MatchLogic = logic;

        //if (MatchLogic.SpawnAIEnabled) FillEmptyPlayerSlotsWithAI();//EnableAIPlayers(); USE THIS COMMENTED OUT CODE AFTER FULL IMPLEMENTATION
    }

    private void NetworkedSceneManager_OnSceneEvent(SceneEvent sceneEvent) {
        // Both client and server receive these notifications
        switch (sceneEvent.SceneEventType) {
            // Handle server to client Load Notifications
            case SceneEventType.Load: {
                    // This event provides you with the associated AsyncOperation
                    // AsyncOperation.progress can be used to determine scene loading progression
                    AsyncOperation asyncOperation = sceneEvent.AsyncOperation;
                    // Since the server "initiates" the event we can simply just check if we are the server here
                    if (NetworkManager.Singleton.IsServer) {
                        // Handle server side load event related tasks here
                    } else {
                        // Handle client side load event related tasks here
                    }
                    break;
                }
            // Handle server to client unload notifications
            case SceneEventType.Unload: {
                    // You can use the same pattern above under SceneEventType.Load here
                    break;
                }
            // Handle client to server LoadComplete notifications
            case SceneEventType.LoadComplete: {
                    // This will let you know when a load is completed

                    // Server Side: receives thisn'tification for both itself and all clients
                    if (NetworkManager.Singleton.IsServer) {
                        if (sceneEvent.ClientId == NetworkManager.ServerClientId) {
                            // Handle server side LoadComplete related tasks here

                            TankNetworkPlayer tankPlayer = GetTankPlayer(sceneEvent.ClientId);

                            if (tankPlayer == null) {
                                Debug.Log("Player finished loading that wasn't already registered as a TankNetworkPlayer!");
                                tankPlayer = RegisterTankNetworkPlayer(sceneEvent.ClientId);
                            }

                            TankPlayerLoadComplete?.Invoke(tankPlayer);

                            /*
                            // On Scene load completion, attempt to retrieve map data and create match logic for the map
                            MapConfig.MapConfigurationData mapConfig;
                            bool gotConfig = TankMapLoader.GetCurrentMapConfig(out mapConfig);

                            // Clear previous match logic if it exists
                            // Doing this here AND in OnPostMatchEnded() because it is possible that a new map
                            // will be loaded prior to that event ever triggering.
                            MatchLogic?.Dispose();
                            MatchLogic = null;

                            AMatchLogic logic = null;
                            if (gotConfig) {
                                logic = TankMapLoader.GetMatchLogic(mapConfig);
                            }

                            if (logic != null) {
                                Debug.Log("MatchLogic loaded successfully.");
                                MatchLogic = logic;

                                
                                //TankNetworkPlayer tankPlayer = RegisterTankNetworkPlayer(sceneEvent.ClientId);
                            }
                            */


                            /*
                            if (NetworkManager.Singleton.IsLobbyHost) {
                                if (MatchLogic != null) {
                                    Debug.Log("ASSIGNING PLAYER TO TEAM WITHOUT THEIR CONSENT");
                                    TankNetworkPlayer tankPlayer = GetTankPlayer(sceneEvent.ClientId);
                                    if (tankPlayer != null) {
                                        Debug.Log("GOT TANK PLAYER");
                                        MatchLogic.TeamManager.AutoAssignPlayerToTeam(tankPlayer); // EXTRACT THIS LOGIC
                                        tankPlayer.SetPlayerPrefab(MatchLogic.DefaultPlayerPrefab); // EXTRACT THIS LOGIC
                                    }
                                }
                            }
                            */
                        } else {
                            // Handle client LoadComplete **server-side** notifications here
                            Debug.Log("client loaded in");

                            TankNetworkPlayer tankPlayer = GetTankPlayer(sceneEvent.ClientId);

                            if (tankPlayer == null) {
                                Debug.Log("Player finished loading that wasn't already registered as a TankNetworkPlayer!");
                                tankPlayer = RegisterTankNetworkPlayer(sceneEvent.ClientId);
                            }

                            TankPlayerLoadComplete?.Invoke(tankPlayer);

                            //TankNetworkPlayer tankPlayer = RegisterTankNetworkPlayer(sceneEvent.ClientId);

                            /*
                            if (MatchLogic != null) {
                                Debug.Log("ASSIGNING PLAYER TO TEAM WITHOUT THEIR CONSENT");

                                TankNetworkPlayer tankPlayer = GetTankPlayer(sceneEvent.ClientId);
                                if (tankPlayer != null) {
                                    Debug.Log("GOT TANK PLAYER");
                                    MatchLogic.TeamManager.AutoAssignPlayerToTeam(tankPlayer);
                                    tankPlayer.SetPlayerPrefab(MatchLogic.DefaultPlayerPrefab);
                                }
                            }
                            */
                        }
                    } else { // Clients generate thisn'tification locally
                        // Handle client side LoadComplete related tasks here
                    }

                    // So you can use sceneEvent.ClientId to also track when clients are finished loading a scene
                    break;
                }
            // Handle Client to Server Unload Complete Notification(s)
            case SceneEventType.UnloadComplete: {
                    // This will let you know when an unload is completed
                    // You can follow the same pattern above as SceneEventType.LoadComplete here

                    // Server Side: receives thisn'tification for both itself and all clients
                    // Client Side: receives thisn'tification for itself

                    // So you can use sceneEvent.ClientId to also track when clients are finished unloading a scene
                    break;
                }
            // Handle Server to Client Load Complete (all clients finished loading notification)
            case SceneEventType.LoadEventCompleted: {
                    // This will let you know when all clients have finished loading a scene
                    // Received on both server and clients
                    foreach (ulong clientId in sceneEvent.ClientsThatCompleted) {
                        // Example of parsing through the clients that completed list
                        if (NetworkManager.Singleton.IsServer) {
                            MatchLogic?.StartMatch();
                            // Handle any server-side tasks here
                        } else {
                            // Handle any client-side tasks here
                        }
                    }
                    break;
                }
            // Handle Server to Client unload Complete (all clients finished unloading notification)
            case SceneEventType.UnloadEventCompleted: {
                    // This will let you know when all clients have finished unloading a scene
                    // Received on both server and clients
                    foreach (ulong clientId in sceneEvent.ClientsThatCompleted) {
                        // Example of parsing through the clients that completed list
                        if (NetworkManager.Singleton.IsServer) {
                            // Handle any server-side tasks here
                        } else {
                            // Handle any client-side tasks here
                        }
                    }
                    break;
                }
        }
    }

    private async Task<bool> SignInUnityServicesIfNotSignedIn() {
        bool signedIn = AuthenticationService.Instance.IsSignedIn;
        if (!signedIn) {
            signedIn = await SignInToUnityServicesAnonymously();
        }

        return signedIn;
    }

    private async Task<bool> SignInToUnityServicesAnonymously() {
        //if (AuthenticationService.Instance.IsSignedIn) return;
        bool successful = false;
        try {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            successful = true;
        } catch (AuthenticationException e) {
            Debug.Log(e);
        } catch (RequestFailedException e) {
            Debug.Log(e);
        }

        return successful;
    }
}