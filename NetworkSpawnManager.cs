using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System;

public class NetworkSpawnManager : NetworkBehaviour {
    public static NetworkSpawnManager Singleton { get; private set; } = null;

    public static Action<ATankPlayer, NetworkObject> SpawnedTankPlayer;

    void Awake() {
        if(Singleton != null) {
            Debug.Log("NetworkSpawnManager.Singleton already exists, destroying duplicate.");
            Destroy(this);
            return;
        }

        Singleton = this;

        SubscribeToEvents();
    }

    private void SubscribeToEvents() {
        TankNetworkPlayer.TankPlayerWantsToSpawn += OnTankPlayerWantsToSpawn;
    }

    private void UnsubcribeFromEvents() {
        TankNetworkPlayer.TankPlayerWantsToSpawn -= OnTankPlayerWantsToSpawn;
    }

    /// <summary>
    /// Spawn a GameObject that has a NetworkObject Component attached on the network.  Only works if called on server.
    /// Optionally provide a clientId (ownerId) to give ownership to a client.
    /// If ownerId is not provided, the server will own it.
    /// </summary>
    /// <param name="networkObjectPrefab"></param>
    /// <param name="position"></param>
    /// <param name="rotation"></param>
    /// <param name="ownerId"></param>
    /// <returns>NetworkObject component of the instantiated prefab, null if spawn failed.</returns>
    public NetworkObject SpawnNetworkObject(GameObject networkObjectPrefab, Vector3 position, Quaternion rotation, ulong ownerID = NetworkManager.ServerClientId, bool destroyWithScene = true) {
        if (!IsServer && !IsHost) return null;

        bool prefabHasNetworkObjectComponent = networkObjectPrefab.GetComponent<NetworkObject>() != null;
        if (!prefabHasNetworkObjectComponent) return null;

        // Requirements met, Instantiate then network-spawn the object.
        GameObject spawnedObject = Instantiate(networkObjectPrefab, position, rotation);
        NetworkObject spawnedNetworkObject = spawnedObject.GetComponent<NetworkObject>();

        spawnedNetworkObject.SpawnWithOwnership(ownerID, destroyWithScene);

        return spawnedNetworkObject;
    }

    private void OnTankPlayerWantsToSpawn(ATankPlayer player, Transform location) {
        if (player == null || location == null || player.playerGameObjectPrefab == null) return;

        // TODO: DESTROY THE PLAYER IF THEY ARE ALREADY SPAWNED OR SIMPLY DON'T SPAWN THEM HERE
        // *code*

        NetworkObject spawnedPlayer = null;
        if(player is TankAIPlayer) {
            spawnedPlayer = SpawnNetworkObject(player.playerGameObjectPrefab, location.position, location.rotation);
            if(spawnedPlayer.TryGetComponent(out TankStateMachine tankStateMachine)) {
                tankStateMachine.gameObject.AddComponent<TankControllerAITDM>();
            }
        } else if(player is TankNetworkPlayer) {
            TankNetworkPlayer tankNetworkPlayer = (TankNetworkPlayer) player;
            spawnedPlayer = SpawnNetworkObject(tankNetworkPlayer.playerGameObjectPrefab, location.position, location.rotation, tankNetworkPlayer.ClientID);

            /*
            if (spawnedPlayer.TryGetComponent(out TankStateMachine tankStateMachine)) {
                tankStateMachine.AttachTankControllerPlayerRpc(); // Sends RPC to owner to control this Tank
            }
            */
        }

        if (spawnedPlayer == null) return;

        SpawnedTankPlayer?.Invoke(player, spawnedPlayer);
    }
}