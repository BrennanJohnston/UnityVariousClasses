using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Unity.Netcode;
using UnityEngine;

public class TankTeamManager : NetworkBehaviour {
    [SerializeField] private string _tankTeamGameObjectTag;
    [SerializeField] private GameObject _tankTeamPrefab;

    public static TankTeamManager Singleton { get; private set; } = null;

    public int TeamCount { get { return Teams.Count; } }

    /// <summary>
    /// <para>Functional on: Server, Client</para>
    /// Stores TankTeam.TeamID keys and their corresponding TankTeam.
    /// </summary>
    private Dictionary<int, TankTeam> Teams = new Dictionary<int, TankTeam>();

    /// <summary>
    /// <para>Functional on: Server, Client</para>
    /// Stores TankTeam keys and retrieves a key (TankTeam.TeamID) for Teams Dictionary associated with that TankTeam.
    /// </summary>
    private Dictionary<TankTeam, int> TankTeamToTeamID = new Dictionary<TankTeam, int>();

    /// <summary>
    /// <para>Functional on: Server</para>
    /// Stores TankPlayerID keys and retrieves the TankTeam associated with that TankPlayerID.  Returns null if player is not assigned to a team.
    /// </summary>
    private Dictionary<int, TankTeam> TankPlayerIDToTankTeam = new Dictionary<int, TankTeam>();

    void Awake() {
        //if(!IsServer) LoadTeamData();

        if(Singleton != null) {
            //Singleton.GetComponent<NetworkObject>()?.Despawn();
            Singleton = null;
        }

        Singleton = this;
        TankTeam.TeamInitialized += RegisterTeam; // Called on Server during spinup
        TankTeam.TeamNetworkSpawned += RegisterTeam; // Called on clients joining after server is already running
        TankTeam.TeamNetworkDespawned += UnregisterTeam;
        NetworkSpawnManager.SpawnedTankPlayer += OnTankPlayerSpawned;
    }

    public override void OnDestroy() {
        TankTeam.TeamInitialized -= RegisterTeam; // Called on Server during spinup
        TankTeam.TeamNetworkSpawned -= RegisterTeam; // Called on clients joining after server is already running
        TankTeam.TeamNetworkDespawned -= UnregisterTeam;
        NetworkSpawnManager.SpawnedTankPlayer -= OnTankPlayerSpawned;
    }

    [Rpc(SendTo.Server)]
    public void RequestJoinTankTeamRpc(int teamID, RpcParams rpcParams = default) {
        TankTeam team = GetTeamByTeamID(teamID);
        if (team == null) {
            Debug.Log("teamID was invalid");
            return;
        }

        ATankPlayer abstractPlayer = TankRelay.Singleton.GetTankPlayer(rpcParams.Receive.SenderClientId);
        if (abstractPlayer == null) {
            Debug.Log("SenderClientId was invalid");
            return;
        }

        bool teamWasAssigned = AssignPlayerToTeam(abstractPlayer, team);
        if (teamWasAssigned) {
            Debug.Log("Player was successfully assigned to requested TankTeam");
        } else {
            Debug.Log("Player was NOT successfully assigned to requested TankTeam");
        }
    }

    [Rpc(SendTo.Server)]
    public void RequestAutoAssignTankTeamRpc(RpcParams rpcParams = default) {
        ATankPlayer abstractPlayer = TankRelay.Singleton.GetTankPlayer(rpcParams.Receive.SenderClientId);
        if (abstractPlayer == null) {
            Debug.Log("tankPlayerID was invalid");
            return;
        }

        bool teamWasAssigned = AutoAssignPlayerToTeam(abstractPlayer);

    }

    /// <summary>
    /// Handler for when a TankPlayer's prefab is spawned into the game.
    /// </summary>
    /// <param name="player"></param>
    /// <param name="spawnedObject"></param>
    private void OnTankPlayerSpawned(ATankPlayer player, NetworkObject spawnedObject) {
        if(spawnedObject.TryGetComponent(out TankTeamAssignment tankTeamAssignment)) {
            if (player.AssignedTankTeam == null) tankTeamAssignment.RemoveTankTeam();
            else tankTeamAssignment.AssignTankTeam(player.AssignedTankTeam);
        }
    }

    /// <summary>
    /// <para>Functional on: Server</para>
    /// Automatically assign the provided player to a team based on autobalancing. Returns true if assignment was successful.
    /// </summary>
    /// <param name="player"></param>
    public bool AutoAssignPlayerToTeam(ATankPlayer tankPlayer) {
        if ((!IsServer && !IsHost) || tankPlayer == null) return false;

        TankTeam tankTeam = GetTeamWithLowestPlayerCount();
        Debug.Log("AutoAssignPlayerToTeam()");
        if (tankTeam == null) {
            Debug.Log("Attempted to assign player to team but failed");
            return false;
        }

        return AssignPlayerToTeam(tankPlayer, tankTeam);
    }

    /// <summary>
    /// <para>Functional on: Server</para>
    /// Assigns the provided TankNetworkPlayer to the provided TankTeam. Removes player from previous team, if applicable.
    /// </summary>
    /// <param name="tankPlayer"></param>
    /// <param name="tankTeam"></param>
    /// <returns>True if successful, false otherwise.</returns>
    public bool AssignPlayerToTeam(ATankPlayer tankPlayer, TankTeam tankTeam) {
        if ((!IsServer && !IsHost) || tankPlayer == null || tankTeam == null) return false;

        // Remove player from previous team if they are currently assigned to a team
        if(GetTeamByTankPlayerID(tankPlayer.TankPlayerID) != null) {
            RemovePlayerFromTeam(tankPlayer);
        }

        Debug.Log("AssignPlayerToTeam()");
        tankTeam.AddPlayer(tankPlayer);
        TankPlayerIDToTankTeam[tankPlayer.TankPlayerID] = tankTeam;

        return true;
    }

    /// <summary>
    /// <para>Functional on: Server</para>
    /// Remove the provided TankNetworkPlayer from their respective TankTeam
    /// </summary>
    /// <param name="tankPlayer"></param>
    /// <returns>True if removal was successful, false otherwise.</returns>
    public bool RemovePlayerFromTeam(ATankPlayer tankPlayer) {
        if ((!IsServer && !IsHost) || tankPlayer == null) return false;

        if (TankPlayerIDToTankTeam.TryGetValue(tankPlayer.TankPlayerID, out TankTeam tankTeam)) {
            tankTeam.RemovePlayer(tankPlayer);
            TankPlayerIDToTankTeam.Remove(tankPlayer.TankPlayerID);
            return true;
        }

        return false;
    }

    /// <summary>
    /// <para>Functional on: Server, Client</para>
    /// Register the provided team with this TankTeamManager.
    /// </summary>
    /// <param name="team"></param>
    private void RegisterTeam(TankTeam team) {
        if (team == null || !team.IsInitialized || GetTeamByTeamID(team.TeamID) != null) return;
        Debug.Log("REGISTERTEAM WAS CALLED");
        Teams[team.TeamID] = team;
        TankTeamToTeamID[team] = team.TeamID;
        return;
    }

    private void UnregisterTeam(TankTeam team) {
        if (team == null) return;
        Debug.Log("UNREGISTERTEAM WAS CALLED");
        Teams.Remove(team.TeamID);
        TankTeamToTeamID.Remove(team);
        return;
    }

    /// <summary>
    /// <para>Functional on: Server, Client</para>
    /// Gets a TankTeam with associated teamID
    /// </summary>
    /// <param name="teamID"></param>
    /// <returns>TankTeam if teamID was associated with a team, null otherwise.</returns>
    public TankTeam GetTeamByTeamID(int teamID) {
        TankTeam tankTeam;
        Teams.TryGetValue(teamID, out tankTeam);
        return tankTeam;
    }

    /// <summary>
    /// Gets a TankTeam that contains ATankPlayer with provided tankPlayerID.  Returns null if player is not registered with a team.
    /// </summary>
    /// <param name="clientID"></param>
    /// <returns>TankTeam that NetworkPlayer with provided clientID is a member of, null if player is not registered with a team.</returns>
    public TankTeam GetTeamByTankPlayerID(int tankPlayerID) {
        TankTeam tankTeam;
        TankPlayerIDToTankTeam.TryGetValue(tankPlayerID, out tankTeam);
        return tankTeam;
    }

    /// <summary>
    /// <para>Functional on: Server</para>
    /// Creates all teams based on provided teamConfigs and network spawns them. Clears any previously created teams.
    /// </summary>
    /// <param name="teams"></param>
    /// <returns>True if setting all teams was successful, false if any team registration fails.</returns>
    public bool CreateTeams(List<TankTeam.TankTeamConfig> teamConfigs) {
        if ((!IsServer && !IsHost) || teamConfigs == null || _tankTeamPrefab == null) return false;

        //ClearTeams();
        for (int i = 0; i < teamConfigs.Count; i++) {
            //TankTeam newTankTeam = new TankTeam(teamConfigs[i]);
            //TankTeam newTankTeam = gameObject.AddComponent<TankTeam>();
            GameObject tankTeamGameObject = Instantiate(_tankTeamPrefab);
            NetworkObject tankTeamNetworkObject = tankTeamGameObject.GetComponent<NetworkObject>();
            if (tankTeamNetworkObject == null) {
                Destroy(tankTeamGameObject);
                continue;
            }

            tankTeamNetworkObject.Spawn(destroyWithScene: false);

            TankTeam newTankTeam = tankTeamGameObject.GetComponent<TankTeam>();
            if (newTankTeam == null) { Debug.Log("newTankTeam was null!"); continue; }
            newTankTeam.Initialize(teamConfigs[i]);


            /*
            bool teamWasRegistered = RegisterTeam(newTankTeam);
            if (!teamWasRegistered) {
                ClearTeams();
                return false;
            }
            */
        }

        return true;
    }

    /*
    private TankTeam CreateTeam() {
        TankTeam team = new TankTeam();
        Teams[team.TeamID] = team;
        TankTeamToTeamID.Add(team, team.TeamID);

        return team;
    }

    /// <summary>
    /// Generate teamCount number of teams.  Automatically clears any previously existing teams.
    /// </summary>
    /// <param name="teamCount"></param>
    public void CreateTeams(int teamCount) {
        ClearTeams();
        for(int i = 0; i < teamCount; i++) {
            TankTeam team = CreateTeam();
            // Do something with team if necessary
        }
    }
    */

    /// <summary>
    /// <para>Functional on: Server</para>
    /// Clears and destroys all current teams.
    /// </summary>
    public void ClearTeams() {
        if (!IsServer && !IsHost) return;

        List<int> teamIDKeys = new List<int>();
        foreach(int teamID in Teams.Keys) {
            teamIDKeys.Add(teamID);
        }

        for(int i = 0; i < teamIDKeys.Count; i++) {
            TankTeam team = Teams[teamIDKeys[i]];
            team.Dispose();
            //NetworkObject teamNetworkObject = team.GetComponent<NetworkObject>();

            //teamNetworkObject?.Despawn();

            //DestroyProp(team.gameObject);
        }

        Teams.Clear();
        TankTeamToTeamID.Clear();
        TankPlayerIDToTankTeam.Clear();
    }

    /// <summary>
    /// <para>Functional on: Server</para>
    /// Clears all teams and destroys this TankTeamManager.
    /// </summary>
    public void Dispose() {
        if (!IsServer && !IsHost) return;

        ClearTeams();
        gameObject.GetComponent<NetworkObject>()?.Despawn();
        //DestroyProp(gameObject);
    }

    //private void AssignPlayerToTeam()

    /// <summary>
    /// Retrieves the team with the lowest player count (or some team that is tied for lowest count).
    /// Returns null if no teams are available.
    /// </summary>
    /// <returns></returns>
    private TankTeam GetTeamWithLowestPlayerCount() {
        if (Teams.Count < 1) return null;

        TankTeam lowestPlayerCountTeam = Teams[0];
        foreach(int teamID in Teams.Keys) {
            TankTeam team = Teams[teamID];
            if (team.TeamMemberCount < lowestPlayerCountTeam.TeamMemberCount) lowestPlayerCountTeam = team;
        }

        return lowestPlayerCountTeam;
    }
}