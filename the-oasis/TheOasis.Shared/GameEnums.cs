namespace TheOasis.Shared;

public enum GamePhase
{
    IdentityReveal,     // Looking at roles
    TeamSelection,      // Leader picking players
    Voting,             // Everyone voting on the team
    MissionExecution,   // Team members choosing cards
    Assassination,      // Assassin chooses a target
    GameOver            // Evil/Good won
}

public enum MissionCard
{
    Success,
    Failure,
    Reverse
}