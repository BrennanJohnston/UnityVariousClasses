using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System;
using System.Net;

[RequireComponent(typeof(TankTeamAssignment))]
public class CTFFlag : AFlag {
    [SerializeField] private GameObject _flagHomeTransform;

    public CTFFlagStates FlagState { get; private set; } = CTFFlagStates.AtHome;
    public bool AtHome { get { return FlagState == CTFFlagStates.AtHome; } }

    public static Action<CTFFlag> FlagTakenFromHome;
    public static Action<CTFFlag> FlagPickedUp;
    public static Action<CTFFlag> FlagDropped;

    /// <summary>
    /// Flag that was captured, TankTeam that captured the flag.
    /// Parameters: CTFFlag that was captured, TankTeam that did the capture
    /// </summary>
    public static Action<CTFFlag, TankTeam> FlagCaptured;

    public enum CTFFlagStates {
        AtHome,
        Carried,
        Dropped
    }

    private TankTeamAssignment FlagTeamAssignmentComponent;

    protected override void Awake() {
        base.Awake();

        FlagTeamAssignmentComponent = GetComponent<TankTeamAssignment>();
    }

    protected override void OnCarryablePickedUp() {
        base.OnCarryablePickedUp();

        FlagState = CTFFlagStates.Carried;
    }

    protected override void OnCarryableDropped() {
        base.OnCarryableDropped();

        FlagState = CTFFlagStates.Dropped;
    }

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();
        if (!IsServer && !IsHost) return;

        // Detach from the FlagHome Transform
        transform.SetParent(null);
    }

    private void OnTriggerEnter(Collider collider) {
        if (!IsServer && !IsHost) return;

        GameObject collidedGO = collider.gameObject;
        Debug.Log("TriggerEnter occurred on CTFFlag");

        // Determine if a tank hit this flag
        TankStateMachine tank = collidedGO.GetComponentInParent<TankStateMachine>();
        if (tank == null) return;
        TankTeamAssignment tankTeamAssignment = tank.GetComponent<TankTeamAssignment>();

        // If this flag and the tank are on the same team, check to see if we can return this flag home
        if(tankTeamAssignment != null) {
            if (tankTeamAssignment.AssignedTankTeam.TeamID == FlagTeamAssignmentComponent.AssignedTankTeam.TeamID) {
                // This flag and the tank are part of the same team, is this flag at home?
                if (FlagState == CTFFlagStates.Dropped) {
                    ReturnHome();
                    return;
                }
            }
        }

        // A tank hit this flag, is that tank holding a flag?
        AFlag carriedFlag = tank.GetComponentInChildren<AFlag>();
        if(carriedFlag != null) {
            // The tank is carrying a flag

            if (tankTeamAssignment == null ||
                tankTeamAssignment.AssignedTankTeam.TeamID != FlagTeamAssignmentComponent.AssignedTankTeam.TeamID) return;

            // The tank and the flag that got hit are on the same team, is the flag at home?
            if (FlagState != CTFFlagStates.AtHome) return;

            // This flag is at home
            if (!(carriedFlag is CTFFlag)) return;

            // The flag being carried is a CTFFlag, capture it
            NetworkObjectCarrier carrier = tank.GetComponent<NetworkObjectCarrier>();
            if (carrier == null) return;

            NetworkObjectCarryable carriedFlagCarryable = carriedFlag.GetComponent<NetworkObjectCarryable>();
            if (carriedFlagCarryable == null) return;

            carrier.RemoveCarryable(carriedFlagCarryable, false);

            CTFFlag capturedFlag = (CTFFlag)carriedFlag;
            capturedFlag.ReturnHome();
            FlagCaptured?.Invoke(capturedFlag, tankTeamAssignment.AssignedTankTeam);
        } else {
            // The tank is not carrying a flag
            if (tankTeamAssignment == null || 
                tankTeamAssignment.AssignedTankTeam.TeamID == FlagTeamAssignmentComponent.AssignedTankTeam.TeamID) return;
            // The flag and the tank belong to different teams, so pick up flag
            NetworkObjectCarrier carrier = tank.GetComponent<NetworkObjectCarrier>();
            if (carrier == null) return;
            carrier.AttachCarryable(carryableComponent);
        }


        /*
        CTFFlag otherFlag = collidedGO.GetComponentInParent<CTFFlag>();
        if (otherFlag == null) return;

        TankTeamAssignment carrierTeamAssignment = carryableComponent.CurrentCarrier?.GetComponent<TankTeamAssignment>();
        TankTeamAssignment otherFlagTeamAssignment = otherFlag.GetComponent<TankTeamAssignment>();
        if (carrierTeamAssignment.AssignedTankTeam.TeamID != otherFlagTeamAssignment.AssignedTankTeam.TeamID) return;

        // Is otherFlag at home?
        if (otherFlag.FlagState != CTFFlagStates.AtHome) return;


        // Carrier team and team of flag that was collided with are the same, and other flag is AtHome, capture this flag
        // TODO: Remove Carryable from Carrier before returning home
        carryableComponent.CurrentCarrier?.RemoveCarryable(carryableComponent);
        ReturnHome();
        FlagCaptured?.Invoke(this, otherFlagTeamAssignment.AssignedTankTeam);
        TankStateMachine collidedTank = collidedGO.GetComponentInParent<TankStateMachine>();
        Debug.Log("here 1");
        if (collidedTank == null) return;
        Debug.Log("here 2");
        // A tank collided with this flag
        // Check to see what team they're on
        TankTeamAssignment tankTeamAssignmentComponent = collidedTank.GetComponent<TankTeamAssignment>();
        if (tankTeamAssignmentComponent == null) return;
        Debug.Log("here 3");
        if (tankTeamAssignmentComponent.AssignedTankTeam == FlagTeamAssignmentComponent.AssignedTankTeam) {
            // Tank that collided is on the team that the flag belongs to
            Debug.Log("here 4");
            // If flag is at home, do nothing
            if (AtHome) return;
            Debug.Log("here 5");
            // Flag is not at home, return flag to home
            ReturnHome();
        } else {
            // Tank that collided is on a different team than this flag belongs to
            NetworkObjectCarrier carrier = collidedTank.GetComponentInChildren<NetworkObjectCarrier>();
            Debug.Log("here 6");
            if (carrier == null) {
                Debug.Log("here 7");
                Debug.Log("Detected enemy tank collision with flag, but no NetworkObjectCarrier was found to attach to.");
                return;
            }

            carrier.AttachCarryable(carryableComponent);
        }
        */
    }

    /// <summary>
    /// Return this flag to it's home transform
    /// </summary>
    public void ReturnHome() {
        if (AtHome || (!IsServer && !IsHost)) return;

        Debug.Log("Flag was returned home.");
        transform.position = _flagHomeTransform.transform.position;
        FlagState = CTFFlagStates.AtHome;
    }
}