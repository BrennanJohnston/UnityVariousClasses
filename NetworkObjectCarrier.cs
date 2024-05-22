using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.VisualScripting;
using System;

/// <summary>
/// Class designed to allow for attachment (parenting) of NetworkObjects to other NetworkObjects at specific Transforms.
/// Aids in dealing with NGO parenting limitations.
/// NetworkObjectCarrier is meant to be used with objects that have a NetworkObjectCarryable component.
/// </summary>
public class NetworkObjectCarrier : NetworkBehaviour {
    [SerializeField] private List<AttachPointEntry> _attachmentPoints;
    [SerializeField] private bool _debugMode = false;

    private Dictionary<string, Transform> AttachmentPoints = new Dictionary<string, Transform>();
    private Dictionary<Transform, List<NetworkObjectCarryable>> AttachedObjects = new Dictionary<Transform, List<NetworkObjectCarryable>>();
    private Dictionary<string, List<NetworkObjectCarryable>> AttachPointNameToObjects = new Dictionary<string, List<NetworkObjectCarryable>>();
    private HashSet<NetworkObjectCarryable> IsCarryingHashSet = new HashSet<NetworkObjectCarryable>();

    public Action<NetworkObjectCarryable, Transform> CarryableAttached;
    public Action<NetworkObjectCarryable> CarryableDetached;

    [System.Serializable]
    public struct AttachPointEntry {
        public string name;
        public Transform transform;
    }

    void Awake() {
        for (int i = 0; i < _attachmentPoints.Count; i++) {
            AttachPointEntry entry = _attachmentPoints[i];
            AttachmentPoints.Add(entry.name, entry.transform);
            AttachedObjects.Add(entry.transform, new List<NetworkObjectCarryable>());
            AttachPointNameToObjects.Add(entry.name, new List<NetworkObjectCarryable>());
        }
    }

    private void OnCarriedObjectDespawned(NetworkObjectCarryable despawnedCarryable) {
        RemoveCarryable(despawnedCarryable, true);

        despawnedCarryable.Despawned -= OnCarriedObjectDespawned;
    }

    /// <summary>
    /// Retrieve a Transform that coincides with <paramref name="attachPointName"/>. The name should be identical to an entry in _attachmentPoints.
    /// Names and coinciding Transforms are added via Unity editor on this Component.
    /// </summary>
    /// <param name="attachPointName"></param>
    /// <returns>Transform paired with provided string, null if not found.</returns>
    public Transform GetAttachPointByName(string attachPointName) {
        Transform attachPoint;
        AttachmentPoints.TryGetValue(attachPointName, out attachPoint);
        return attachPoint;
    }

    public bool HasAttachPoint(string attachPointName) {
        return AttachmentPoints.ContainsKey(attachPointName);
    }

    /// <summary>
    /// Determine if an attachment point with <paramref name="attachPointName"/> is in use or not.
    /// </summary>
    /// <param name="attachPointName"></param>
    /// <returns>True if at least one item is being carried at that attachment point, false otherwise.</returns>
    public bool AttachmentPointInUse(string attachPointName) {
        Transform attachmentPoint = GetAttachPointByName(attachPointName);
        if (attachmentPoint == null) return false;

        List<NetworkObjectCarryable> carryablesAtAttachPoint;
        if (!AttachedObjects.TryGetValue(attachmentPoint, out carryablesAtAttachPoint)) return false;

        return carryablesAtAttachPoint.Count > 0;
    }

    public bool IsCarrying(NetworkObjectCarryable carryable) {
        return IsCarryingHashSet.Contains(carryable);
    }

    /// <summary>
    /// Attach the provided <paramref name="carryableGameObject"/> to the Attachment Point associated with the provided <paramref name="_attachPointName"/>.
    /// The provided <paramref name="carryableGameObject"/> must have a NetworkObjectCarryable component.
    /// <para><paramref name="carryableGameObject"/>.GetComponent&lt;NetworkObjectCarryable&gt;().DefaultAttachPointName will be used if no <paramref name="_attachPointName"/> is provided.</para>
    /// </summary>
    /// <param name="carryableGameObject"></param>
    /// <param name="_attachPointName"></param>
    /// <returns>True if attachment was successful, false otherwise.</returns>
    public bool AttachCarryable(GameObject carryableGameObject, string _attachPointName = null) {
        NetworkObjectCarryable carryable;
        if (!carryableGameObject.TryGetComponent(out carryable)) {
            if (_debugMode) Debug.Log("NetworkObjectCarrier.AttachCarryable(): carryableGameObject did not have a NetworkObjectCarryable component.");
            return false;
        }

        return AttachCarryable(carryable, _attachPointName);
    }

    /// <summary>
    /// Attach the provided <paramref name="carryable"/> to the Attachment Point associated with the provided <paramref name="_attachPointName"/>.
    /// The provided <paramref name="carryable"/> must have a NetworkObjectCarryable component.
    /// <para><paramref name="carryable"/>.DefaultAttachPointName will be used if no <paramref name="_attachPointName"/> is provided.</para>
    /// </summary>
    /// <param name="carryable"></param>
    /// <param name="_attachPointName"></param>
    /// <returns>True if attachment was successful, false otherwise.</returns>
    public bool AttachCarryable(NetworkObjectCarryable carryable, string _attachPointName = null) {
        if (!IsServer && !IsHost) return false;

        string attachPointName = (_attachPointName == null) ? carryable.DefaultAttachPointName : _attachPointName;

        Transform attachmentPoint;
        if (!AttachmentPoints.TryGetValue(attachPointName, out attachmentPoint)) {
            if (_debugMode) Debug.Log("NetworkObjectCarrier.AttachCarryable(): Provided attachPointName was not registered with this NetworkObjectCarrier.");
            return false;
        }

        List<NetworkObjectCarryable> attachPointCarryables;
        if (!AttachedObjects.TryGetValue(attachmentPoint, out attachPointCarryables)) {
            attachPointCarryables = new List<NetworkObjectCarryable>();
            AttachedObjects.Add(attachmentPoint, attachPointCarryables);
        }

        List<NetworkObjectCarryable> nameToCarryables;
        if(!AttachPointNameToObjects.TryGetValue(attachPointName, out nameToCarryables)) {
            nameToCarryables = new List<NetworkObjectCarryable>();
            AttachPointNameToObjects.Add(attachPointName, nameToCarryables);
        }

        NetworkObject carryableNetworkObject;
        if (!carryable.TryGetComponent(out carryableNetworkObject)) {
            if (_debugMode) Debug.Log("NetworkObjectCarrier.AttachCarryable(): carryableGameObject did not have a NetworkObject component.");
            return false;
        }

        bool attachmentSuccessful = carryableNetworkObject.TrySetParent(gameObject);
        if (!attachmentSuccessful) {
            if (_debugMode) Debug.Log("NetworkObjectCarrier.AttachCarryable(): NetworkObject.TrySetParent() failed.");
            return false;
        }

        carryable.transform.position = attachmentPoint.position;
        carryable.transform.rotation = attachmentPoint.rotation;
        carryable.SetCurrentAttachPointName(attachPointName);
        attachPointCarryables.Add(carryable);
        nameToCarryables.Add(carryable);
        IsCarryingHashSet.Add(carryable);

        carryable.Despawned += OnCarriedObjectDespawned;
        CarryableAttached?.Invoke(carryable, attachmentPoint);
        return true;
    }

    public bool RemoveCarryable(GameObject carryableGameObject, bool removeBecauseItDespawned) {
        NetworkObjectCarryable carryable;
        if (!carryableGameObject.TryGetComponent(out carryable)) return false;

        return RemoveCarryable(carryable, removeBecauseItDespawned);
    }

    public bool RemoveCarryable(NetworkObjectCarryable carryable, bool removeBecauseItDespawned) {
        if (!IsServer && !IsHost) return false;

        NetworkObject carryableNetworkObject;
        if (!carryable.TryGetComponent(out carryableNetworkObject)) return false;

        //bool detachSuccessful = carryableNetworkObject.TryRemoveParent();
        //if (!detachSuccessful) return false;

        Transform attachPoint = carryable.CurrentAttachTransform;
        if(attachPoint == null) return true;
        List<NetworkObjectCarryable> attachPointCarryables;
        if (!AttachedObjects.TryGetValue(attachPoint, out attachPointCarryables)) return true;

        List<NetworkObjectCarryable> attachPointNameToObjectsList;
        if (!AttachPointNameToObjects.TryGetValue(carryable.GetCurrentAttachPointName(), out attachPointNameToObjectsList)) return true;

        attachPointCarryables.Remove(carryable);
        attachPointNameToObjectsList.Remove(carryable);
        IsCarryingHashSet.Remove(carryable);

        if(!removeBecauseItDespawned)
            carryableNetworkObject.TryRemoveParent();

        //if (!removalSuccessful) return false;

        CarryableDetached?.Invoke(carryable);
        return true;
    }
}