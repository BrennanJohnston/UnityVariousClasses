using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class GuidedLauncherActivated : AGuidedLauncherState {

    public GuidedLauncherActivated(AWeapon sm, AWeapon.WeaponState key, bool isOwner) : base(sm, key, isOwner) { }

    float targetingTimer = 0f;
    bool targetAcquired = false;

    public override void Enter() {
        WeaponSM.WeaponAnimator.Play("Activated");
        targetAcquired = false;
        GuidedLauncherSM.CurrentTarget = null;
        Debug.Log("Activated state");
    }

    public override void Exit() {
    }

    protected override void OwnerFixedUpdate() {
        base.OwnerFixedUpdate();

        if(GuidedLauncherSM.CurrentTarget == null) {
            targetAcquired = false;
            List<Transform> tanksInRange = GuidedLauncherSM.GetTanksInRange();
            if (tanksInRange == null) {
                return;
            }
            Transform closestTankRootTransform = GuidedLauncherSM.GetTankClosestToForward(tanksInRange);
            if (closestTankRootTransform == null) {
                return;
            }
            bool tankIsInAngleRange = GuidedLauncherSM.TankIsInAngleRange(closestTankRootTransform);
            if (!tankIsInAngleRange) {
                return;
            }

            bool tankIsInLOS = GuidedLauncherSM.TankIsInLOS(closestTankRootTransform);
            if(!tankIsInLOS) {
                Debug.Log("tank is not in LOS");
                return;
            }

            GuidedLauncherSM.CurrentTarget = closestTankRootTransform.gameObject;
            targetingTimer = GuidedLauncher.TargetAcquisitionTime;
        } else {
            bool tankIsInRange = GuidedLauncherSM.TankIsInRange(GuidedLauncherSM.CurrentTarget.transform.root);
            bool tankIsInAngleRange = GuidedLauncherSM.TankIsInAngleRange(GuidedLauncherSM.CurrentTarget.transform.root);
            bool tankIsInLOS = GuidedLauncherSM.TankIsInLOS(GuidedLauncherSM.CurrentTarget.transform.root);
            if(!tankIsInRange || !tankIsInAngleRange || !tankIsInLOS) {
                Debug.Log("lost acquired target");
                GuidedLauncherSM.CurrentTarget = null;
                targetAcquired = false;
                return;
            }

            targetingTimer -= Time.fixedDeltaTime;

            if(targetingTimer <= 0f) {
                targetAcquired = true;
            }
        }
    }

    public override AWeapon.WeaponState GetNextState() {
        AWeapon.WeaponState nextStateKey = StateKey;

        // Check if wants to Fire
        if(WeaponSM.TankControllerComponent != null && WeaponSM.TankControllerComponent.FireSecondary && targetAcquired) {
            nextStateKey = AWeapon.WeaponState.Firing;
            if(GuidedLauncherSM.CurrentTarget == null) {
                targetAcquired = false;
            } else {
                NetworkObject targetNO = GuidedLauncherSM.CurrentTarget.GetComponentInParent<NetworkObject>();
                if (targetNO == null) {
                    Debug.Log("Target did not have a NetworkObject component.");
                } else {
                    GuidedLauncherSM.CachedTarget = targetNO;
                }
            }
        }
        
        if(!WeaponSM.WantsToBeActivated) {
            nextStateKey = AWeapon.WeaponState.Deactivating;
        }


        return nextStateKey;
    }
}