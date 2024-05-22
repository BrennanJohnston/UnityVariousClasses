using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using System.Linq;
using Unity.Netcode;
using Unity.Collections;

public abstract class AMatchLogic : NetworkBehaviour {
    [SerializeField] private GameObject _tankTeamManagerPrefab;
    [SerializeField] private GameObject _scoreboardUIPrefab;

    public static AMatchLogic Singleton { get; private set; } = null;

    // network variables
    private NetworkVariable<int> _scoreLimit = new NetworkVariable<int>();
    private NetworkVariable<float> _timeLimit = new NetworkVariable<float>();
    private NetworkVariable<float> _postGameTimeLimit = new NetworkVariable<float>(5f);
    private NetworkVariable<float> _timer = new NetworkVariable<float>();
    private NetworkVariable<float> _spawnTime = new NetworkVariable<float>();
    private NetworkVariable<MatchState> _matchState = new NetworkVariable<MatchState>();
    private NetworkVariable<bool> _matchComplete = new NetworkVariable<bool>();

    private bool _spawnAIEnabled = false;

    public bool IsInitialized { get; private set; } = false;
    public int ScoreLimit { 
        get { return _scoreLimit.Value; } 
        set { if (!IsServer) return; _scoreLimit.Value = value; } }
    public float TimeLimit {
        get { return _timeLimit.Value; }
        set { if (!IsServer) return; _timeLimit.Value = value; }
    }
    public float PostGameTimeLimit {
        get { return _postGameTimeLimit.Value; }
        set { if (!IsServer) return; _postGameTimeLimit.Value = value; }
    }
    public float Timer {
        get { return _timer.Value; }
        set { if (!IsServer) return; _timer.Value = value; }
    }
    public float SpawnTime {
        get { return _spawnTime.Value; }
        set { if (!IsServer) return; _spawnTime.Value = value; }
    }
    public MatchState CurrentState {
        get { return _matchState.Value; }
        set { if (!IsServer) return; _matchState.Value = value; }
    }
    public bool MatchComplete {
        get { return _matchComplete.Value; }
        set { if (!IsServer) return; _matchComplete.Value = value; }
    }

    public bool SpawnAIEnabled {
        get { return _spawnAIEnabled; }
        set { _spawnAIEnabled = value; }
    }

    // Team manager only exists on the Server
    public TankTeamManager TeamManager         { get; private set; }
    public bool            FriendlyFireEnabled { get; private set; }
    public GameObject      DefaultPlayerPrefab { get; private set; }

    /// <summary>
    /// Invoked when an AMatchLogic is constructed and initialized successfully.
    /// </summary>
    public static Action<AMatchLogic> MatchLogicLoaded;
    public static Action MatchStarted;
    public static Action MatchEnded;
    public static Action PostMatchEnded;

    protected delegate void StateUpdateDelegate(float deltaTime);
    protected StateUpdateDelegate StateUpdate;

    private Dictionary<int, float> playerRespawnTimerDictionary = new Dictionary<int, float>();

    private ScoreboardUI scoreboardUI;

    public enum MatchState {
        WarmUp,
        InProgress,
        Ended
    }

    void Awake() {
        if(Singleton != null) {
            Singleton.Dispose();
            //DestroyProp(Singleton.gameObject);
            Singleton = null;
        }

        Singleton = this;

        TankInputManager.ShowScoreboardInputChanged += OnShowScoreboardInputChanged;
    }

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();

        if(_scoreboardUIPrefab != null) {
            GameObject scoreboardGO = Instantiate(_scoreboardUIPrefab);
            scoreboardUI = scoreboardGO.GetComponent<ScoreboardUI>();
            Debug.Log("scoreboardUI null?: " + (scoreboardUI == null));
            if(scoreboardUI != null) {
                Debug.Log("hiding scoreboard UI");
                scoreboardUI.HideScoreboard();
            }
        }
    }

    void Update() {
        if (!IsServer && !IsHost) return;

        if(StateUpdate == null) {
            Debug.Log("StateUpdate is null, cannot update AMatchLogic");
            return;
        }

        StateUpdate(Time.deltaTime);
    }

    public override void OnDestroy() {
        base.OnDestroy();

        TankInputManager.ShowScoreboardInputChanged -= OnShowScoreboardInputChanged;
    }

    /// <summary>
    /// Call the base if you override this! Initializes this AMatchLogic with the provided mapConfig information.
    /// Only callable once. If you need new MatchLogic, put a new MatchLogic Component into the scene and Initialize it.
    /// </summary>
    /// <param name="mapConfig"></param>
    //public AMatchLogic(MapConfig.MapConfigurationData mapConfig) {
    public virtual void Initialize(MapConfig.MapConfigurationData mapConfig) {
        if (IsInitialized || _tankTeamManagerPrefab == null) {
            Debug.Log("AMatchLogic.Initialize() was called when the MatchLogic has already been Initialized!");
            return;
        }

        // Reset all player data
        List<ATankPlayer> players = TankRelay.Singleton.GetTankPlayers();
        for (int i = 0; i < players.Count; i++) {
            ATankPlayer player = players[i];
            if (player == null) continue;
            player.ResetScoreData();
            player.RemoveTeamAssignment();
        }

        SubscribeToEvents();

        ScoreLimit = mapConfig.ScoreLimit;
        TimeLimit = mapConfig.TimeLimit;
        SpawnTime = mapConfig.SpawnTime;
        CurrentState = MatchState.WarmUp;
        StateUpdate = WarmUpUpdate;
        GameObject tankTeamManagerGO = Instantiate(_tankTeamManagerPrefab);
        if(tankTeamManagerGO.TryGetComponent(out NetworkObject tankTeamManagerNetworkObject)) {
            tankTeamManagerNetworkObject.Spawn(destroyWithScene: false); // If this doesn't spawn, something is seriously amiss
        }
        TankTeamManager teamManagerComponent = tankTeamManagerGO.GetComponent<TankTeamManager>();
        TeamManager = teamManagerComponent;
        TeamManager.CreateTeams(mapConfig.Teams);
        FriendlyFireEnabled = mapConfig.FriendlyFireEnabled;
        DefaultPlayerPrefab = mapConfig.DefaultPlayerPrefab;
        MatchComplete = false;
        SpawnAIEnabled = mapConfig.SpawnAI;
        
        // Create spawnpoints based on mapConfig data
        for(int i = 0; i < mapConfig.SpawnPoints.Count; i++) {
            SpawnPoint currentSpawnPoint = mapConfig.SpawnPoints[i];
            Debug.Log("Detected spawnpoint with TeamID " + currentSpawnPoint.TeamID);
            TankTeam associatedTeam = TeamManager.GetTeamByTeamID(currentSpawnPoint.TeamID);
            if (associatedTeam != null) {
                Debug.Log("Found associated team");
                associatedTeam.AddSpawnPoint(currentSpawnPoint);
            } else Debug.Log("Did not find associated team for spawnpoint");
        }

        // Find and iterate all in-map objects with DefaultTeamAssignmentID and assign them to teams based on their DefaultTeamID's
        GameObject[] teamAssignableObjects = GameObject.FindGameObjectsWithTag("DefaultTeamAssignable");
        for(int i = 0; i < teamAssignableObjects.Length; i++) {
            TankTeamAssignment currentAssignable = teamAssignableObjects[i].GetComponent<TankTeamAssignment>();
            if (currentAssignable == null) continue;

            TankTeam defaultTeam = TeamManager.GetTeamByTeamID(currentAssignable.DefaultTeamAssignmentID);
            if (defaultTeam == null) continue;

            currentAssignable.AssignTankTeam(defaultTeam);
        }
        
        //SubscribeToEvents();

        IsInitialized = true;
        Debug.Log("AMatchLogic.Initialize() succeeded");
        MatchLogicLoaded?.Invoke(this);
    }

    /// <summary>
    /// Change the gamestate from WarmUp to InProgress.  Override this function (and call the base!) to apply logic for match beginning.
    /// </summary>
    public virtual void StartMatch() {
        Debug.Log("AMatchLogic.StartMatch() called");
        Timer = TimeLimit;
        CurrentState = MatchState.InProgress;
        StateUpdate = InProgressUpdate;

        TriggerMatchStarted();
    }

    /// <summary>
    /// End the match and determine a winner.  Override this function (and call the base!) to apply logic to determining a winner for the match.
    /// </summary>
    public virtual void EndMatch() {
        if (CurrentState == MatchState.Ended) return;
        Debug.Log("AMatchLogic.EndMatch() called");
        CurrentState = MatchState.Ended;
        StateUpdate = EndedUpdate;
        Timer = PostGameTimeLimit;

        TriggerMatchEnded();
    }

    protected virtual void WarmUpUpdate(float deltaTime) {
        UpdateRespawnTimers(deltaTime);
    }

    protected virtual void InProgressUpdate(float deltaTime) {
        UpdateRespawnTimers(deltaTime);
        Timer -= deltaTime;
        if(Timer <= 0f) {
            EndMatch();
        }
    }

    protected virtual void EndedUpdate(float deltaTime) {
        Timer -= deltaTime;
        if(Timer <= 0f) {
            if (!MatchComplete) {
                TriggerPostMatchEnded();
                //Dispose();
                MatchComplete = true;
            }
        }
    }

    private void UpdateRespawnTimers(float deltaTime) {
        // THIS LINE IS KIND OF SLOW, FIGURE OUT SOME OTHER WAY TO ITERATE TIMERS
        List<int> dictionaryKeys = playerRespawnTimerDictionary.Keys.ToList<int>();

        for (int i = 0; i < dictionaryKeys.Count; i++) {
            ATankPlayer tankPlayer = TankRelay.Singleton.GetTankPlayer(dictionaryKeys[i]);

            if (tankPlayer == null) continue;

            float updatedTime = playerRespawnTimerDictionary[tankPlayer.TankPlayerID] - deltaTime;

            if (updatedTime > 0f) {
                playerRespawnTimerDictionary[tankPlayer.TankPlayerID] = updatedTime;
                continue;
            }

            UnregisterDeadPlayer(tankPlayer);
            //TankTeam networkPlayerTankTeam = TeamManager.GetTeamByClientID(tankPlayer.ClientID);

            if (tankPlayer.AssignedTankTeam == null) continue;

            SpawnPoint randomSpawnPoint = tankPlayer.AssignedTankTeam.GetRandomSpawnPoint();

            if (randomSpawnPoint == null) continue;

            tankPlayer.SpawnPlayer(randomSpawnPoint);
        }
    }

    // Call the base if you override this function.
    protected virtual void SubscribeToEvents() {
        Debug.Log("AMatchLogic.SubscribeToEvents() called");
        TankNetworkPlayer.TankPlayerDied += OnTankPlayerDied;
        //TankRelay.TankPlayerRegistered += OnTankPlayerRegisteredOnTankRelay;
        TankRelay.TankPlayerLoadComplete += OnTankPlayerLoadComplete;
        TankRelay.TankPlayerRegistered += OnTankPlayerRegistered;
        TankTeam.PlayerJoinedTeam += OnPlayerJoinedTeam;
    }

    // Call the base if you override this function.
    protected virtual void UnsubscribeFromEvents() {
        Debug.Log("AMatchLogic.UnsubscribeFromEvents() called");
        TankNetworkPlayer.TankPlayerDied -= OnTankPlayerDied;
        //TankRelay.TankPlayerRegistered -= OnTankPlayerRegisteredOnTankRelay;
        TankRelay.TankPlayerLoadComplete -= OnTankPlayerLoadComplete;
        TankRelay.TankPlayerRegistered -= OnTankPlayerRegistered;
        TankTeam.PlayerJoinedTeam -= OnPlayerJoinedTeam;
    }

    /// <summary>
    /// Disposes of this AMatchLogic instance, and Disposes of associated TankTeamManager.
    /// </summary>
    public void Dispose() {
        if (!IsServer && !IsHost) return;
        Debug.Log("AMatchLogic.Dispose() called");
        //EndMatch();
        UnsubscribeFromEvents();
        TeamManager.Dispose();
        gameObject.GetComponent<NetworkObject>()?.Despawn();
    }

    /// <summary>
    /// Event handler for when a TankNetworkPlayer finishes loading
    /// </summary>
    /// <param name="tankPlayer"></param>
    private void OnTankPlayerLoadComplete(TankNetworkPlayer tankPlayer) {
        Debug.Log("OnTankPlayerLoadComplete() was called");
        tankPlayer.SetPlayerPrefab(DefaultPlayerPrefab);
        TeamManager.AutoAssignPlayerToTeam(tankPlayer);
    }

    /// <summary>
    /// Event handler for any ATankPlayer (including AI) being registered in the Relay.
    /// If ATankPlayer is a TankAIPlayer, autoassigns player to team.
    /// </summary>
    /// <param name="tankPlayer"></param>
    private void OnTankPlayerRegistered(ATankPlayer tankPlayer) {
        if (tankPlayer.AssignedTankTeam != null) return;
        //tankPlayer.SetPlayerPrefab(DefaultPlayerPrefab);
        Debug.Log("OnTankPlayerRegistered()");
        //TeamManager.AutoAssignPlayerToTeam(tankPlayer);
    }

    protected virtual void OnTankPlayerDied(ATankPlayer tankPlayer, DeathInfo deathInfo) {
        deathInfo.WeaponUsedInfo?.TankPlayerOwner.IterateKills(1);
        tankPlayer.IterateDeaths(1);
        RegisterDeadPlayer(tankPlayer);
    }

    private void OnPlayerJoinedTeam(ATankPlayer tankPlayer, TankTeam tankTeam) {
        Debug.Log("OnPlayerJoinedTeam() was called.");
        RegisterDeadPlayer(tankPlayer);
    }

    private void OnShowScoreboardInputChanged(bool show) {
        if (scoreboardUI == null) return;
        if (show) {
            scoreboardUI.ShowScoreboard();
        } else {
            scoreboardUI.HideScoreboard();
        }
    }

    /// <summary>
    /// Register an ATankPlayer as dead.
    /// </summary>
    /// <param name="tankPlayer"></param>
    private void RegisterDeadPlayer(ATankPlayer tankPlayer) {
        if (tankPlayer == null || playerRespawnTimerDictionary.ContainsKey(tankPlayer.TankPlayerID)) return;
        Debug.Log("Registered ATankPlayer as Dead");
        playerRespawnTimerDictionary.Add(tankPlayer.TankPlayerID, SpawnTime);
    }

    /// <summary>
    /// Unregister a TankNetworkPlayer as dead (they're alive).
    /// </summary>
    /// <param name="tankPlayer"></param>
    private void UnregisterDeadPlayer(ATankPlayer tankPlayer) {
        Debug.Log("Unregistering Dead Player");
        playerRespawnTimerDictionary.Remove(tankPlayer.TankPlayerID);
    }

    private void TriggerMatchStarted() {
        Debug.Log("AMatchLogic.TriggerMatchStarted() called");
        MatchStarted?.Invoke();
    }

    private void TriggerMatchEnded() {
        Debug.Log("AMatchLogic.TriggerMatchEnded() called");
        MatchEnded?.Invoke();
    }

    private void TriggerPostMatchEnded() {
        Debug.Log("AMatchLogic.TriggerPostMatchEnded() called");
        PostMatchEnded?.Invoke();
    }
}