namespace TheOasis.Shared;

public enum GamePhase
{
    IdentityReveal,     // Looking at roles
    TeamSelection,      // Leader picking players
    Voting,             // Everyone voting on the team
    MissionExecution,   // Selected team choosing Success/Fail/Reverse
    GameOver            // Evil/Good won
}