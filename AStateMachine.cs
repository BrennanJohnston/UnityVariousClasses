using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public abstract class AStateMachine<T> : NetworkBehaviour where T : Enum {
    protected Dictionary<T, AState<T>> StateDictionary = new Dictionary<T, AState<T>>();

    protected AState<T> CurrentState;

    protected virtual void Awake() { }

    protected virtual void Start() { }

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();
        //if (!IsOwner) return;
        CurrentState.Enter();
    }

    public override void OnGainedOwnership() {
        base.OnGainedOwnership();

        UpdateStateOwnerships();
    }

    public override void OnLostOwnership() {
        base.OnLostOwnership();

        UpdateStateOwnerships();
    }

    private void UpdateStateOwnerships() {
        foreach(T key in StateDictionary.Keys) {
            AState<T> state = StateDictionary[key];
            state.SetIsOwner(IsOwner);
        }
    }

    protected virtual void Update() {
        //if (!IsOwner) return;

        T nextStateKey = CurrentState.GetNextState();
        if(!nextStateKey.Equals(CurrentState.StateKey)) {
            TransitionState(nextStateKey);
        }

        CurrentState.Update();
        CurrentState.NetworkUpdate();
    }

    protected virtual void FixedUpdate() {
        //if (!IsOwner) return;

        CurrentState.FixedUpdate();
        CurrentState.NetworkFixedUpdate();
    }

    private void TransitionState(T stateKey) {
        CurrentState.Exit();
        CurrentState = StateDictionary[stateKey];
        CurrentState.Enter();
    }
}