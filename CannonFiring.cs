using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CannonFiring : ACannonState {
    public CannonFiring(AWeapon sm, AWeapon.WeaponState key, bool isOwner) : base(sm, key, isOwner) { }

    bool hasFired = false;

    public override void Enter() {
        Debug.Log("WeaponSM.IsOwner?: " + (WeaponSM.IsOwner));
        if (WeaponSM.IsOwner) WeaponSM.FireWeaponServerRpc(WeaponSM.FireTransform.position);

        hasFired = true;
    }

    public override void Exit() {
        CannonSM.TriggerFired();
    }

    public override AWeapon.WeaponState GetNextState() {
        AWeapon.WeaponState nextStateKey = StateKey;

        if (hasFired)
            nextStateKey = AWeapon.WeaponState.Reloading;

        return nextStateKey;
    }
}