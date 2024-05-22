using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class TDMLogic : AMatchLogic {
    public override void Initialize(MapConfig.MapConfigurationData mapConfig) {
        base.Initialize(mapConfig);
    }

    private void OnTankTeamScoreChanged(TankTeam team) {
        Debug.Log("TDMLogic.OnTankTeamScoreChanged(): Score changed on team " + team.ToString() + " new score is " + team.Score);

        if(team.Score >= ScoreLimit) {
            EndMatch();
        }
    }

    protected override void OnTankPlayerDied(ATankPlayer tankPlayer, DeathInfo deathInfo) {
        base.OnTankPlayerDied(tankPlayer, deathInfo);

        if (CurrentState != MatchState.InProgress) return;

        // iterate score for team associated with killer
        if(deathInfo.WeaponUsedInfo?.TankPlayerOwner != null) {
            TankTeam killerTeam = TeamManager.GetTeamByTankPlayerID(deathInfo.WeaponUsedInfo.TankPlayerOwner.TankPlayerID);
            if(killerTeam != null) {
                killerTeam.IncreaseScore(1);
            }
        }
    }

    public override void EndMatch() {
        base.EndMatch();
        Debug.Log("TDMLogic.EndMatch() called");
    }

    protected override void SubscribeToEvents() {
        base.SubscribeToEvents();

        TankTeam.ScoreChanged += OnTankTeamScoreChanged;
    }

    protected override void UnsubscribeFromEvents() {
        base.UnsubscribeFromEvents();

        TankTeam.ScoreChanged -= OnTankTeamScoreChanged;
    }
}