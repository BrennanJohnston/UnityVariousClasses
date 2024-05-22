using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Collections;

public abstract class ATankPlayer : NetworkBehaviour {
    private static int _IDIterator = 0;

    private NetworkVariable<NetworkString64Bytes> _username = new NetworkVariable<NetworkString64Bytes>("Tank Player");
    private NetworkVariable<int> _tankPlayerID = new NetworkVariable<int>(-1);

    // Score data
    private NetworkVariable<short> _score = new NetworkVariable<short>(0);
    private NetworkVariable<short> _kills = new NetworkVariable<short>(0);
    private NetworkVariable<short> _deaths = new NetworkVariable<short>(0);
    private NetworkVariable<int> _assignedTeamID = new NetworkVariable<int>(TankTeam.NO_TEAM);

    /// <summary>
    /// Used to export data regarding an ATankPlayer's score data.
    /// </summary>
    public struct ScoreData {
        private short score;
        private short kills;
        private short deaths;
        
        public short Score { get { return score; } }
        public short Kills { get { return kills; } }
        public short Deaths { get { return deaths; } }

        internal ScoreData(short _score, short _kills, short _deaths) {
            score = _score;
            kills = _kills;
            deaths = _deaths;
        }
    }

    public NetworkString64Bytes Username {
        get { return _username.Value; }
        set {
            if (!IsServer && !IsHost) return;
            if (!string.IsNullOrWhiteSpace(value.ToString())) {
                _username.Value = value;
                UsernameChanged?.Invoke(this);
            }
        }
    }

    public int TankPlayerID { get { return _tankPlayerID.Value; }
        private set { if (!IsServer && !IsHost) return; _tankPlayerID.Value = value; } }

    public TankTeam AssignedTankTeam { get; private set; } = null;

    public int AssignedTankTeamID { get { return _assignedTeamID.Value; } }

    public GameObject playerGameObjectInstance { get; private set; }

    public GameObject playerGameObjectPrefab { get; private set; }

    public Action ScoreDataChanged;

    public static Action<ATankPlayer, DeathInfo> TankPlayerDied;
    public static Action<ATankPlayer> UsernameChanged;
    public static Action<ATankPlayer> TankPlayerCreated;
    public static Action<ATankPlayer> TankPlayerDestroyed;
    /// <summary>
    /// Player whose assignment changed, old team ID, new team ID.
    /// </summary>
    public static Action<ATankPlayer, int, int> AssignedTeamIDChanged;

    /// <summary>
    /// Emitted when an ATankPlayer instance wants to spawn the player prefab. Passes the relevant TankNetworkPlayer instance and desired Transform spawn location.
    /// </summary>
    public static Action<ATankPlayer, Transform> TankPlayerWantsToSpawn;

    protected virtual void Awake() {
        NetworkSpawnManager.SpawnedTankPlayer += OnTankPlayerSpawned;
    }

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();

        if (IsServer || IsHost) {
            TankPlayerID = _IDIterator;
            _IDIterator++;
        }

        _score.OnValueChanged += OnScoreValueChanged;
        _kills.OnValueChanged += OnKillsValueChanged;
        _deaths.OnValueChanged += OnDeathsValueChanged;
        _assignedTeamID.OnValueChanged += OnAssignedTeamIDChanged;

        UpdateAssignedTankTeam(TankTeam.NO_TEAM);
        TankPlayerCreated?.Invoke(this);
    }

    public override void OnNetworkDespawn() {
        base.OnNetworkDespawn();

        TankPlayerDestroyed?.Invoke(this);
    }

    public override void OnDestroy() {
        base.OnDestroy();

        NetworkSpawnManager.SpawnedTankPlayer -= OnTankPlayerSpawned;
        _score.OnValueChanged -= OnScoreValueChanged;
        _kills.OnValueChanged -= OnKillsValueChanged;
        _deaths.OnValueChanged -= OnDeathsValueChanged;
    }

    /// <summary>
    /// Called when an ATankPlayer's prefab is spawned.
    /// </summary>
    /// <param name="player"></param>
    /// <param name="spawnedObject"></param>
    private void OnTankPlayerSpawned(ATankPlayer player, NetworkObject spawnedObject) {
        if (!ReferenceEquals(this, player)) return;

        playerGameObjectInstance = spawnedObject.gameObject;
        if(playerGameObjectInstance.TryGetComponent(out PlayerNameAssignment nameAssignmentComponent)) {
            Debug.Log("Successfully received PlayerNameAssignment component");
            nameAssignmentComponent.SetPlayerName(Username.ToString());
        }

        SubscribeToOnDied();
    }

    /*
    // Only called on the server
    private void OnPlayerJoinedTeamHandler(ATankPlayer player, TankTeam team) {
        if (!ReferenceEquals(this, player)) return;

        AssignedTankTeam = team;
        if (AssignedTankTeam == null) _assignedTeamID.Value = NO_TEAM;
        else _assignedTeamID.Value = AssignedTankTeam.TeamID;

        if (playerGameObjectInstance == null) return;

        TankTeamAssignment tankTeamAssignmentComponent = null;
        if (playerGameObjectInstance?.TryGetComponent(out tankTeamAssignmentComponent) == false) return;

        tankTeamAssignmentComponent?.AssignTankTeam(AssignedTankTeam);
    }

    private void OnPlayerLeftTeamHandler(ATankPlayer player, TankTeam team) {
        if (!ReferenceEquals(this, player)) return;

        AssignedTankTeam = null;

        if (playerGameObjectInstance == null) return;

        TankTeamAssignment tankTeamAssignmentComponent = null;
        if (playerGameObjectInstance?.TryGetComponent(out tankTeamAssignmentComponent) == false) return;

        tankTeamAssignmentComponent?.RemoveTankTeam();
    }
    */

    /// <summary>
    /// <para>Functional on: Server</para>
    /// Emits ATankPlayer.TankPlayerWantsToSpawn event that this TankNetworkPlayer wants to spawn at provided Transform.
    /// </summary>
    /// <param name="location"></param>
    /// <returns>True if request was emitted, false otherwise.</returns>
    public bool SpawnPlayer(Transform location) {
        if (playerGameObjectPrefab == null || (!IsServer && !IsHost)) return false;

        TankPlayerWantsToSpawn?.Invoke(this, location);
        return true;

        /*
        bool success = false;
        NetworkObject spawnedPlayer = TankRelay.Singleton.SpawnNetworkObject(playerGameObjectPrefab, location.position, location.rotation, ClientID);
        if (spawnedPlayer == null) return false;

        playerGameObjectInstance = spawnedPlayer.gameObject;
        if (playerGameObjectInstance?.TryGetComponent(out TankTeamAssignment tankTeamAssignmentComponent) == true) {
            if (AssignedTankTeam == null) tankTeamAssignmentComponent.RemoveTankTeam();
            else tankTeamAssignmentComponent.AssignTankTeam(AssignedTankTeam);
        }
        SubscribeToOnDied();
        success = true;

        return success;
        */
    }

    /// <summary>
    /// <para>Functional on: Server</para>
    /// Emits ATankPlayer.TankPlayerWantsToSpawn event that this TankNetworkPlayer wants to spawn at provided SpawnPoint.
    /// </summary>
    /// <param name="spawnPoint"></param>
    /// <returns>True if request was emitted, false otherwise.</returns>
    public bool SpawnPlayer(SpawnPoint spawnPoint) {
        return SpawnPlayer(spawnPoint.transform);
    }

    /// <summary>
    /// <para>Functional on: Server</para>
    /// Network destroys this players GameObject (Tank).
    /// </summary>
    public void KillPlayerGameObject() {
        if (playerGameObjectInstance == null || (!IsServer && !IsHost)) return;

        if (playerGameObjectInstance.TryGetComponent(out ANetworkDie dieComponent)) {
            //destruction called on the server should destroy for all clients
            dieComponent.NetworkDie(new DeathInfo(this));
            return;
        }

        //destruction called on the server should destroy for all clients
        if (playerGameObjectInstance.TryGetComponent(out NetworkObject networkObject)) {
            networkObject.Despawn();
            return;
        }

        // Fallback to destroy the player if they somehow do not have a networkObject component and do not have an ADie component.
        Debug.Log("ATankPlayer.KillPlayerGameObject(): Player object did not have an ADie component and/or networkObject component, fallback destroy was called.");
        Destroy(playerGameObjectInstance);
        return;
    }

    /// <summary>
    /// Set the prefab this player will prefer to spawn with. Only works if the provided prefab has a NetworkObject component at its root.
    /// </summary>
    /// <param name="prefab"></param>
    /// <returns>True if assignment is successful, false otherwise.</returns>
    public bool SetPlayerPrefab(GameObject prefab) {
        if (!IsServer && !IsHost) return false;

        bool success = false;
        if (prefab.GetComponent<NetworkObject>() == null) return false;

        playerGameObjectPrefab = prefab;
        Debug.Log("Set player prefab successfully.");
        success = true;

        return success;
    }

    /// <summary>
    /// <para>Functional on: Server</para>
    /// Changes the AssignedTeamID of this ATankPlayer, which Invokes ATankPlayer.AssignedTeamIDChanged on all clients.
    /// Use RemoveTeamAssignment() to remove from current team.
    /// </summary>
    /// <param name="team"></param>
    public void SetTeamAssignment(TankTeam team) {
        if ((!IsServer && !IsHost) || team == null || _assignedTeamID == null) return;

        _assignedTeamID.Value = team.TeamID;
    }

    /// <summary>
    /// <para>Functional on: Server</para>
    /// Changes the AssignedTeamID of this ATankPlayer to ATankPlayer.NO_TEAM, which Invokes ATankPlayer.AssignedTeamIDChanged on all clients.
    /// </summary>
    public void RemoveTeamAssignment() {
        if (!IsServer && !IsHost) return;

        _assignedTeamID.Value = TankTeam.NO_TEAM;
    }

    /// <summary>
    /// Event trigger handler for NetworkVariable value change.
    /// </summary>
    /// <param name="oldValue"></param>
    /// <param name="newValue"></param>
    private void OnAssignedTeamIDChanged(int oldValue, int newValue) {
        UpdateAssignedTankTeam(oldValue);
    }

    public void UpdateAssignedTankTeam(int oldTeamID) {
        AssignedTankTeam = TankTeamManager.Singleton?.GetTeamByTeamID(_assignedTeamID.Value);
        Debug.Log("PLAYER ASSIGNED TEAM: " + oldTeamID + " : " + _assignedTeamID.Value);
        Debug.Log("PLAYER TEAM: " + AssignedTankTeam);
        if (_assignedTeamID.Value == TankTeam.NO_TEAM) AssignedTankTeam = null;
        AssignedTeamIDChanged?.Invoke(this, oldTeamID, _assignedTeamID.Value);
    }

    public ScoreData GetScoreData() {
        return new ScoreData(_score.Value, _kills.Value, _deaths.Value);
    }

    /// <summary>
    /// Reset all score data (score, kills, deaths) for this ATankPlayer.
    /// </summary>
    public void ResetScoreData() {
        _score.Value = 0;
        _kills.Value = 0;
        _deaths.Value = 0;
    }

    /// <summary>
    /// Add amount to score.
    /// </summary>
    /// <param name="amount"></param>
    public void IterateScore(short amount) {
        _score.Value += amount;
    }

    /// <summary>
    /// Add amount to kills.
    /// </summary>
    /// <param name="amount"></param>
    public void IterateKills(short amount) {
        _kills.Value += amount;
    }

    /// <summary>
    /// Add amount to deaths.
    /// </summary>
    /// <param name="amount"></param>
    public void IterateDeaths(short amount) {
        _deaths.Value += amount;
    }

    private void OnScoreValueChanged(short oldScore, short newScore) {
        ScoreDataChanged?.Invoke();
    }

    private void OnKillsValueChanged(short oldKills, short newKills) {
        ScoreDataChanged?.Invoke();
    }

    private void OnDeathsValueChanged(short oldDeaths, short newDeaths) {
        ScoreDataChanged?.Invoke();
    }

    /// <summary>
    /// Triggered when the player gameObject ADie emits OnDied event.  Subscribed to in SubscribeToOnDied.
    /// </summary>
    private void OnPlayerGameObjectDied(DeathInfo deathInfo) {
        //UnsubscribeFromOnDied(playerGameObjectInstance);
        Debug.Log("Player Died, info below...");
        Debug.Log(deathInfo);
        UnsubscribeFromOnDied();
        playerGameObjectInstance = null;
        TankPlayerDied?.Invoke(this, deathInfo);
    }

    /// <summary>
    /// Subscribe to the ANetworkDie.Died event so we are alerted when this player dies.
    /// </summary>
    private void SubscribeToOnDied() {
        if (playerGameObjectInstance == null) return;
        if (playerGameObjectInstance.TryGetComponent(out ANetworkDie dieComponent)) {
            dieComponent.Died += OnPlayerGameObjectDied;
        }
    }

    /// <summary>
    /// Unsubscribe from the ANetworkDie.Died event. Called in OnPlayerGameObjectDied
    /// </summary>
    private void UnsubscribeFromOnDied() {
        if (playerGameObjectInstance == null) return;
        if(playerGameObjectInstance.TryGetComponent(out ANetworkDie dieComponent)) {
            dieComponent.Died -= OnPlayerGameObjectDied;
        }
    }

    public override string ToString() {
        string str = $"ATankPlayer( Username: {Username} )";
        return str;
    }
}