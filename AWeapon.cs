using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System;

public abstract class AWeapon : AStateMachine<AWeapon.WeaponState> {
    [SerializeField] public Transform FireTransform;
    [SerializeField] public GameObject ProjectilePrefab;

    /// <summary>
    /// A data class that keeps track of information regarding a weapon.
    /// </summary>
    public class WeaponInfo {
        public string WeaponName { get; private set; }
        public System.Type WeaponType { get; private set; }
        public ATankPlayer TankPlayerOwner { get; private set; }

        internal WeaponInfo(string weaponName, System.Type weaponType, ATankPlayer tankPlayerOwner) {
            WeaponName = weaponName;
            WeaponType = weaponType;
            TankPlayerOwner = tankPlayerOwner;
        }

        public override string ToString() {
            string str = $"WeaponInfo( WeaponName: {WeaponName} ------ System.Type: {WeaponType} ------ NetworkPlayerOwner: {TankPlayerOwner} )";
            return str;
        }
    }

    public ATankController TankControllerComponent { get; private set; }
    public IIsGrounded TankGroundedComponent { get; private set; }
    //public FauxSuspension FauxSuspension { get; private set; }
    public TankStateMachine Tank { get; private set; }
    protected abstract string WeaponName { get; }
    public TankNetworkPlayer TankPlayerOwner { get; private set; }
    public TankTeamAssignment TankTeamAssignmentComponent { get; private set; }
    public Animator WeaponAnimator { get; private set; }
    public WeaponInfo Info { get; private set; }
    public NetworkObjectCarryable WeaponCarryable { get; private set; }
    public WeaponState CurrentWeaponStateKey { get { return (CurrentState == null) ? WeaponState.None : CurrentState.StateKey; } }

    /// <summary>
    /// Bool used by AWeaponState to determine if this AWeapon wants to activate. This is not an indicator of actual weapon state.
    /// Modified via ActivateWeapon()
    /// </summary>
    public bool WantsToBeActivated { get; private set; }

    public static Action<AWeapon> Fired;

    public enum WeaponState {
        None,
        Deactivated,
        Activating,
        Activated,
        Firing,
        Reloading,
        Deactivating
    }

    protected override void Awake() {
        base.Awake();

        ATankController.AttachedToGameObject += OnTankControllerAttached;

        UpdateTankComponents();
        WeaponAnimator = GetComponentInParent<Animator>();
        WeaponCarryable = GetComponentInParent<NetworkObjectCarryable>();
    }

    public override void OnDestroy() {
        base.OnDestroy();

        ATankController.AttachedToGameObject -= OnTankControllerAttached;
    }

    /// <summary>
    /// Call the base if you override this function.
    /// </summary>
    public override void OnNetworkSpawn() {
        //TankControllerComponent = GetComponent<ATankController>();

        //TankPlayerOwner = TankRelay.Singleton.GetTankPlayer(OwnerClientId);

        Info = GenerateWeaponInfo();
        base.OnNetworkSpawn();
    }

    public override void OnNetworkDespawn() {
        base.OnNetworkDespawn();
    }

    /// <summary>
    /// <para>Functional on: Owner</para>
    /// </summary>
    /// <param name="activate"></param>
    public void ActivateWeapon(bool activate) {
        if (!IsOwner) return;
        WantsToBeActivated = activate;
        Debug.Log("Weapon activation boolean set to " + activate);
    }

    public abstract void TriggerFired();

    private void UpdateTankComponents() {
        TankGroundedComponent = GetComponentInParent<IIsGrounded>();
        Tank = GetComponentInParent<TankStateMachine>();
        TankTeamAssignmentComponent = GetComponentInParent<TankTeamAssignment>();
    }

    public void OnTankControllerAttached(GameObject gObject) {
        if (!transform.IsChildOf(gObject.transform)) return;

        TankControllerComponent = GetComponentInParent<ATankController>();
    }

    public override void OnNetworkObjectParentChanged(NetworkObject parentNetworkObject) {
        base.OnNetworkObjectParentChanged(parentNetworkObject);

        UpdateTankComponents();
        TankControllerComponent = GetComponentInParent<ATankController>();
    }

    /// <summary>
    /// DON'T call this base if you override this function, causes a stack overflow.
    /// </summary>
    /// <param name="positionFired"></param>
    [Rpc(SendTo.Server)]
    public virtual void FireWeaponServerRpc(Vector3 positionFired) {
        Debug.Log("AWeapon.FireWeaponServerRpc() called.");
        FireWeaponClientRpc(new NetworkObjectReference());
    }

    /// <summary>
    /// DON'T call this base if you override this function, causes a stack overflow.
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    protected virtual void FireWeaponClientRpc(NetworkObjectReference projectileReference) { }

    /// <summary>
    /// Generate a WeaponInfo for this weapon.  Only call after TankPlayerOwner has been assigned.
    /// </summary>
    /// <returns></returns>
    private WeaponInfo GenerateWeaponInfo() {
        if(TankPlayerOwner == null) {
            TankPlayerOwner = TankRelay.Singleton.GetTankPlayer(OwnerClientId);
        }
        return new WeaponInfo(WeaponName, GetType(), TankPlayerOwner);
    }
}