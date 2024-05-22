using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System;

/// <summary>
/// Abstract class used to emit death event
/// </summary>
public abstract class ANetworkDie : NetworkBehaviour {
    public Action<DeathInfo> Died;

    public override void OnNetworkDespawn() {
        base.OnNetworkDespawn();
    }

    /// <summary>
    /// Cause the gameObject to die and despawn. Invokes Died event and triggers other components based on death.
    /// </summary>
    /// <param name="deathInfo"></param>
    public virtual void NetworkDie(DeathInfo deathInfo) {
        if (!IsServer && !IsHost) return;

        // This invocation is the main reason this function exists. A LOT of systems listen for this event.
        if (!NetworkObject.IsSpawned) return;
        Died?.Invoke(deathInfo);
        NetworkObject.Despawn();

        //DestroyProp(gameObject);
    }
}