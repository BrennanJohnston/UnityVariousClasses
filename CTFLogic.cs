using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CTFLogic : AMatchLogic {
    public override void Initialize(MapConfig.MapConfigurationData mapConfig) {
        base.Initialize(mapConfig);
    }

    private void OnTankTeamScoreChanged(TankTeam team) {
        Debug.Log("TDMLogic.OnTankTeamScoreChanged(): Score changed on team " + team.ToString() + " new score is " + team.Score);

        if (team.Score >= ScoreLimit) {
            EndMatch();
        }
    }

    private void OnFlagCaptured(CTFFlag capturedFlag, TankTeam teamThatCaptured) {
        teamThatCaptured.IncreaseScore(1);
    }

    public override void EndMatch() {
        base.EndMatch();
        Debug.Log("CTFLogic.EndMatch() called");
    }

    protected override void SubscribeToEvents() {
        base.SubscribeToEvents();

        CTFFlag.FlagCaptured += OnFlagCaptured;
        TankTeam.ScoreChanged += OnTankTeamScoreChanged;
    }

    protected override void UnsubscribeFromEvents() {
        base.UnsubscribeFromEvents();

        CTFFlag.FlagCaptured -= OnFlagCaptured;
        TankTeam.ScoreChanged -= OnTankTeamScoreChanged;
    }
}