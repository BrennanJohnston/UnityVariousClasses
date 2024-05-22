using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GuidedLauncherDeactivated : AGuidedLauncherState {
    public GuidedLauncherDeactivated(AWeapon sm, AWeapon.WeaponState key, bool isOwner) : base(sm, key, isOwner) { }

    public override void Enter() {
        Debug.Log("Deactivated state");
    }

    public override void Exit() {
    }
    
    public override AWeapon.WeaponState GetNextState() {
        AWeapon.WeaponState nextStateKey = StateKey;

        if(WeaponSM.WantsToBeActivated)
            nextStateKey = AWeapon.WeaponState.Activating;

        return nextStateKey;
    }
}