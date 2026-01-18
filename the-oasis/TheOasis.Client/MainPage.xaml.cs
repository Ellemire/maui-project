using Microsoft.AspNetCore.SignalR.Client;
using TheOasis.Shared;

namespace TheOasis.Client;

public partial class MainPage : ContentPage
{
    private HubConnection? _hubConnection;
    private const string ServerUrl = "https://k7dc640z-7208.euw.devtunnels.ms/gameHub";
    private bool _isConnected = false;

    public MainPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_isConnected)
        {
            await ConnectToServerAsync();
        }
    }

    private async Task ConnectToServerAsync()
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(ServerUrl)
            .WithAutomaticReconnect()
            .Build();

        for (int i = 0; i < 3; i++)
        {
            try
            {
                await _hubConnection.StartAsync();
                _isConnected = true;
                Console.WriteLine("Connected to SignalR!");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Attempt {i + 1} failed: {ex.Message}");
                await Task.Delay(2000);
            }
        }

        await DisplayAlertAsync("Connection Error", "Could not connect to the server. Please check your internet or try again later.", "OK");
    }

    private async void OnCreateClicked(object sender, EventArgs e)
    {
        if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected)
        {
            await DisplayAlertAsync("Error", "No connection to server.", "OK");
            return;
        }

        try
        {
            // Assuming Host is "Player 1" initially
            var session = await _hubConnection.InvokeAsync<GameSessionDto>("CreateGame", "HostPlayer");

            // Navigate to Lobby
            var lobbyPage = new LobbyPage(session, _hubConnection);
            lobbyPage.SetHostPrivileges(true);
            await Navigation.PushAsync(lobbyPage);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", "Failed to create game: " + ex.Message, "OK");
        }
    }

    private async void OnJoinClicked(object sender, EventArgs e)
    {
        if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected)
        {
            await DisplayAlertAsync("Error", "No connection to server.", "OK");
            return;
        }

        var code = CodeEntry.Text;
        if (string.IsNullOrWhiteSpace(code) || code.Length != 4)
        {
            await DisplayAlertAsync("Error", "Please enter a 4-digit code.", "OK");
            return;
        }

        try
        {
            // Hardcoded name for testing, normally you'd ask for input
            var session = await _hubConnection.InvokeAsync<GameSessionDto>("JoinGame", code, "GuestPlayer");

            var lobbyPage = new LobbyPage(session, _hubConnection);
            lobbyPage.SetHostPrivileges(false);
            await Navigation.PushAsync(lobbyPage);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", "Failed to join: " + ex.Message, "OK");
        }
    }
}