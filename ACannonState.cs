using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ACannonState : AWeaponState {
    public Cannon CannonSM { get; private set; }

    public ACannonState(AWeapon sm, AWeapon.WeaponState key, bool isOwner) : base(sm, key, isOwner) {
        CannonSM = (Cannon)sm;
    }
}