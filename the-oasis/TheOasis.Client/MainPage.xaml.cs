using Microsoft.AspNetCore.SignalR.Client;
using TheOasis.Shared;

namespace TheOasis.Client;

public partial class MainPage : ContentPage
{
    private HubConnection? _hubConnection;
    //private const string ServerUrl = "https://k7dc640z-7208.euw.devtunnels.ms/gameHub";
    private const string ServerUrl = "https://nxj54rpw-7208.euw.devtunnels.ms/gameHub";
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
        if (!CheckConnection()) return;

        // Validation: Nickname
        string nick = NickEntry.Text;
        if (string.IsNullOrWhiteSpace(nick))
        {
            await DisplayAlertAsync("Error", "Please enter your nickname.", "OK");
            return;
        }

        try
        {
            // Pass the actual nickname instead of "HostPlayer"
            var session = await _hubConnection!.InvokeAsync<GameSessionDto>("CreateGame", nick);

            // Pass the nickname to the Lobby so we know who we are
            var lobbyPage = new LobbyPage(session, _hubConnection!, nick);
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
        if (!CheckConnection()) return;

        string nick = NickEntry.Text;
        string code = CodeEntry.Text;

        // Validation
        if (string.IsNullOrWhiteSpace(nick))
        {
            await DisplayAlertAsync("Error", "Please enter your nickname.", "OK");
            return;
        }
        if (string.IsNullOrWhiteSpace(code) || code.Length != 6)
        {
            await DisplayAlertAsync("Error", "Please enter a 6-digit code.", "OK");
            return;
        }

        try
        {
            var session = await _hubConnection!.InvokeAsync<GameSessionDto>("JoinGame", code, nick);

            var lobbyPage = new LobbyPage(session, _hubConnection!, nick);
            lobbyPage.SetHostPrivileges(false);
            await Navigation.PushAsync(lobbyPage);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", "Failed to join: " + ex.Message, "OK");
        }
    }

    private bool CheckConnection()
    {
        if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected)
        {
            DisplayAlertAsync("Error", "No connection to server.", "OK");
            return false;
        }
        return true;
    }
}