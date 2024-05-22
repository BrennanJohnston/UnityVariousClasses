using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GuidedLauncherFiring : AGuidedLauncherState {

    public GuidedLauncherFiring(AWeapon sm, AWeapon.WeaponState key, bool isOwner) : base(sm, key, isOwner) { }

    public override void Enter() {
        // Fire weapon
        if (WeaponSM.IsOwner) WeaponSM.FireWeaponServerRpc(WeaponSM.FireTransform.position);
    }

    public override void Exit() {
        GuidedLauncherSM.TriggerFired();
    }

    public override AWeapon.WeaponState GetNextState() {
        return AWeapon.WeaponState.Reloading;
    }
}