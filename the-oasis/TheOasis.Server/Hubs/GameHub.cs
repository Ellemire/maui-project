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
            session.PlayerRoles = playerRoleMap;

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

    // 4. Team member submits a card
    public async Task SubmitMissionCard(string gameCode, string playerName, MissionCard card)
    {
        if (_gameManager.Games.TryGetValue(gameCode, out var session))
        {
            // Validation 1: Phase
            if (session.CurrentPhase != GamePhase.MissionExecution) return;

            // Validation 2: Is player on the team?
            if (!session.CurrentProposal.Contains(playerName))
                throw new HubException("You are not on the mission team.");

            // Validation 3: Role Restrictions
            var role = session.PlayerRoles[playerName];
            var roleDef = GameRules.AllRoles.First(r => r.Type == role);

            // Rule: Good Faction cannot play Failure
            if (roleDef.Faction == Faction.RoyalConvoy && card == MissionCard.Failure)
                throw new HubException("The Royal Convoy cannot sabotage missions!");

            // Rule: Only Translators can use Reverse
            bool isTranslator = (role == RoleType.TranslatorGood || role == RoleType.TranslatorEvil);
            if (card == MissionCard.Reverse && !isTranslator)
                throw new HubException("Only Translators can use the Reverse card.");

            // Store vote
            session.MissionCards[playerName] = card;

            // Check if all team members have played
            int teamSize = MissionRules.TeamSizes[session.Players.Count][session.MissionIndex];
            if (session.MissionCards.Count == teamSize)
            {
                await ResolveMission(gameCode, session);
            }
        }
    }

    private async Task ResolveMission(string gameCode, GameSessionDto session)
    {
        // Tally cards
        int fails = session.MissionCards.Values.Count(c => c == MissionCard.Failure);
        int reverses = session.MissionCards.Values.Count(c => c == MissionCard.Reverse);

        // Logic: Usually 1 Fail = Fail. 
        // In 7+ player games, Mission 4 often requires 2 Fails (Standard rules).
        // Let's stick to simple: 1 Fail = Fail.

        bool baseResult = (fails == 0); // True = Success, False = Fail

        // Apply Reverse: Odd number of reverses flips the result
        bool finalResult = (reverses % 2 != 0) ? !baseResult : baseResult;

        // Update Score
        if (finalResult) session.GoodWins++;
        else session.EvilWins++;

        // Update History Entry
        var lastEntry = session.History.LastOrDefault();
        if (lastEntry != null)
        {
            lastEntry.MissionOutcome = finalResult
                ? $"SUCCESS (Fails: {fails}, Reverses: {reverses})"
                : $"FAILED (Fails: {fails}, Reverses: {reverses})";

            // Ensure the 'WasApproved' flag is true so it renders colored
            lastEntry.WasApproved = true;
        }

        // Check Game End Conditions
        if (session.EvilWins >= 3)
        {
            session.CurrentPhase = GamePhase.GameOver;
            await BroadcastGameState(gameCode, session, "GAME OVER. The Nomads (Evil) have won 3 missions.");
            return;
        }

        if (session.GoodWins >= 3)
        {
            // TRIGGER ASSASSINATION PHASE
            session.CurrentPhase = GamePhase.Assassination;

            // Find the Assassin to notify them specifically (optional, logic handled in UI)
            var assassinName = session.PlayerRoles.FirstOrDefault(x => x.Value == RoleType.Assassin).Key;

            await BroadcastGameState(gameCode, session, $"Convoy has secured 3 missions! {assassinName}, identify the Priestess to steal the win!");
            return;
        }

        // Next Round
        session.MissionIndex++;
        session.VoteTrack = 0;
        session.LeaderIndex = (session.LeaderIndex + 1) % session.Players.Count;
        session.CurrentPhase = GamePhase.TeamSelection;
        session.CurrentProposal.Clear();
        session.MissionCards.Clear(); // Clear cards for next mission

        await BroadcastGameState(gameCode, session, $"Mission Result: {lastEntry?.MissionOutcome}");
    }

    // 5. Assassin shoots
    public async Task AssassinShoot(string gameCode, string assassinName, string targetName)
    {
        if (_gameManager.Games.TryGetValue(gameCode, out var session))
        {
            if (session.CurrentPhase != GamePhase.Assassination) return;

            // Validate it is the Assassin shooting
            var role = session.PlayerRoles[assassinName];
            if (role != RoleType.Assassin)
                throw new HubException("Only the Assassin can perform the assassination.");

            var targetRole = session.PlayerRoles[targetName];
            var targetDef = GameRules.AllRoles.First(r => r.Type == targetRole);

            if (targetDef.Faction == Faction.DesertNomads)
                throw new HubException("You cannot assassinate your fellow Nomad!");

            if (targetRole == RoleType.HighPriestess)
            {
                await EndGame(gameCode, session, $"ASSASSINATION SUCCESS! {targetName} was the Priestess. NOMADS WIN!");
            }
            else
            {
                await EndGame(gameCode, session, $"ASSASSINATION FAILED! {targetName} was {targetDef.Name}. ROYAL CONVOY WINS!");
            }
        }
    }

    // Helper to End Game and Reveal Roles
    private async Task EndGame(string gameCode, GameSessionDto session, string message)
    {
        session.CurrentPhase = GamePhase.GameOver;

        // Prepare Role Reveal Map
        var roleMap = new Dictionary<string, string>();
        foreach (var kvp in session.PlayerRoles)
        {
            var roleName = GameRules.AllRoles.First(r => r.Type == kvp.Value).Name;
            roleMap.Add(kvp.Key, roleName);
        }

        await BroadcastGameState(gameCode, session, message, null, roleMap);
    }

    // Play Again Logic
    public async Task<GameSessionDto> PlayAgain(string gameCode, string playerName)
    {
        if (_gameManager.Games.TryGetValue(gameCode, out var session))
        {
            // Scenario 1: The game is still in "Game Over" state. 
            // This player is the FIRST to click Play Again.
            if (session.CurrentPhase == GamePhase.GameOver)
            {
                // 1. Reset Game State to Lobby settings
                session.GoodWins = 0;
                session.EvilWins = 0;
                session.MissionIndex = 0;
                session.VoteTrack = 0;
                session.CurrentPhase = GamePhase.IdentityReveal; // Represents "Not Started" / Lobby state effectively
                session.IsStarted = false; // Important: Mark as Lobby

                session.CurrentProposal.Clear();
                session.MissionCards.Clear();
                session.History.Clear();
                session.CurrentVotes.Clear();
                session.PlayerRoles.Clear(); // Clear old roles
                session.ReadyPlayers.Clear();

                // 2. CRITICAL: Clear the player list. 
                // The first person to join the new lobby becomes the Host (Index 0).
                session.Players.Clear();

                // 3. Add this player (Host)
                session.Players.Add(playerName);
                _gameManager.RegisterPlayer(gameCode, playerName, Context.ConnectionId);

                return session;
            }
            // Scenario 2: The game was ALREADY reset by someone else (it is in Lobby state).
            else if (!session.IsStarted)
            {
                // Check if player is already in the list (prevent duplicates)
                if (!session.Players.Contains(playerName))
                {
                    session.Players.Add(playerName);
                    _gameManager.RegisterPlayer(gameCode, playerName, Context.ConnectionId);

                    // Notify others in the lobby that someone rejoined
                    await Clients.Group(gameCode).SendAsync("PlayerJoined", playerName);
                }
                else
                {
                    // Player might be refreshing or clicking twice, just update connection
                    _gameManager.RegisterPlayer(gameCode, playerName, Context.ConnectionId);
                }

                return session;
            }
            // Scenario 3: The game is already IN PROGRESS (IsStarted = true).
            else
            {
                throw new HubException("The game has already started without you!");
            }
        }
        throw new HubException("Game session not found.");
    }

    // Helper to send the GameStateDto to everyone
    private async Task BroadcastGameState(string gameCode, GameSessionDto session, string systemMsg, Dictionary<string, bool>? lastVotes = null, Dictionary<string, string>? gameOverRoles = null)
    {
        int teamSize = 0;
        // Safety check for array bounds in case game over happens oddly
        if (session.MissionIndex < MissionRules.TeamSizes[session.Players.Count].Length)
            teamSize = MissionRules.TeamSizes[session.Players.Count][session.MissionIndex];

        var state = new GameStateDto
        {
            Phase = session.CurrentPhase,
            AllPlayers = new List<string>(session.Players),
            GameHistory = session.History,
            LeaderName = session.Players[session.LeaderIndex],
            CurrentMissionNumber = session.MissionIndex + 1,
            RequiredTeamSize = teamSize,
            FailedVotesCount = session.VoteTrack,
            ProposedTeam = session.CurrentProposal,
            LastVoteResults = lastVotes,
            SystemMessage = systemMsg,
            GameOverRoles = gameOverRoles
        };

        await Clients.Group(gameCode).SendAsync("UpdateGameState", state);
    }
}