using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;

public class TankTeam : NetworkBehaviour {
    public Color TeamColor { get { return teamColor.Value; } }

    /// <summary>
    /// Total of all TeamMembers (includes all Players, AI, etc.)
    /// </summary>
    public int TeamMemberCount { get { return teamMembers.Count; } }

    /// <summary>
    /// Total of all TeamMembers that are TankNetworkPlayers, disregarding AI and others
    /// </summary>
    public int TeamMemberCountPlayers { get { return teamMembers.Count - teamAIMembers.Count; } }

    /// <summary>
    /// Total of all TeamMembers that are strictly AI, disregarding Players
    /// </summary>
    public int TeamMemberCountAI { get { return teamAIMembers.Count; } }
    public int MaxTeamMemberCount
    {
        get {
            return maxTeamMemberCount.Value;
        }
        private set {
            maxTeamMemberCount.Value = Mathf.Max(0, value);
        }
    }
    public int TeamID { get { return teamID.Value; } }
    public int Score { get { return score.Value; } }
    public bool TeamFull { get { return teamMembers.Count >= MaxTeamMemberCount; } }
    /// <summary>
    /// Are all team slots full, disregarding any AI teammates
    /// </summary>
    public bool TeamFullIgnoreAI { get { return (teamMembers.Count - teamAIMembers.Count) >= MaxTeamMemberCount; } }

    public bool IsInitialized { get { return isInitialized.Value; }  }

    private NetworkVariable<Color> teamColor = new NetworkVariable<Color>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> maxTeamMemberCount = new NetworkVariable<int>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> teamID = new NetworkVariable<int>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> score = new NetworkVariable<int>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> isInitialized = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>
    /// Set of all ATankPlayers in this TankTeam
    /// </summary>
    private HashSet<ATankPlayer> teamMembers = new HashSet<ATankPlayer>();

    /// <summary>
    /// List of ATankPlayers in teamMembers that are TankAIPlayers
    /// </summary>
    private List<TankAIPlayer> teamAIMembers = new List<TankAIPlayer>();

    //private Dictionary<ulong, TankNetworkPlayer> clientIdToNetworkPlayer = new Dictionary<ulong, TankNetworkPlayer>();
    private List<SpawnPoint> teamSpawnPointsList = new List<SpawnPoint>();
    private Dictionary<SpawnPoint, int> teamSpawnPointsDictionary = new Dictionary<SpawnPoint, int>();

    public static Action<TankTeam> TeamInitialized;

    public static Action<TankTeam> TeamNetworkSpawned;

    public static Action<TankTeam> TeamNetworkDespawned;

    public static Action<ATankPlayer, TankTeam> PlayerJoinedTeam;

    public static Action<ATankPlayer, TankTeam> PlayerLeftTeam;

    public static Action<TankTeam> ScoreChanged;

    public const int NO_TEAM = -1;

    /*
    public struct TankTeamNetworkData : INetworkSerializable, IEquatable<TankTeamNetworkData>
    {
        Color teamColor;
        int teamID;
        int score;

        public bool Equals(TankTeamNetworkData other)
        {
            return teamColor.Equals(other.teamColor) && score == other.score;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            if (serializer.IsReader)
            {
                FastBufferReader reader = serializer.GetFastBufferReader();
                reader.ReadValueSafe(out teamColor);
                reader.ReadValueSafe(out teamID);
                reader.ReadValueSafe(out score);
            } else
            {
                FastBufferWriter writer = serializer.GetFastBufferWriter();
                writer.WriteValueSafe(teamColor);
                writer.WriteValueSafe(teamID);
                writer.WriteValueSafe(score);
            }
        }
    }
    */

    [System.Serializable]
    public class TankTeamConfig {
        public Color TeamColor;
        public int MaxTeamMemberCount;
        private int teamID;

        /*
        public TankTeamConfig(Color teamColor, int maxTeamMemberCount) {
            TeamColor = teamColor;
            MaxTeamMemberCount = maxTeamMemberCount;
        }
        */

        public int TeamID
        {
            get { return teamID; }
            set { teamID = value; }
        }
    }

    /*
    private TankTeam() { }

    internal TankTeam(TankTeamConfig teamConfig)
    {
        TeamID = teamConfig.TeamID;//getNextTeamID();
        TeamColor = teamConfig.TeamColor;
        MaxTeamMemberCount = teamConfig.MaxTeamMemberCount;
        if (MaxTeamMemberCount <= 0) TeamFull = true;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectCallback;
    }
    */

    /*
    private static TankTeam CreateTankTeam(TankTeamConfig teamConfig) {
        return new TankTeam(teamConfig);
    }
    */

    /*
    internal static List<TankTeam> CreateTankTeams(List<TankTeamConfig> teamConfigs) {
        List<TankTeam> tankTeams = new List<TankTeam>();
        for (int i = 0; i < teamConfigs.Count; i++) {
            tankTeams.Add(new TankTeam(teamConfigs[i]));
        }

        return tankTeams;
    }
    */

    /* this works but doesn't allow testing offline
    public override void OnNetworkSpawn() {
        TeamInitialized?.Invoke(this);
    }
    */

    public override void OnDestroy() {
        base.OnDestroy();

        ATankPlayer.AssignedTeamIDChanged -= OnTankPlayerAssignedTeamIDChanged;
    }

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();

        ATankPlayer.AssignedTeamIDChanged += OnTankPlayerAssignedTeamIDChanged;
        ATankPlayer.TankPlayerDestroyed += OnTankPlayerDestroyed;

        TeamNetworkSpawned?.Invoke(this);

        // Iterate existing players to add to team to fix timing issue
        // where ATankPlayer may network spawn and trigger event prior to this team being network spawned and registered.
        List<ATankPlayer> players = TankRelay.Singleton.GetTankPlayers();
        for (int i = 0; i < players.Count; i++) {
            ATankPlayer player = players[i];
            Debug.Log("iterating: " + (player.AssignedTankTeamID) + " : " + TeamID);
            if(player.AssignedTankTeamID == TeamID) {
                player.UpdateAssignedTankTeam(TankTeam.NO_TEAM);
            }
        }
    }

    public override void OnNetworkDespawn() {
        base.OnNetworkDespawn();

        /*
        List<ATankPlayer> playerMembers = GetTeamMembers();
        for(int i = 0; i < playerMembers.Count; i++) {
            playerMembers[i].RemoveTeamAssignment();
        }
        */

        TeamNetworkDespawned?.Invoke(this);
    }

    private void OnTankPlayerAssignedTeamIDChanged(ATankPlayer player, int oldTeamID, int newTeamID) {
        if (player == null) return;

        if(oldTeamID == TeamID) {
            bool removed = teamMembers.Remove(player);
            if (player is TankAIPlayer) {
                teamAIMembers.Remove((TankAIPlayer)player);
            }

            //clientIdToNetworkPlayer.Remove(tankPlayer.ClientID);

            if (removed) PlayerLeftTeam?.Invoke(player, this);
        } else if(newTeamID == TeamID) {
            teamMembers.Add(player);
            if (player is TankAIPlayer) {
                teamAIMembers.Add((TankAIPlayer)player);
            }

            PlayerJoinedTeam?.Invoke(player, this);
        }
    }

    /// <summary>
    /// Handler for if a TankPlayer leaves the game and their ATankPlayer instance is despawned.
    /// </summary>
    /// <param name="player"></param>
    private void OnTankPlayerDestroyed(ATankPlayer player) {
        if (player.AssignedTankTeamID != TeamID) return;

        bool removed = teamMembers.Remove(player);
        if (player is TankAIPlayer) {
            teamAIMembers.Remove((TankAIPlayer)player);
        }

        //clientIdToNetworkPlayer.Remove(tankPlayer.ClientID);

        if (removed) PlayerLeftTeam?.Invoke(player, this);
    }

    public void Initialize(TankTeamConfig teamConfig) {
        if ((!IsServer && !IsHost) || IsInitialized) return;
        teamID.Value = teamConfig.TeamID;
        teamColor.Value = teamConfig.TeamColor;
        MaxTeamMemberCount = teamConfig.MaxTeamMemberCount;
        //if (MaxTeamMemberCount <= 0) TeamFull = true;

        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectCallback;

        isInitialized.Value = true;

        Debug.Log("Initialized TankTeam with TeamID " + teamConfig.TeamID);
        TeamInitialized?.Invoke(this);
    }

    public void IncreaseScore(int amount) {
        amount = Mathf.Max(amount, 0);
        score.Value += amount;
        if (amount > 0) ScoreChanged?.Invoke(this);
    }
    
    /// <summary>
    /// <para>Functional on: Server</para>
    /// Calls SetTeamAssignment() on the provided tankPlayer.
    /// </summary>
    /// <param name="tankPlayer"></param>
    /// <returns></returns>
    public bool AddPlayer(ATankPlayer tankPlayer) {
        if (TeamFull || tankPlayer == null || (!IsServer && !IsHost)) return false;

        /*
        if (TeamFull && (tankPlayer is TankNetworkPlayer)) {
            // Team is only full because there exists at least one AI on the team, so remove one AI
            TankAIPlayer randomAI = GetRandomAIPlayer();
            RemovePlayer(randomAI);
        }
        */
        //PlayerJoinedTeamRpc(tankPlayer.TankPlayerID);
        tankPlayer.SetTeamAssignment(this);

        //if (TeamMemberCount >= MaxTeamMemberCount) TeamFull = true;
        return true;
    }

    /// <summary>
    /// <para>Functional on: Server</para>
    /// Calls RemoveTeamAssignment() on the provided tankPlayer.
    /// </summary>
    /// <param name="tankPlayer"></param>
    /// <returns></returns>
    public bool RemovePlayer(ATankPlayer tankPlayer) {
        if (tankPlayer == null || (!IsServer && !IsHost)) return false;

        //PlayerLeftTeamRpc(tankPlayer.TankPlayerID);
        tankPlayer.RemoveTeamAssignment();

        return true;
    }

    /// <summary>
    /// Get all ATankPlayer Team Members. Uses ToList(), so don't call rapidly (like in Update).
    /// </summary>
    /// <returns>List of ATankPlayer's that are members of this team.</returns>
    public List<ATankPlayer> GetTeamMembers() {
        return teamMembers.ToList();
    }

    /// <summary>
    /// Return a random AI player from this team.
    /// </summary>
    /// <returns>An AIPlayer instance that is a member of this team. Null if no AI are on this team.</returns>
    public TankAIPlayer GetRandomAIPlayer() {
        return (teamAIMembers.Count > 0) ? teamAIMembers[0] : null;
    }

    /// <summary>
    /// Get a random SpawnPoint associated with this TankTeam.
    /// </summary>
    /// <returns>Transform for a team spawn point, null if none available.</returns>
    public SpawnPoint GetRandomSpawnPoint() {
        Debug.Log("GetRandomSpawnPoint for team with TeamID " + TeamID + " was called");
        Debug.Log("Spawnpoint count: " + teamSpawnPointsList.Count);
        SpawnPoint spawnPoint = null;
        if (teamSpawnPointsList.Count > 0)
        {
            spawnPoint = teamSpawnPointsList[UnityEngine.Random.Range(0, teamSpawnPointsList.Count)];
        }

        return spawnPoint;
    }

    /// <summary>
    /// Register a SpawnPoint for this TankTeam to spawn at.  SpawnPoint.TeamID must equal this TankTeam.TeamID.
    /// </summary>
    /// <param name="spawnPoint"></param>
    public void AddSpawnPoint(SpawnPoint spawnPoint) {
        if (teamSpawnPointsDictionary.ContainsKey(spawnPoint) || spawnPoint.TeamID != TeamID) return;
        teamSpawnPointsList.Add(spawnPoint);
        teamSpawnPointsDictionary.Add(spawnPoint, teamSpawnPointsList.Count - 1);
        Debug.Log("SpawnPoint added to team " + TeamID);
    }

    public bool RemoveSpawnPoint(SpawnPoint spawnPoint) {
        if (spawnPoint == null || !teamSpawnPointsDictionary.ContainsKey(spawnPoint)) return false;
        int index = teamSpawnPointsDictionary[spawnPoint];
        teamSpawnPointsDictionary.Remove(spawnPoint);
        teamSpawnPointsList.RemoveAt(index);

        return true;
    }

    public void Dispose() {
        if (!IsServer && !IsHost) return;

        teamMembers.Clear();
        teamAIMembers.Clear();
        //clientIdToNetworkPlayer.Clear();
        teamSpawnPointsList.Clear();
        teamSpawnPointsDictionary.Clear();
        gameObject.GetComponent<NetworkObject>()?.Despawn();
    }

    public bool IsTankPlayerMemberOfTeam(ATankPlayer tankPlayer) {
        return teamMembers.Contains(tankPlayer);
    }

    /*
    private static int getNextTeamID() {
        int nextID = teamIdIterator;
        teamIdIterator++;
        return nextID;
    }
    */

    private void OnClientDisconnectCallback(ulong clientId) {
        TankNetworkPlayer disconnectedPlayer = TankRelay.Singleton.GetTankPlayer(clientId);
        if (disconnectedPlayer == null) return;
        bool clientIsMemberOfTeam = IsTankPlayerMemberOfTeam(disconnectedPlayer);//TankRelay.GetTankPlayer(clientId);//clientIdToNetworkPlayer.TryGetValue(clientId, out disconnectedPlayer);
        if (clientIsMemberOfTeam)
        {
            Debug.Log("Player disconnected, removed from TankTeam");
            RemovePlayer(disconnectedPlayer);
        }
    }
}