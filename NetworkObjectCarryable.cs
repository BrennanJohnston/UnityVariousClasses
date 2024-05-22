using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Collections;
using System;

public class NetworkObjectCarryable : NetworkBehaviour {
    [SerializeField] private string _defaultAttachPointName;

    public NetworkObjectCarrier CurrentCarrier { get; private set; } = null;
    public bool IsBeingCarried { get { return CurrentCarrier != null; } }
    public Transform CurrentAttachTransform { get; private set; } = null;
    public string DefaultAttachPointName { get { return _defaultAttachPointName; } }

    private NetworkVariable<NetworkString64Bytes> CurrentAttachPointName = new NetworkVariable<NetworkString64Bytes>();
    private NetworkTransform networkTransform { get; set; } = null;

    public Action PickedUp;
    public Action Dropped;
    public Action<NetworkObjectCarryable> Despawned;

    protected virtual void Awake() {
        networkTransform = GetComponent<NetworkTransform>();
    }

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();

        CurrentAttachPointName.OnValueChanged += OnCurrentAttachPointNameChanged;

        if (!IsServer && !IsHost) return;
        CurrentAttachPointName.Value = DefaultAttachPointName;
    }

    public override void OnNetworkDespawn() {
        base.OnNetworkDespawn();

        CurrentAttachPointName.OnValueChanged -= OnCurrentAttachPointNameChanged;
        Despawned?.Invoke(this);
    }

    public string GetCurrentAttachPointName() {
        return CurrentAttachPointName.Value.ToString();
    }

    public void SetCurrentAttachPointName(string newAttachPointName) {
        if (!IsServer && !IsHost) return;

        CurrentAttachPointName.Value = newAttachPointName;
    }

    private void Update() {
        if (!IsBeingCarried || CurrentAttachTransform == null) return;

        transform.position = CurrentAttachTransform.position;
        transform.rotation = CurrentAttachTransform.rotation;
    }

    public override void OnNetworkObjectParentChanged(NetworkObject parentNetworkObject) {
        base.OnNetworkObjectParentChanged(parentNetworkObject);
        Debug.Log("OnNetworkObjectParentChanged called");

        NetworkObjectCarrier carrier = parentNetworkObject?.GetComponent<NetworkObjectCarrier>();
        if(carrier == null) {
            CurrentCarrier = null;
            CurrentAttachTransform = null;
            EnableNetworkTransform(true);
            Dropped?.Invoke();
            return;
        }

        CurrentCarrier = carrier;
        Transform newAttachTransform = CurrentCarrier.GetAttachPointByName(CurrentAttachPointName.Value.ToString());
        if(newAttachTransform == null) {
            Debug.Log("newAttachTransform was null");
            PickedUp?.Invoke();
            return;
        }

        CurrentAttachTransform = newAttachTransform;
        EnableNetworkTransform(false);
        PickedUp?.Invoke();
    }

    private void OnCurrentAttachPointNameChanged(NetworkString64Bytes oldName, NetworkString64Bytes newName) {
        Debug.Log("OnCurrentAttachPointNameChanged was called");

        if (CurrentCarrier == null) return;

        Transform newAttachTransform = CurrentCarrier.GetAttachPointByName(newName.ToString());
        if (newAttachTransform == null) {
            Debug.Log("newAttachTransform was null");
            return;
        }

        CurrentAttachTransform = newAttachTransform;
        EnableNetworkTransform(false);
    }

    /*
    /// <summary>
    /// This function is used by NetworkObjectCarrier and should *never* be called manually.
    /// </summary>
    /// <param name="transform"></param>
    public void SetCurrentAttachTransform(Transform transform) {
        Debug.Log("SetCurrentAttachTransform was called");
        CurrentAttachTransform = transform;

        if (CurrentAttachTransform == null) {
            Debug.Log("transform was null");
            return;
        }

        transform.position = CurrentAttachTransform.position;
        transform.rotation = CurrentAttachTransform.rotation;
    }
    */

    /*
    private Transform GetCurrentAttachPoint() {
        Transform newAttachPoint = CurrentCarrier?.GetAttachPointByName(CurrentAttachPointName.ToString());
        return newAttachPoint;
    }
    */

    /*
    private NetworkObjectCarrier GetNetworkObjectCarrierInParent(NetworkObject parentNetworkObject) {
        Debug.Log("GetNetworkObjectCarrierInParent was called");
        NetworkObjectCarrier carrier = parentNetworkObject.GetComponent<NetworkObjectCarrier>();
        if (carrier == null) {
            Debug.Log("No carrier was found in parentNetworkObject");
        }

        return carrier;
    }
    */

    private void EnableNetworkTransform(bool enable) {
        if (networkTransform == null) return;
        networkTransform.enabled = enable;
    }
}