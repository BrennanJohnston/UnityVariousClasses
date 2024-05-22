using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class MapConfig : MonoBehaviour {
    [SerializeField] private List<TankTeam.TankTeamConfig> _teams;
    [SerializeField] private int _scoreLimit;
    [SerializeField] private float _timeLimit;
    [SerializeField] private float _spawnTime;
    [SerializeField] private GameMode _gameMode;
    [SerializeField] private bool _friendlyFireEnabled;
    [SerializeField] private GameObject _defaultPlayerPrefab;
    [SerializeField] private bool _spawnAI;

    public const string SPAWN_POINT_TAG = "SpawnPoint";

    public enum GameMode {
        TDM,
        CTF
    }

    public struct MapConfigurationData {
        public List<TankTeam.TankTeamConfig> Teams { get; private set; }
        public List<SpawnPoint> SpawnPoints { get; private set; }
        public int ScoreLimit { get; private set; }
        public float TimeLimit { get; private set; }
        public float SpawnTime { get; private set; }
        public MapConfig.GameMode GameMode { get; private set; }
        public bool FriendlyFireEnabled { get; private set; }
        public GameObject DefaultPlayerPrefab { get; private set; }
        public bool SpawnAI { get; private set; }
        
        public MapConfigurationData(List<TankTeam.TankTeamConfig> teams, List<SpawnPoint> spawnPoints, int scoreLimit, float timeLimit, float spawnTime, MapConfig.GameMode gameMode, bool friendlyFireEnabled, GameObject defaultPlayerPrefab, bool spawnAI) {
            Teams = teams;
            SpawnPoints = spawnPoints;
            ScoreLimit = scoreLimit;
            TimeLimit = timeLimit;
            SpawnTime = spawnTime;
            GameMode = gameMode;
            FriendlyFireEnabled = friendlyFireEnabled;
            DefaultPlayerPrefab = defaultPlayerPrefab;
            SpawnAI = spawnAI;
        }
    }

    // Enforce singleton
    public static MapConfig Singleton = null;

    void Awake() {
        if(Singleton != null) {
            Debug.Log("MORE THAN ONE MapConfig EXISTED ON THE MAP!");
            Destroy(this);
            return;
        }

        Singleton = this;
    }

    public MapConfigurationData GetMapConfiguration() {
        List<SpawnPoint> spawnPoints = GetSpawnPointsInScene();

        for(int i = 0; i < _teams.Count; i++) {
            TankTeam.TankTeamConfig teamConfig = _teams[i];
            teamConfig.TeamID = i;
        }

        MapConfigurationData mapConfigData = new MapConfigurationData(_teams, spawnPoints, _scoreLimit, _timeLimit, _spawnTime, _gameMode, _friendlyFireEnabled, _defaultPlayerPrefab, _spawnAI);

        return mapConfigData;
    }

    private List<SpawnPoint> GetSpawnPointsInScene() {
        List<SpawnPoint> spawnPoints = new List<SpawnPoint>();
        GameObject[] spawnPointObjectList = GameObject.FindGameObjectsWithTag(SPAWN_POINT_TAG);
        for(int i = 0; i < spawnPointObjectList.Length; i++) {
            if(spawnPointObjectList[i].TryGetComponent(out SpawnPoint spawnPointComponent)) {
                spawnPoints.Add(spawnPointComponent);
            }
        }

        return spawnPoints;
    }
}