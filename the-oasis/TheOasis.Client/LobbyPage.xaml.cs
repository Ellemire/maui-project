using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.ObjectModel;
using TheOasis.Shared;

namespace TheOasis.Client;

public partial class LobbyPage : ContentPage
{
    private readonly HubConnection _hubConnection;
    private readonly GameSessionDto _currentSession;
    private readonly string _myNickname;

    // ObservableCollection updates the UI automatically
    public ObservableCollection<string> Players { get; set; } = new();
    public ObservableCollection<RoleBadge> ActiveRoles { get; set; } = new();

    public LobbyPage(GameSessionDto session, HubConnection hubConnection, string myNickname)
    {
        InitializeComponent();

        _currentSession = session;
        _hubConnection = hubConnection;
        _myNickname = myNickname;

        // Bind data
        PlayersList.ItemsSource = Players;
        GameCodeLabel.Text = session.GameCode;

        // Load existing players
        foreach (var p in session.Players)
        {
            Players.Add(p);
        }

        ActiveRolesList.ItemsSource = ActiveRoles;
        UpdateActiveRolesDisplay(session.SelectedRoles); // Initial load

        ConfigureSignalR();
    }

    public void SetHostPrivileges(bool isHost)
    {
        StartGameBtn.IsVisible = isHost;
        SettingsBtn.IsVisible = isHost;
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        // Navigate to Role Selection
        await Navigation.PushAsync(new RoleSelectionPage(_currentSession, _hubConnection));
    }

    private void ConfigureSignalR()
    {
        _hubConnection.Remove("PlayerJoined");
        _hubConnection.Remove("PlayerLeft");
        _hubConnection.Remove("GameStarted");
        _hubConnection.Remove("GameSettingsChanged");
        _hubConnection.Remove("ReceiveRole");

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

        // Listen: Player Left
        _hubConnection.On<string>("PlayerLeft", (leftPlayerName) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (Players.Contains(leftPlayerName))
                {
                    Players.Remove(leftPlayerName);
                }
            });
        });

        // Listen: Game Settings Changed
        _hubConnection.On<List<RoleType>>("GameSettingsChanged", (newRoles) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Update local session data
                _currentSession.SelectedRoles = newRoles;
                // Update UI
                UpdateActiveRolesDisplay(newRoles);
            });
        });

        // Listen: Host Started Game
        _hubConnection.On("GameStarted", async () =>
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {

            });
        });

        // Listen: Receive Role Assignment
        _hubConnection.On<PlayerRoleDto>("ReceiveRole", async (roleDto) =>
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                // Remove this page from stack so user can't go back to Lobby
                // (Optional, depending on desired UX)

                await Navigation.PushAsync(new GamePage(roleDto, _hubConnection, _currentSession.GameCode, _myNickname));
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
        try
        {
            // Notify server that we are leaving
            await _hubConnection.InvokeAsync("LeaveGame", _currentSession.GameCode, _myNickname);
        }
        catch
        {
            // Even if server call fails, we still want to exit locally
        }
        finally
        {
            if (Navigation.NavigationStack.Count <= 1)
            {
                // We are at root, do a hard reset to MainPage
                var window = Application.Current?.Windows.FirstOrDefault();
                if (window is not null)
                {
                    window.Page = new NavigationPage(new MainPage());
                }
            }
            else
            {
                // Close Lobby Page and go back to Main Page
                await Navigation.PopAsync();
            }
        }
    }

    private void UpdateActiveRolesDisplay(List<RoleType> roles)
    {
        ActiveRoles.Clear();
        foreach (var roleType in roles)
        {
            var def = GameRules.AllRoles.FirstOrDefault(r => r.Type == roleType);
            if (def != null)
            {
                ActiveRoles.Add(new RoleBadge
                {
                    Name = def.Name,
                    FactionColor = def.Faction == Faction.RoyalConvoy ? Colors.LightGreen : Colors.IndianRed
                });
            }
        }
    }
}

// Helper DTO for display in Lobby
public class RoleBadge
{
    public string Name { get; set; } = "";
    public required Color FactionColor { get; set; }
}