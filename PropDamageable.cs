using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System;

public class PropDamageable : ADamageable {
    [SerializeField] private float _healthMaxHP;
    [Tooltip("If true, this component will handle ANetworkDie/Despawn call when HP reaches empty. " +
        "Set this to false if some other component is going to handle HP <= 0 event.")]

    private AWeapon.WeaponInfo mostRecentWeaponInfo;
    private float currentHP;

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();

        currentHP = _healthMaxHP;
        if (currentHP <= 0f) HPReachedEmpty?.Invoke();
    }

    public override AWeapon.WeaponInfo GetMostRecentWeaponInfo() {
        return mostRecentWeaponInfo;
    }

    public void ResetHP() {
        currentHP = _healthMaxHP;
    }

    public override float TakeDamage(float damageAmount, AWeapon.WeaponInfo weaponInfo) {
        if (!IsServer && !IsHost) return 0f;

        bool hadHPToStart = currentHP > 0f;

        if(weaponInfo != null) mostRecentWeaponInfo = weaponInfo;

        damageAmount = Mathf.Max(damageAmount, 0f);
        currentHP -= damageAmount;

        if(currentHP <= 0f) {
            float rollover = Mathf.Abs(currentHP);

            if(hadHPToStart)
                HPReachedEmpty?.Invoke();

            return rollover;
        } else if (damageAmount > 0f) DamageTaken?.Invoke();

        return 0f;
    }
}