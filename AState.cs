using System;

public abstract class AState<T> where T : Enum {

    public AStateMachine<T> StateMachine { get; private set; }
    public T StateKey { get; private set; }
    public delegate void UpdateDelegate();
    public delegate void FixedUpdateDelegate();

    public UpdateDelegate NetworkUpdate;
    public FixedUpdateDelegate NetworkFixedUpdate;

    public AState(AStateMachine<T> sm, T key, bool isOwner) {
        StateMachine = sm;
        StateKey = key;

        SetIsOwner(isOwner);
    }

    public void SetIsOwner(bool isOwner) {
        if (isOwner) {
            NetworkUpdate = OwnerUpdate;
            NetworkFixedUpdate = OwnerFixedUpdate;
        } else {
            NetworkUpdate = NonOwnerUpdate;
            NetworkFixedUpdate = NonOwnerFixedUpdate;
        }
    }

    public abstract void Enter();
    public abstract void Exit();
    public abstract T GetNextState();

    public virtual void Awake() { }
    public virtual void OnNetworkSpawn() { }

    public virtual void Update() { }
    public virtual void FixedUpdate() { }

    protected virtual void OwnerUpdate() { }
    protected virtual void OwnerFixedUpdate() { }

    protected virtual void NonOwnerUpdate() { }
    protected virtual void NonOwnerFixedUpdate() { }
    /*
    public void Update() {
        if (IsOwner) OwnerUpdate();
        else NonOwnerUpdate();
    }
    public void FixedUpdate() {
        if (IsOwner) OwnerFixedUpdate();
        else NonOwnerFixedUpdate();
    }
    */
}