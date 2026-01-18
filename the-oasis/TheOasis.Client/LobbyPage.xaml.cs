using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.ObjectModel;
using TheOasis.Shared;

namespace TheOasis.Client;

public partial class LobbyPage : ContentPage
{
    private readonly HubConnection _hubConnection;
    private readonly GameSessionDto _currentSession;

    // ObservableCollection updates the UI automatically
    public ObservableCollection<string> Players { get; set; } = new();

    public LobbyPage(GameSessionDto session, HubConnection hubConnection)
    {
        InitializeComponent();

        _currentSession = session;
        _hubConnection = hubConnection;

        // Bind data
        PlayersList.ItemsSource = Players;
        GameCodeLabel.Text = session.GameCode;

        // Load existing players
        foreach (var p in session.Players)
        {
            Players.Add(p);
        }

        ConfigureSignalR();
    }

    public void SetHostPrivileges(bool isHost)
    {
        StartGameBtn.IsVisible = isHost;
    }

    private void ConfigureSignalR()
    {
        // Listen: Player Joined
        _hubConnection.On<string>("PlayerJoined", (newPlayerName) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!Players.Contains(newPlayerName))
                {
                    Players.Add(newPlayerName);
                }
            });
        });

        // Listen: Host Started Game
        _hubConnection.On("GameStarted", async () =>
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DisplayAlertAsync("The Oasis", "The game is starting!", "OK");
                // Navigation to Role/Game Page will happen here
            });
        });
    }

    private async void OnStartGameClicked(object sender, EventArgs e)
    {
        try
        {
            await _hubConnection.InvokeAsync("StartGame", _currentSession.GameCode);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", "Failed to start: " + ex.Message, "OK");
        }
    }

    private async void OnLeaveClicked(object sender, EventArgs e)
    {
        // Logic to leave the group on server should be added here
        await Navigation.PopAsync();
    }
}