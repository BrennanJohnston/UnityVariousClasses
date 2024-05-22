using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ATankState : AState<TankStateMachine.TankState> {
    public TankStateMachine TankSM { get; private set; }

    public ATankState(TankStateMachine sm, TankStateMachine.TankState key, bool isOwner) : base(sm, key, isOwner) {
        TankSM = sm;
    }
}