using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using TheOasis.Shared;

namespace TheOasis.Server.Hubs;


public class GameManager
{
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
        var code = new Random().Next(1000, 9999).ToString();

        var session = new GameSessionDto { GameCode = code };
        session.Players.Add(hostName);

        _gameManager.Games.TryAdd(code, session);

        await Groups.AddToGroupAsync(Context.ConnectionId, code);

        return session;
    }

    public async Task<GameSessionDto> JoinGame(string gameCode, string playerName)
    {
        if (_gameManager.Games.TryGetValue(gameCode, out var session))
        {
            if (session.Players.Contains(playerName))
                throw new HubException("Nick taken.");

            session.Players.Add(playerName);

            await Groups.AddToGroupAsync(Context.ConnectionId, gameCode);

            await Clients.Group(gameCode).SendAsync("PlayerJoined", playerName);

            return session;
        }
        throw new HubException("Game does not exist.");
    }
}