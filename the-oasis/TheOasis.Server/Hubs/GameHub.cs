using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using TheOasis.Shared;

namespace TheOasis.Server.Hubs;


public class GameManager
{
    // Using ConcurrentDictionary for thread safety
    public ConcurrentDictionary<string, GameSessionDto> Games { get; } = new();
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

    public async Task StartGame(string gameCode)
    {
        if (_gameManager.Games.TryGetValue(gameCode, out var session))
        {
            session.IsStarted = true;
            await Clients.Group(gameCode).SendAsync("GameStarted");
        }
    }
}