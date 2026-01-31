using Microsoft.AspNetCore.SignalR.Client;
using TheOasis.Shared;

namespace TheOasis.Client;

public partial class GamePage : ContentPage
{
    // Fields to store the connection and session info
    private readonly HubConnection _hubConnection;
    private readonly string _gameCode;
    private readonly string _myNickname;
    private readonly PlayerRoleDto _roleData;

    // Properties bound to the XAML UI
    public string RoleName => _roleData.RoleName;
    public string Description => _roleData.Description;
    public string FactionName => _roleData.Faction == Faction.RoyalConvoy ? "ROYAL CONVOY" : "DESERT NOMADS";
    public Color FactionColor => _roleData.Faction == Faction.RoyalConvoy ? Colors.LightGreen : Colors.Red;

    public bool HasSecretInfo => _roleData.KnownInformation.Any();
    public string SecretInfo => string.Join("\n", _roleData.KnownInformation);

    public GamePage(PlayerRoleDto roleData, HubConnection hubConnection, string gameCode, string nickname)
    {
        InitializeComponent();

        // 1. Assign local fields
        _roleData = roleData;
        _hubConnection = hubConnection;
        _gameCode = gameCode;
        _myNickname = nickname;

        // 2. Set BindingContext so XAML can read the properties above
        BindingContext = this;

        // 3. Setup the listener for the game start
        ConfigureSignalR();
    }

    private void ConfigureSignalR()
    {
        // Listener: When server says "UpdateGameState", it means everyone is ready
        // and the game loop (Team Selection) has started.
        _hubConnection.On<GameStateDto>("UpdateGameState", async (state) =>
        {
            // UI updates must be on the Main Thread
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                // Remove this listener to avoid double-handling on the next page (optional but good practice)
                _hubConnection.Remove("UpdateGameState");

                // Navigate to the main Game Board
                await Navigation.PushAsync(new MissionBoardPage(_hubConnection, _gameCode, _myNickname, state, _roleData));
            });
        });
    }

    private async void OnProceedClicked(object sender, EventArgs e)
    {
        // Disable button to prevent double clicks
        if (sender is Button btn)
        {
            btn.IsEnabled = false;
            btn.Text = "Waiting for others...";
        }

        try
        {
            // Send "I am ready" signal to the server
            await _hubConnection.InvokeAsync("PlayerReadyForMission", _gameCode, _myNickname);
        }
        catch (Exception ex)
        {
            // Re-enable button if there was an error
            if (sender is Button btn2)
            {
                btn2.IsEnabled = true;
                btn2.Text = "PROCEED TO MISSION";
            }
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }
}