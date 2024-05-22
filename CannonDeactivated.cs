using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CannonDeactivated : ACannonState {
    public CannonDeactivated(AWeapon sm, AWeapon.WeaponState key, bool isOwner) : base(sm, key, isOwner) { }

    public override void Enter() {
    }

    public override void Exit() {
    }

    public override AWeapon.WeaponState GetNextState() {
        AWeapon.WeaponState nextStateKey = StateKey;

        nextStateKey = AWeapon.WeaponState.Activating;

        return nextStateKey;
    }
}