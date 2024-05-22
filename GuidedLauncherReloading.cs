using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GuidedLauncherReloading : AGuidedLauncherState {

    public GuidedLauncherReloading(AWeapon sm, AWeapon.WeaponState key, bool isOwner) : base(sm, key, isOwner) { }

    float reloadTime = 1f;
    float reloadTimer;

    public override void Enter() {
        reloadTimer = reloadTime;
        Debug.Log("Reloading state");
    }

    public override void Exit() { }

    public override AWeapon.WeaponState GetNextState() {
        AWeapon.WeaponState nextStateKey = StateKey;

        if (reloadTimer <= 0f)
            nextStateKey = AWeapon.WeaponState.Activated;

        return nextStateKey;
    }

    public override void Update() {
        reloadTimer -= Time.deltaTime;
    }
}