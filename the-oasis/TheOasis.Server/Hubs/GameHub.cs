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
}