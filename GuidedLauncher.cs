using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System;

public class GuidedLauncher : AWeapon {
    [SerializeField] private LayerMask _tankLayer;
    [SerializeField] private LayerMask _groundLayer;

    protected override string WeaponName { get; } = "Guided Missile Launcher";

    public const float MaxTargetingDistance = 300; // units
    public const float TargetAcquisitionTime = 2f; // seconds
    public const float MaxTargetingAngle = 0.20f; // dot product, distance from 1 that is allowed

    public GameObject CurrentTarget { get; set; } = null;
    /// <summary>
    /// After successful target acquisition and firing of projectile, this saves the acquired target
    /// so the Owner of the missile can set its target after the server has spawned the missile.
    /// </summary>
    public NetworkObject CachedTarget { get; set; } = null;

    public static new Action<GuidedLauncher> Fired;

    protected override void Awake() {
        base.Awake();
    }

    public override void OnNetworkSpawn() {
        StateDictionary.Add(WeaponState.Deactivated, new GuidedLauncherDeactivated(this, WeaponState.Deactivated, IsOwner));
        StateDictionary.Add(WeaponState.Deactivating, new GuidedLauncherDeactivating(this, WeaponState.Deactivating, IsOwner));
        StateDictionary.Add(WeaponState.Activating, new GuidedLauncherActivating(this, WeaponState.Activating, IsOwner));
        StateDictionary.Add(WeaponState.Activated, new GuidedLauncherActivated(this, WeaponState.Activated, IsOwner));
        StateDictionary.Add(WeaponState.Firing, new GuidedLauncherFiring(this, WeaponState.Firing, IsOwner));
        StateDictionary.Add(WeaponState.Reloading, new GuidedLauncherReloading(this, WeaponState.Reloading, IsOwner));

        CurrentState = StateDictionary[WeaponState.Deactivated];

        // Calling this after adding states is important
        base.OnNetworkSpawn();
    }

    /// <summary>
    /// Called by a client to fire their own weapon.
    /// </summary>
    /// <param name="positionFired"></param>
    [Rpc(SendTo.Server)]
    public override void FireWeaponServerRpc(Vector3 positionFired) {
        // Fire projectile
        Quaternion projectileRotation = FireTransform.rotation;
        //if (TankGroundedComponent.IsGrounded()) projectileRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(FireTransform.forward, Vector3.up));

        //GameObject projectile = GameObject.Instantiate(CannonSM.ProjectilePrefab, CannonSM.FireTransform.position, projectileRotation);
        NetworkObject projectile = NetworkSpawnManager.Singleton.SpawnNetworkObject(ProjectilePrefab, positionFired, projectileRotation, OwnerClientId);
        AProjectile projectileComponent = projectile?.GetComponent<AProjectile>();
        TankTeamAssignment projectileTankTeamAssignment = projectile?.GetComponent<TankTeamAssignment>();
        projectileComponent?.SetWeaponInfo(Info);

        projectileTankTeamAssignment?.AssignTankTeam(TankTeamAssignmentComponent?.AssignedTankTeam);
        TankGameUtilities.IgnoreCollision(transform.root.gameObject, projectile?.gameObject);
        
        Debug.Log("Fired Guided Launcher Server RPC");
        FireWeaponClientRpc(projectile);
    }

    /// <summary>
    /// Called by the server in FireWeaponServerRpc() to tell clients that this weapon has been fired.
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    protected override void FireWeaponClientRpc(NetworkObjectReference projectileReference) {
        Debug.Log("Fired Guided Launcher Client RPC");
        if (Tank.FauxSuspensionComponent != null) {
            Tank.FauxSuspensionComponent.ApplyImpulse(-FireTransform.forward, 1f);
        }

        if (Tank.FauxAntennaComponent != null) {
            Tank.FauxAntennaComponent.ApplyImpulse(FireTransform.forward, 0.5f);
        }

        if (!IsOwner) return;

        NetworkObject projectileNO;
        if (projectileReference.TryGet(out projectileNO)) {
            GuidedLauncherMissile missile;
            if (projectileNO.TryGetComponent(out missile)) {
                missile.SetTarget(CachedTarget);
            }
        }

        CachedTarget = null;

        // TRIGGER SOUND EFFECT FOR THIS WEAPON
        //TriggerSoundEvent(TankAudioManager.SoundID.CannonFire);
    }

    public List<Transform> GetTanksInRange() {
        Collider[] tankColliders = Physics.OverlapSphere(transform.position, MaxTargetingDistance, _tankLayer.value);

        List<Transform> validTransforms = new List<Transform>();
        HashSet<Transform> registeredTransforms = new HashSet<Transform>();
        // Filter out any tanks on the same team
        for(int i = 0; i < tankColliders.Length; i++) {
            Collider collider = tankColliders[i];
            Transform colliderRoot = collider.transform.root;
            if (registeredTransforms.Contains(colliderRoot)) continue;

            GameObject tank = colliderRoot.gameObject;
            TankTeamAssignment tankTeamAssignment = tank.GetComponent<TankTeamAssignment>();
            if (tankTeamAssignment == null) continue;

            if(tankTeamAssignment.TankTeamID != Tank.TankTeamAssignmentComponent.TankTeamID) {
                validTransforms.Add(colliderRoot);
                registeredTransforms.Add(colliderRoot);
            }
        }

        return validTransforms;
    }

    /// <summary>
    /// Returns null if tankColliders had no entries.
    /// </summary>
    /// <param name="tankColliders"></param>
    /// <returns></returns>
    public Transform GetTankClosestToForward(List<Transform> tankRootTransforms) {
        if (tankRootTransforms.Count < 1) return null;
        Transform best = null;
        Vector3 vectorToGO;
        float bestDot = -1;
        for(int i = 0; i < tankRootTransforms.Count; i++) {
            Transform rootTransform = tankRootTransforms[i];
            // ignore if the tank hit is the same tank that this GuidedLauncher belongs to
            if (rootTransform == transform.root) continue;

            if (best == null) {
                best = rootTransform;
                vectorToGO = (best.position - transform.position).normalized;
                bestDot = Vector3.Dot(transform.forward, vectorToGO);
            }

            vectorToGO = (rootTransform.position - transform.position).normalized;
            float dot = Vector3.Dot(transform.forward, vectorToGO);
            if(dot < bestDot) {
                best = rootTransform;
                bestDot = dot;
            }
        }

        return best;
    }

    public bool TankIsInRange(Transform tankRootTransform) {
        Vector3 vectorToGO = tankRootTransform.position - transform.position;
        return vectorToGO.magnitude <= MaxTargetingDistance;
    }

    public bool TankIsInAngleRange(Transform tankRootTransform) {
        if (tankRootTransform == null) return false;
        Vector3 vectorToGO = (tankRootTransform.position - transform.position).normalized;
        float dot = Vector3.Dot(transform.forward, vectorToGO);
        if (1 - dot <= MaxTargetingAngle) return true;
        return false;
    }

    public bool TankIsInLOS(Transform tankRootTransform) {
        if (tankRootTransform == null) return false;

        Vector3 raycastPosition = transform.position;
        Vector3 raycastDirection = (tankRootTransform.position - raycastPosition);
        RaycastHit hitInfo;
        Ray ray = new Ray(raycastPosition, raycastDirection.normalized);
        if (Physics.Raycast(ray, out hitInfo, raycastDirection.magnitude, _tankLayer + _groundLayer)) {
            // if hit something other than the target, it is not in LOS
            if (hitInfo.collider.transform.root != tankRootTransform) return false;
        }

        return true;
    }

    public override void TriggerFired() {
        Fired?.Invoke(this);
    }
}