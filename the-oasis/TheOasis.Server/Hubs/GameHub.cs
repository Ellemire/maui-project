using Microsoft.AspNetCore.SignalR;
using Microsoft.Win32;
using System.Collections.Concurrent;
using TheOasis.Shared;

namespace TheOasis.Server.Hubs;


public class GameManager
{
    public ConcurrentDictionary<string, GameSessionDto> Games { get; } = new();

    // Mapping: (GameCode, PlayerName) -> ConnectionId
    // This allows us to send private messages to specific players within a specific game context
    private readonly ConcurrentDictionary<(string GameCode, string PlayerName), string> _playerConnections = new();

    public void RegisterPlayer(string gameCode, string playerName, string connectionId)
    {
        var key = (gameCode, playerName);
        _playerConnections.AddOrUpdate(key, connectionId, (k, oldId) => connectionId);
    }

    public void RemovePlayer(string gameCode, string playerName)
    {
        var key = (gameCode, playerName);
        _playerConnections.TryRemove(key, out _);
    }

    public string? GetConnectionId(string gameCode, string playerName)
    {
        _playerConnections.TryGetValue((gameCode, playerName), out var connectionId);
        return connectionId;
    }
}

public class GameHub : Hub
{
    private readonly GameManager _gameManager;

    public GameHub(GameManager gameManager)
    {
        _gameManager = gameManager;
    }

    public async Task<GameSessionDto> CreateGame(string hostName)
    {
        string code;
        bool added = false;
        GameSessionDto session;

        // Loop to ensure the code is unique
        do
        {
            // Generate 6-digit code (100000 to 999999)
            code = Random.Shared.Next(100000, 999999).ToString();

            session = new GameSessionDto { GameCode = code };
            session.Players.Add(hostName);

            // Register Host's connection ID
            _gameManager.RegisterPlayer(code, hostName, Context.ConnectionId);

            // TryAdd returns false if the key already exists
            added = _gameManager.Games.TryAdd(code, session);

        } while (!added); // Keep trying until we find a free code

        await Groups.AddToGroupAsync(Context.ConnectionId, code);
        return session;
    }

    public async Task<GameSessionDto> JoinGame(string gameCode, string playerName)
    {
        if (_gameManager.Games.TryGetValue(gameCode, out var session))
        {
            if (session.Players.Contains(playerName))
                throw new HubException("Nickname already taken in this game.");

            session.Players.Add(playerName);

            // Register Player's connection ID
            _gameManager.RegisterPlayer(gameCode, playerName, Context.ConnectionId);

            await Groups.AddToGroupAsync(Context.ConnectionId, gameCode);

            // Notify others in the lobby
            await Clients.Group(gameCode).SendAsync("PlayerJoined", playerName);

            return session;
        }
        throw new HubException("Game not found.");
    }

    public async Task LeaveGame(string gameCode, string playerName)
    {
        if (_gameManager.Games.TryGetValue(gameCode, out var session))
        {
            session.Players.Remove(playerName);
            _gameManager.RemovePlayer(gameCode, playerName); // Clean up connection mapping

            // Remove from SignalR group so they don't receive updates anymore
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, gameCode);

            // Notify others that someone left
            await Clients.Group(gameCode).SendAsync("PlayerLeft", playerName);

            // Clean up: If lobby is empty, remove the game
            if (session.Players.Count == 0)
            {
                _gameManager.Games.TryRemove(gameCode, out _);
            }
        }
    }

    public async Task UpdateGameSettings(string gameCode, List<RoleType> selectedRoles)
    {
        if (_gameManager.Games.TryGetValue(gameCode, out var session))
        {
            session.SelectedRoles = selectedRoles;

            // Notify everyone in lobby that settings changed (optional, for now we trust Host)
            await Clients.Group(gameCode).SendAsync("GameSettingsChanged", selectedRoles);
        }
    }

    public async Task StartGame(string gameCode)
    {
        if (_gameManager.Games.TryGetValue(gameCode, out var session))
        {
            int playerCount = session.Players.Count;
            int roleCount = session.SelectedRoles.Count;

            if (playerCount < 5 || playerCount > 10)
                throw new HubException("Player count must be between 5 and 10.");

            if (playerCount != roleCount)
                throw new HubException($"Role count ({roleCount}) does not match player count ({playerCount}).");

            // 1. Shuffle Roles
            var rng = new Random();
            var shuffledRoles = session.SelectedRoles.OrderBy(x => rng.Next()).ToList();

            // 2. Map Player -> Role
            var playerRoleMap = new Dictionary<string, RoleType>();
            for (int i = 0; i < playerCount; i++)
            {
                playerRoleMap[session.Players[i]] = shuffledRoles[i];
            }

            // 3. Send distinct message to each player
            foreach (var player in session.Players)
            {
                var roleType = playerRoleMap[player];
                var roleDef = GameRules.AllRoles.FirstOrDefault(r => r.Type == roleType)
                              ?? new RoleDefinition { Name = "Unknown", Description = "Generic" };

                var dto = new PlayerRoleDto
                {
                    Role = roleType,
                    RoleName = roleDef.Name,
                    Faction = roleDef.Faction,
                    Description = roleDef.Description
                };

                // --- LOGIC FOR REVEALING INFO (The "Night" Phase) ---

                // Logic: High Priestess sees Evil (except Envious Drover)
                if (roleType == RoleType.HighPriestess)
                {
                    var evils = playerRoleMap
                       .Where(kvp =>
                           (GameRules.AllRoles.First(r => r.Type == kvp.Value).Faction == Faction.DesertNomads) &&
                           kvp.Value != RoleType.EnviousDrover)
                       .Select(kvp => kvp.Key).ToList();
                    dto.KnownInformation.Add($"Nomads detected: {string.Join(", ", evils)}");
                }

                // Logic: Nomads see each other (except Lone Nomad and Oberon rule)
                if (roleDef.Faction == Faction.DesertNomads && roleType != RoleType.LoneNomad)
                {
                    var otherEvils = playerRoleMap
                       .Where(kvp =>
                           GameRules.AllRoles.First(r => r.Type == kvp.Value).Faction == Faction.DesertNomads &&
                           kvp.Value != RoleType.LoneNomad &&
                           kvp.Key != player)
                       .Select(kvp => kvp.Key).ToList();
                    dto.KnownInformation.Add($"Fellow Nomads: {string.Join(", ", otherEvils)}");
                }

                // Logic: Guard sees Priestess (and Witch)
                if (roleType == RoleType.TaSetiGuard)
                {
                    var magicUsers = playerRoleMap
                       .Where(kvp => kvp.Value == RoleType.HighPriestess || kvp.Value == RoleType.Witch)
                       .Select(kvp => kvp.Key)
                       .OrderBy(x => rng.Next()).ToList();
                    dto.KnownInformation.Add($"Possible Priestesses: {string.Join(", ", magicUsers)}");
                }
                // --------------------------------------------------------

                // Send to specific ConnectionId
                var connectionId = _gameManager.GetConnectionId(gameCode, player);
                if (!string.IsNullOrEmpty(connectionId))
                {
                    await Clients.Client(connectionId).SendAsync("ReceiveRole", dto);
                }
            }

            session.IsStarted = true;
            //await Clients.Group(gameCode).SendAsync("GameStarted");
        }
    }

    // 1. Player clicks "Proceed to Mission"
    public async Task PlayerReadyForMission(string gameCode, string playerName)
    {
        if (_gameManager.Games.TryGetValue(gameCode, out var session))
        {
            session.ReadyPlayers.Add(playerName);

            // If everyone is ready, start the game loop
            if (session.ReadyPlayers.Count == session.Players.Count)
            {
                // Init Game
                session.CurrentPhase = GamePhase.TeamSelection;
                session.MissionIndex = 0;
                session.VoteTrack = 0;
                session.LeaderIndex = new Random().Next(session.Players.Count); // Random leader start

                await BroadcastGameState(gameCode, session, "Game Started! First Leader selected.");
            }
        }
    }

    // 2. Leader proposes a team
    public async Task ProposeTeam(string gameCode, List<string> selectedPlayers)
    {
        if (_gameManager.Games.TryGetValue(gameCode, out var session))
        {
            // Validate: Is it this player's turn? (Skipped for brevity, rely on UI)
            // Validate: Count matches mission rules?
            int required = MissionRules.TeamSizes[session.Players.Count][session.MissionIndex];
            if (selectedPlayers.Count != required)
                throw new HubException($"Select exactly {required} players.");

            session.CurrentProposal = selectedPlayers;
            session.CurrentPhase = GamePhase.Voting;
            session.CurrentVotes.Clear(); // Reset votes

            await BroadcastGameState(gameCode, session, "Team proposed. Please Vote!");
        }
    }

    // 3. Player votes
    public async Task VoteForTeam(string gameCode, string playerName, bool approve)
    {
        if (_gameManager.Games.TryGetValue(gameCode, out var session))
        {
            session.CurrentVotes[playerName] = approve;

            // Check if everyone voted
            if (session.CurrentVotes.Count == session.Players.Count)
            {
                // Tally votes
                int yesVotes = session.CurrentVotes.Values.Count(v => v);
                int noVotes = session.CurrentVotes.Values.Count(v => !v);

                bool isApproved = yesVotes > noVotes; // Strict majority usually required

                var historyEntry = new HistoryEntryDto
                {
                    MissionNumber = session.MissionIndex + 1,
                    AttemptNumber = session.VoteTrack + 1, // 1st attempt is index 0
                    LeaderName = session.Players[session.LeaderIndex],
                    ProposedTeam = new List<string>(session.CurrentProposal),
                    Votes = new Dictionary<string, bool>(session.CurrentVotes),
                    WasApproved = isApproved,
                    MissionOutcome = null // Will be updated later if mission executes
                };
                session.History.Add(historyEntry);

                // Prepare results to show everyone
                var voteResults = new Dictionary<string, bool>(session.CurrentVotes);

                if (isApproved)
                {
                    // Team Approved -> Go to Mission Execution
                    session.CurrentPhase = GamePhase.MissionExecution;
                    session.VoteTrack = 0; // Reset track on success
                    await BroadcastGameState(gameCode, session, "Team Approved! Proceeding to Mission.", voteResults);
                }
                else
                {
                    // Team Rejected
                    session.VoteTrack++;
                    session.CurrentPhase = GamePhase.TeamSelection;
                    session.LeaderIndex = (session.LeaderIndex + 1) % session.Players.Count; // Next leader
                    session.CurrentProposal.Clear();

                    string msg = "Vote Failed.";

                    // Check Evil Win Condition (5 fails)
                    if (session.VoteTrack >= 5)
                    {
                        session.CurrentPhase = GamePhase.GameOver;
                        msg = "EVIL WINS! 5 Failed Votes.";
                    }

                    await BroadcastGameState(gameCode, session, msg, voteResults);
                }
            }
        }
    }

    // Helper to send the GameStateDto to everyone
    private async Task BroadcastGameState(string gameCode, GameSessionDto session, string systemMsg, Dictionary<string, bool>? lastVotes = null)
    {
        int required = MissionRules.TeamSizes[session.Players.Count][session.MissionIndex];

        var state = new GameStateDto
        {
            Phase = session.CurrentPhase,
            AllPlayers = new List<string>(session.Players),
            LeaderName = session.Players[session.LeaderIndex],
            CurrentMissionNumber = session.MissionIndex + 1,
            RequiredTeamSize = required,
            FailedVotesCount = session.VoteTrack,
            ProposedTeam = session.CurrentProposal,
            LastVoteResults = lastVotes,
            SystemMessage = systemMsg,
            GameHistory = session.History
        };

        await Clients.Group(gameCode).SendAsync("UpdateGameState", state);
    }
}