namespace TheOasis.Shared;

public class CreateGameRequest
{
    public required string HostName { get; set; }
}

public class JoinGameRequest
{
    public required string GameCode { get; set; }
    public required string PlayerName { get; set; }
}

public class GameSessionDto
{
    public required string GameCode { get; set; }
    public List<string> Players { get; set; } = new();
    public bool IsStarted { get; set; }
    public List<RoleType> SelectedRoles { get; set; } = new();

    // Server-side state tracking
    public HashSet<string> ReadyPlayers { get; set; } = new();
    public int LeaderIndex { get; set; } = 0;
    public int VoteTrack { get; set; } = 0;
    public int MissionIndex { get; set; } = 0; // 0 to 4
    public GamePhase CurrentPhase { get; set; } = GamePhase.IdentityReveal;
    public List<string> CurrentProposal { get; set; } = new();
    public Dictionary<string, bool> CurrentVotes { get; set; } = new();
    public List<HistoryEntryDto> History { get; set; } = new();
}

public class GameStateDto
{
    public GamePhase Phase { get; set; }
    public List<string> AllPlayers { get; set; } = new();
    public List<HistoryEntryDto> GameHistory { get; set; } = new();

    // Who is the current leader (Name)
    public string LeaderName { get; set; } = "";

    // Mission Info
    public int CurrentMissionNumber { get; set; } // 1-5
    public int RequiredTeamSize { get; set; }

    // Vote Track (0-5). If hits 5, Evil wins.
    public int FailedVotesCount { get; set; }

    // For TeamSelection Phase: currently selected players by leader
    public List<string> ProposedTeam { get; set; } = new();

    // For Voting Phase: Status of votes (only showed after everyone voted)
    // Key: PlayerName, Value: True(Approve)/False(Reject)
    public Dictionary<string, bool>? LastVoteResults { get; set; }

    // Message to display (e.g. "Vote Failed", "Team Approved")
    public string SystemMessage { get; set; } = "";
}

public static class MissionRules
{
    // Key: Total Players. Value: Array of team sizes for mission 1,2,3,4,5
    public static readonly Dictionary<int, int[]> TeamSizes = new()
        {
            { 5, new[] { 2, 3, 2, 3, 3 } },
            { 6, new[] { 2, 3, 4, 3, 4 } },
            { 7, new[] { 2, 3, 3, 4, 4 } },
            { 8, new[] { 3, 4, 4, 5, 5 } },
            { 9, new[] { 3, 4, 4, 5, 5 } },
            { 10, new[] { 3, 4, 4, 5, 5 } }
        };
}

public class HistoryEntryDto
{
    public int MissionNumber { get; set; }
    public int AttemptNumber { get; set; } // e.g., 1 to 5 (Vote Track)
    public string LeaderName { get; set; } = "";

    // Who was proposed for the mission
    public List<string> ProposedTeam { get; set; } = new();

    // How everyone voted (Key: PlayerName, Value: Approved?)
    public Dictionary<string, bool> Votes { get; set; } = new();

    // Result of the proposal vote
    public bool WasApproved { get; set; }

    // Result of the ACTUAL mission (Success/Fail) - Null if vote was rejected
    public string? MissionOutcome { get; set; }
}