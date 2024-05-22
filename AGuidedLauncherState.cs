using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class AGuidedLauncherState : AWeaponState {

    public GuidedLauncher GuidedLauncherSM { get; private set; }

    public AGuidedLauncherState(AWeapon sm, AWeapon.WeaponState key, bool isOwner) : base(sm, key, isOwner) {
        GuidedLauncherSM = (GuidedLauncher)sm;
    }
}