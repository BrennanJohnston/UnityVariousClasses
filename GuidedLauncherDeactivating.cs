using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GuidedLauncherDeactivating : AGuidedLauncherState {

    public GuidedLauncherDeactivating(AWeapon sm, AWeapon.WeaponState key, bool isOwner) : base(sm, key, isOwner) { }

    public override void Enter() {
        WeaponSM.WeaponAnimator.Play("Deactivating");
        Debug.Log("Deactivating state");
    }

    public override void Exit() {

    }

    public override AWeapon.WeaponState GetNextState() {
        AWeapon.WeaponState nextStateKey = StateKey;

        AnimatorStateInfo stateInfo = WeaponSM.WeaponAnimator.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.normalizedTime >= 1f) nextStateKey = AWeapon.WeaponState.Deactivated;

        return nextStateKey;
    }
}