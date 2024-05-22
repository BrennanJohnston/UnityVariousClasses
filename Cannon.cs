using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System;

public class Cannon : AWeapon {

    protected override string WeaponName { get; } = "Cannon";

    public static new Action<Cannon> Fired;

    protected override void Awake() {
        base.Awake();
    }

    public override void OnNetworkSpawn() {
        StateDictionary.Add(WeaponState.Deactivated, new CannonDeactivated(this, WeaponState.Deactivated, IsOwner));
        StateDictionary.Add(WeaponState.Activating, new CannonActivating(this, WeaponState.Activating, IsOwner));
        StateDictionary.Add(WeaponState.Activated, new CannonActivated(this, WeaponState.Activated, IsOwner));
        StateDictionary.Add(WeaponState.Firing, new CannonFiring(this, WeaponState.Firing, IsOwner));
        StateDictionary.Add(WeaponState.Reloading, new CannonReloading(this, WeaponState.Reloading, IsOwner));

        CurrentState = StateDictionary[WeaponState.Deactivated];

        base.OnNetworkSpawn();
    }

    /// <summary>
    /// Called by a client to fire their own weapon.
    /// </summary>
    /// <param name="positionFired"></param>
    [Rpc(SendTo.Server)]
    public override void FireWeaponServerRpc(Vector3 positionFired) {
        Debug.Log("Fire weapon server rpc called in Cannon");
        // Fire projectile
        Quaternion projectileRotation = FireTransform.rotation;
        if (TankGroundedComponent.IsGrounded()) projectileRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(FireTransform.forward, Vector3.up));
        //GameObject projectile = GameObject.Instantiate(CannonSM.ProjectilePrefab, CannonSM.FireTransform.position, projectileRotation);
        NetworkObject projectile = NetworkSpawnManager.Singleton.SpawnNetworkObject(ProjectilePrefab, positionFired/*FireTransform.position*/, projectileRotation/*, OwnerClientId*/);
        AProjectile projectileComponent = projectile?.GetComponent<AProjectile>();
        TankTeamAssignment projectileTankTeamAssignment = projectile?.GetComponent<TankTeamAssignment>();
        projectileComponent?.SetWeaponInfo(Info);

        projectileTankTeamAssignment?.AssignTankTeam(TankTeamAssignmentComponent?.AssignedTankTeam);
        TankGameUtilities.IgnoreCollision(gameObject, projectile?.gameObject);

        FireWeaponClientRpc(projectile);
    }

    /// <summary>
    /// Called by the server in FireWeaponServerRpc() to tell clients that this weapon has been fired.
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    protected override void FireWeaponClientRpc(NetworkObjectReference projectileReference) {

        if (Tank.FauxSuspensionComponent != null) {
            Tank.FauxSuspensionComponent.ApplyImpulse(-FireTransform.forward, 1f);
        }

        if(Tank.FauxAntennaComponent != null) {
            Tank.FauxAntennaComponent.ApplyImpulse(FireTransform.forward, 0.5f);
        }
    }

    public override void TriggerFired() {
        Fired?.Invoke(this);
    }
}