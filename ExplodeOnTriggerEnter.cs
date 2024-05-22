using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Explode))]
public class ExplodeOnTriggerEnter : NetworkBehaviour {

    private bool flaggedForDestroy = false;
    private Explode explodableComponent;

    void Awake() {
        explodableComponent = GetComponent<Explode>();
    }

    [Rpc(SendTo.Server)]
    private void ServerProcessTriggerEnterRpc() {
        if (flaggedForDestroy) return;

        flaggedForDestroy = true;
        explodableComponent.DoExplosion();
    }

    private void OnTriggerEnter(Collider collider) {
        if (!IsOwner || flaggedForDestroy || !NetworkObject.IsSpawned) return;

        if (!IsServer && !IsHost) flaggedForDestroy = true;

        ServerProcessTriggerEnterRpc();
    }
}