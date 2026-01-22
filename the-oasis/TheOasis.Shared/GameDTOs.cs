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
}