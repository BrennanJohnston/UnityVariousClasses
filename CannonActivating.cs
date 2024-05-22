using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CannonActivating : ACannonState {
    public CannonActivating(AWeapon sm, AWeapon.WeaponState key, bool isOwner) : base(sm, key, isOwner) { }

    float readyTime = 1.5f;
    float readyTimer;

    public override void Enter() {
        readyTimer = readyTime;
    }

    public override void Exit() {
    }

    public override AWeapon.WeaponState GetNextState() {
        AWeapon.WeaponState nextStateKey = StateKey;

        if(readyTimer <= 0f)
            nextStateKey = AWeapon.WeaponState.Activated;

        return nextStateKey;
    }

    public override void Update() {
        base.Update();

        readyTimer -= Time.deltaTime;
    }
}