using Microsoft.AspNetCore.SignalR.Client;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TheOasis.Shared;

namespace TheOasis.Client;

public partial class RoleSelectionPage : ContentPage
{
    private readonly GameSessionDto _session;
    private readonly HubConnection _hubConnection;
    private List<SelectableRole> _roles = new();

    public RoleSelectionPage(GameSessionDto session, HubConnection hubConnection)
    {
        InitializeComponent();
        _session = session;
        _hubConnection = hubConnection;

        LoadData();
        UpdateCounters();
    }

    private void LoadData()
    {
        int playerCount = _session.Players.Count;
        PlayerCountLabel.Text = $"Players: {playerCount}";

        if (GameRules.PlayerDistributions.TryGetValue(playerCount, out var rules))
        {
            BalanceLabel.Text = $"Required: {rules.Good} Good, {rules.Evil} Evil";
        }
        else
        {
            BalanceLabel.Text = "Invalid player count (5-10 allowed)";
        }

        // Populate list
        foreach (var role in GameRules.AllRoles)
        {
            bool isPreSelected = _session.SelectedRoles.Contains(role.Type);

            _roles.Add(new SelectableRole
            {
                Type = role.Type,
                Name = role.Name,
                Description = role.Description,
                Faction = role.Faction,
                IsSelected = isPreSelected
            });
        }
        RolesCollection.ItemsSource = _roles;
    }

    private void OnRoleItemTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is SelectableRole role)
        {
            role.IsSelected = !role.IsSelected;

            UpdateCounters();
        }
    }

    private void UpdateCounters()
    {
        int good = _roles.Count(r => r.IsSelected && r.Faction == Faction.RoyalConvoy);
        int evil = _roles.Count(r => r.IsSelected && r.Faction == Faction.DesertNomads);

        GoodCountLabel.Text = $"Good Selected: {good}";
        EvilCountLabel.Text = $"Evil Selected: {evil}";

        // Validate logic
        int totalPlayers = _session.Players.Count;
        if (GameRules.PlayerDistributions.TryGetValue(totalPlayers, out var rules))
        {
            bool isValid = (good == rules.Good) && (evil == rules.Evil);
            ConfirmBtn.IsEnabled = isValid;
            ConfirmBtn.Opacity = isValid ? 1.0 : 0.5;
            ConfirmBtn.Text = isValid ? "CONFIRM & SAVE" : $"Select {rules.Good} Good & {rules.Evil} Evil";
        }
    }

    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        var selectedTypes = _roles.Where(r => r.IsSelected).Select(r => r.Type).ToList();

        try
        {
            // Update local session immediately so if we come back, it's saved
            _session.SelectedRoles = selectedTypes;

            await _hubConnection.InvokeAsync("UpdateGameSettings", _session.GameCode, selectedTypes);
            await Navigation.PopAsync(); // Go back to Lobby
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }
}

// Helper class for UI binding
public class SelectableRole : INotifyPropertyChanged
{
    public RoleType Type { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public Faction Faction { get; set; }
    public Color FactionColor => Faction == Faction.RoyalConvoy ? Colors.LightGreen : Colors.IndianRed;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}