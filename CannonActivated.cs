using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CannonActivated : ACannonState {
    public CannonActivated(AWeapon sm, AWeapon.WeaponState key, bool isOwner) : base(sm, key, isOwner) { }

    public override void Enter() { }

    public override void Exit() { }

    public override AWeapon.WeaponState GetNextState() {
        AWeapon.WeaponState nextStateKey = StateKey;

        if (WeaponSM.TankControllerComponent != null && WeaponSM.TankControllerComponent.FirePrimary) {
            nextStateKey = AWeapon.WeaponState.Firing;
        }

        return nextStateKey;
    }
}