using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public abstract class ADamageable : NetworkBehaviour {
    [SerializeField] protected bool _dieOnHPEmpty = false;

    protected virtual void Awake() {
        HPReachedEmpty += OnHPReachedEmpty;
        DamageTaken += OnDamageTaken;
    }

    public override void OnDestroy() {
        HPReachedEmpty -= OnHPReachedEmpty;
        DamageTaken -= OnDamageTaken;
    }

    protected virtual void OnHPReachedEmpty() {
        if (_dieOnHPEmpty) {
            if(TryGetComponent(out ANetworkDie die)) {
                die.NetworkDie(new DeathInfo());
            } else {
                NetworkObject.Despawn();
            }
        }
    }

    protected virtual void OnDamageTaken() {  }

    public Action HPReachedEmpty;
    public Action DamageTaken;
    public abstract AWeapon.WeaponInfo GetMostRecentWeaponInfo();
    /// <summary>
    /// Interface function whose implementation is meant to negatively effect some form of HP.
    /// </summary>
    /// <param name="damageAmount"></param>
    /// <returns>Float</returns>
    public abstract float TakeDamage(float damageAmount, AWeapon.WeaponInfo weaponInfo);
}
