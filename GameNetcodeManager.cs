using System;
using UnityEngine;
using Unity.Netcode;
using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Authentication;
using UnityEngine.SceneManagement;
using Unity.Collections;
using System.Net;
using System.Collections;

/// <summary>
/// Singleton class that handles player connections and keeps a list of currently connected NetworkPlayer's
/// </summary>
public class GameNetcodeManager : NetworkBehaviour {
    //[SerializeField] Canvas _mapVoteCanvasPrefab;

    public static GameNetcodeManager Singleton { get; internal set; }

    public string PlayerId { get { return AuthenticationService.Instance?.PlayerId; } }
    public string PlayerName { get { return AuthenticationService.Instance?.PlayerName; } }

    //private Dictionary<ulong, NetworkPlayer> connectedPlayers = new Dictionary<ulong, NetworkPlayer>();

    //public AMatchLogic CurrentMatchLogic { get; private set; } = null;

    async void Awake() {
        if (Singleton != null) {
            // As long as you aren't creating multiple NetworkManager instances, throw an exception.
            // (***the current position of the callstack will stop here***)
            throw new Exception($"Detected more than one instance of {nameof(GameNetcodeManager)}! " +
                $"Do you have more than one component attached to a {nameof(GameObject)}");
        }

        Singleton = this;

        TankMapLoader.Singleton.Initialize();

        TankRelay.ClientStopped += OnClientStoppedHandler;
        await InitializeUnityServices();
        await SignInToUnityServicesAnonymously();
        await TankVivox.InitializeAsync();

        SceneManager.LoadScene("MainMenuScene");
    }

    // Start is called before the first frame update
    async void Start() {
        if (Singleton != this) {
            return;
        }

        //NM = NetworkManager.Singleton;

        if (NetworkManager.Singleton == null) {
            throw new Exception($"There is no {nameof(NetworkManager)} for the {nameof(GameNetcodeManager)} to do stuff with! " +
                $"Please add a {nameof(NetworkManager)} to the scene.");
        }
        //await TankVivox.LoginAsync((AuthenticationService.Instance.PlayerName == null) ? "NameDefault" : AuthenticationService.Instance.PlayerName);
    }

    void Update() {
        /*
        if(CurrentMatchLogic != null) {
            CurrentMatchLogic.Update(Time.deltaTime);
        }
        */

        //TankLobby.Singleton.Update(Time.deltaTime);
    }


    private void OnClientStoppedHandler(bool wasHost) {
        TankMapLoader.Singleton.LoadMainMenu();
    }

    /// <summary>
    /// Call this before doing *any* Unity Services calls.
    /// </summary>
    public async Task InitializeUnityServices() {
        try {
            await UnityServices.InitializeAsync();
        } catch (ServicesInitializationException e) {
            Debug.Log(e);
        } catch (Exception e) {
            Debug.Log(e);
        }
    }

    public async Task<bool> SignInToUnityServicesAnonymously() {
        // FOR DEBUGGING, REMOVE FROM PRODUCTION ============================================
        AuthenticationService.Instance.ClearSessionToken();
        Debug.Log("cleared session token");
        // ==================================================================================

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


    // ===================================================================================================

    // GENERAL RPC'S FOR LOBBY/RELAY MANAGEMENT ==========================================================

    // ===================================================================================================

    /// <summary>
    /// Send the current map vote mapNames to the clients and intantiate the vote menu.
    /// </summary>
    /// <param name="mapNames"></param>
    [Rpc(SendTo.ClientsAndHost)]
    public void StartMapVoteClientRpc(FixedString128Bytes[] mapNames) {
        string[] mapNameStrings = TankGameUtilities.ParseFixedString128BytesArray(mapNames);

        TankUIManager.Singleton.GameplayUI.BeginMapVote(mapNameStrings);
        /*
        Canvas mapVoteCanvasGO = Instantiate(_mapVoteCanvasPrefab);
        if(mapVoteCanvasGO.TryGetComponent(out MapVoteCanvas mapVoteCanvas)) {
            mapVoteCanvas.SetMapVoteEntries(mapNameStrings);
        }
        */
    }

    /// <summary>
    /// Cast a vote for a given map.  voteIndex is an array index for the current mapNames string array.
    /// </summary>
    /// <param name="voteIndex"></param>
    [Rpc(SendTo.Server)]
    public void CastMapVoteServerRpc(ulong clientId, int voteIndex) {
        Debug.Log("map vote was cast for index " + voteIndex + " from clientId " + clientId);
        MapVote.Singleton.CastVote(clientId, voteIndex);
    }

    // ===================================================================================================

    // ===================================================================================================

    // ===================================================================================================


    /*
    public bool StartHost() {
        bool startedHost = false;
        if (!NM.IsServer && !NM.IsClient) {
            startedHost = NM.StartHost();
        }

        if (startedHost) {
            NM.ConnectionApprovalCallback = ConnectionApprovalCallback;
            NM.SceneManager.OnSceneEvent += NetworkedSceneManager_OnSceneEvent;

            //RegisterNewNetworkPlayer(NM.LocalClientId);
        }

        return startedHost;
    }

    public bool StartClient() {
        bool startedClient = false;
        if (!NM.IsServer && !NM.IsClient) {
            startedClient = NM.StartClient();
        }

        if (startedClient) {
            NM.SceneManager.OnSceneEvent += NetworkedSceneManager_OnSceneEvent;
        }

        return startedClient;
    }
    */

    /// <summary>
    /// Call this before doing *any* Unity Services calls.
    /// </summary>
    /*
    public bool StartHost() {
        bool startedHost = false;
        if (!NM.IsServer && !NM.IsClient) {
            startedHost = NM.StartHost();
        }

        if (startedHost) {
            NM.ConnectionApprovalCallback = ConnectionApprovalCallback;
            NM.SceneManager.OnSceneEvent += NetworkedSceneManager_OnSceneEvent;

            //RegisterNewNetworkPlayer(NM.LocalClientId);
        }

        return startedHost;
    }

    public bool StartClient() {
        bool startedClient = false;
        if (!NM.IsServer && !NM.IsClient) {
            startedClient = NM.StartClient();
        }

        if (startedClient) {
            NM.SceneManager.OnSceneEvent += NetworkedSceneManager_OnSceneEvent;
        }

        return startedClient;
    }
    */

    /*
    private void OnDestroy() {
        // Since the NetworkManager can potentially be destroyed before this component, only
        // remove the subscriptions if that singleton still exists.
        if (NM != null) {
            NM.OnClientConnectedCallback -= OnClientConnectedCallback;
            NM.OnClientDisconnectCallback -= OnClientDisconnectCallback;
        }
    }
    */
}