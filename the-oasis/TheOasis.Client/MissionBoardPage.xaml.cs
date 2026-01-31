using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.SignalR.Client;
using TheOasis.Shared;

namespace TheOasis.Client;

// ViewModel for Player List
public class PlayerBoardItem : INotifyPropertyChanged
{
    public required string Name { get; set; }

    private bool _isLeader;
    public bool IsLeader
    {
        get => _isLeader;
        set { _isLeader = value; OnPropertyChanged(); }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    private bool _isSelectionMode;
    public bool IsSelectionMode
    {
        get => _isSelectionMode;
        set { _isSelectionMode = value; OnPropertyChanged(); }
    }

    private string _statusText = "";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public Color StatusColor { get; set; } = Colors.Gray;

    // For Assassination Phase
    private bool _isTargetable;
    public bool IsTargetable
    {
        get => _isTargetable;
        set { _isTargetable = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string prop = "")
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}

// ViewModel for Vote Track Bubbles
public class VoteTrackItem
{
    public required Color Color { get; set; }
}

// ViewModel for History
public class HistoryItemViewModel
{
    public string Title { get; set; } = "";
    public string ResultText { get; set; } = "";
    public Color ResultColor { get; set; } = Colors.Gray;
    public Color BorderColor { get; set; } = Colors.Gray;

    public string TeamString { get; set; } = "";

    public List<string> VotesDetail { get; set; } = new();

    public bool HasOutcome { get; set; }
    public string OutcomeString { get; set; } = "";
}

public partial class MissionBoardPage : ContentPage
{
    private readonly HubConnection _hubConnection;
    private readonly string _gameCode;
    private readonly string _myNickname;
    private readonly RoleType _myRole;
    private readonly Faction _myFaction;
    private GameStateDto? _currentState;

    public ObservableCollection<PlayerBoardItem> Players { get; set; } = new();
    public ObservableCollection<VoteTrackItem> VoteTrack { get; set; } = new();
    public ObservableCollection<HistoryItemViewModel> History { get; set; } = new();

    public MissionBoardPage(HubConnection hubConnection, string gameCode, string nickname, GameStateDto initialState, PlayerRoleDto myRoleData)
    {
        InitializeComponent();
        _hubConnection = hubConnection;
        _gameCode = gameCode;
        _myNickname = nickname;
        _myRole = myRoleData.Role;
        _myFaction = myRoleData.Faction;

        // Set Labels in Header
        MyNickLabel.Text = _myNickname;
        MyRoleLabel.Text = _myRole.ToString(); // Or custom display name

        PlayersCollection.ItemsSource = Players;
        VoteTrackView.ItemsSource = VoteTrack;
        HistoryCollection.ItemsSource = History;

        UpdateUI(initialState);
        ConfigureSignalR();
    }

    protected override bool OnBackButtonPressed()
    {
        // Optional: Prompt user if they really want to quit app
        // For now, we just disable the back navigation to previous screens
        return true;
    }

    private void ConfigureSignalR()
    {
        _hubConnection.On<GameStateDto>("UpdateGameState", (state) =>
        {
            MainThread.BeginInvokeOnMainThread(() => UpdateUI(state));
        });
    }

    private void UpdateUI(GameStateDto state)
    {
        _currentState = state;

        // Populate players if list is empty
        if (Players.Count == 0 && state.AllPlayers.Count > 0)
        {
            foreach (var playerName in state.AllPlayers)
            {
                Players.Add(new PlayerBoardItem { Name = playerName });
            }
        }

        SystemMessageLabel.Text = state.SystemMessage;
        MissionInfoLabel.Text = $"Mission {state.CurrentMissionNumber} (Select {state.RequiredTeamSize})";
        VoteTrackLabel.Text = $"Failed Votes: {state.FailedVotesCount}/5";

        // Update Vote Track Visuals
        VoteTrack.Clear();
        for (int i = 0; i < 5; i++)
        {
            VoteTrack.Add(new VoteTrackItem { Color = i < state.FailedVotesCount ? Colors.Red : Colors.Gray });
        }

        // --- UPDATE HISTORY ---
        History.Clear();
        if (state.GameHistory != null)
        {
            foreach (var entry in state.GameHistory.AsEnumerable().Reverse())
            {
                var vm = new HistoryItemViewModel
                {
                    Title = $"Mission {entry.MissionNumber} - Attempt {entry.AttemptNumber}",
                    TeamString = $"Leader: {entry.LeaderName}\nTeam: {string.Join(", ", entry.ProposedTeam)}"
                };

                if (!entry.WasApproved)
                {
                    vm.ResultText = "VOTE REJECTED";
                    vm.ResultColor = Colors.Gray;
                    vm.BorderColor = Colors.Gray;
                    vm.HasOutcome = false;
                }
                else
                {
                    if (string.IsNullOrEmpty(entry.MissionOutcome))
                    {
                        vm.ResultText = "MISSION PENDING";
                        vm.ResultColor = Colors.Orange;
                        vm.BorderColor = Colors.Orange;
                        vm.HasOutcome = false;
                    }
                    else if (entry.MissionOutcome.Contains("Success", StringComparison.OrdinalIgnoreCase))
                    {
                        vm.ResultText = "MISSION SUCCESS";
                        vm.ResultColor = Colors.LightGreen;
                        vm.BorderColor = Colors.LightGreen;
                        vm.HasOutcome = true;
                        vm.OutcomeString = entry.MissionOutcome;
                    }
                    else
                    {
                        vm.ResultText = "MISSION FAILED";
                        vm.ResultColor = Colors.Red;
                        vm.BorderColor = Colors.Red;
                        vm.HasOutcome = true;
                        vm.OutcomeString = entry.MissionOutcome;
                    }
                }

                foreach (var vote in entry.Votes)
                {
                    string symbol = vote.Value ? "✅" : "❌";
                    vm.VotesDetail.Add($"{vote.Key} {symbol}");
                }
                History.Add(vm);
            }
        }

        // --- LOGIC FOR BUTTON VISIBILITY ---
        bool amILeader = state.LeaderName == _myNickname;

        // Hide all controls initially
        ConfirmTeamBtn.IsVisible = false;
        VotingControls.IsVisible = false;
        MissionControls.IsVisible = false;
        PlayAgainBtn.IsVisible = false;

        // Reset Player states
        foreach (var playerItem in Players)
        {
            playerItem.IsLeader = playerItem.Name == state.LeaderName;
            playerItem.IsTargetable = false; // Reset shooting targets

            // Default Status Reset
            if (state.Phase == GamePhase.TeamSelection)
            {
                playerItem.StatusText = "";
                playerItem.IsSelectionMode = amILeader;
                if (!amILeader) playerItem.IsSelected = false;
            }
            else if (state.Phase == GamePhase.Voting)
            {
                playerItem.IsSelectionMode = false;
                bool isInTeam = state.ProposedTeam.Contains(playerItem.Name);
                playerItem.StatusText = isInTeam ? "ON MISSION TEAM" : "";
                playerItem.StatusColor = isInTeam ? Colors.Orange : Colors.Gray;
            }
            else
            {
                playerItem.IsSelectionMode = false;
            }
        }

        // --- PHASE SPECIFIC LOGIC ---

        if (state.Phase == GamePhase.TeamSelection)
        {
            if (amILeader)
            {
                SystemMessageLabel.Text = $"YOU are the Leader. Select {state.RequiredTeamSize} people.";
                ConfirmTeamBtn.IsVisible = true;
                ConfirmTeamBtn.Text = $"CONFIRM ({Players.Count(p => p.IsSelected)}/{state.RequiredTeamSize})";
                ConfirmTeamBtn.IsEnabled = Players.Count(p => p.IsSelected) == state.RequiredTeamSize;
            }
            else
            {
                SystemMessageLabel.Text = $"Leader {state.LeaderName} is choosing...";
            }
        }
        else if (state.Phase == GamePhase.Voting)
        {
            SystemMessageLabel.Text = "Vote on the proposed team!";
            VotingControls.IsVisible = true;
        }
        else if (state.Phase == GamePhase.MissionExecution)
        {
            // Mission Execution Logic
            if (state.ProposedTeam.Contains(_myNickname))
            {
                SystemMessageLabel.Text = "Choose your Mission Card!";
                MissionControls.IsVisible = true;

                // Validation: Good Faction cannot play Failure
                bool isGood = _myFaction == Faction.RoyalConvoy;
                BtnFail.IsEnabled = !isGood;
                BtnFail.Opacity = isGood ? 0.5 : 1.0;

                // Validation: Only Translators use Reverse
                bool isTranslator = (_myRole == RoleType.TranslatorGood || _myRole == RoleType.TranslatorEvil);
                BtnReverse.IsEnabled = isTranslator;
                BtnReverse.Opacity = isTranslator ? 1.0 : 0.5;
            }
            else
            {
                SystemMessageLabel.Text = "Mission Team is performing the mission...";
                // Visual helper: highlight team
                foreach (var p in Players)
                {
                    if (state.ProposedTeam.Contains(p.Name))
                    {
                        p.StatusText = "EXECUTING...";
                        p.StatusColor = Colors.Yellow;
                    }
                }
            }
        }
        else if (state.Phase == GamePhase.Assassination)
        {
            // Assassination Logic
            if (_myRole == RoleType.Assassin)
            {
                SystemMessageLabel.Text = "ASSASSIN: Tap a target to shoot!";
                foreach (var p in Players)
                {
                    // Assassin can target anyone except themselves
                    if (p.Name != _myNickname)
                    {
                        p.IsTargetable = true; // Show Shoot Button
                    }
                }
            }
            else
            {
                SystemMessageLabel.Text = "Assassin is taking the shot...";
            }
        }
        else if (state.Phase == GamePhase.GameOver)
        {
            SystemMessageLabel.Text = state.SystemMessage;
            SystemMessageLabel.TextColor = state.SystemMessage.Contains("WIN") ? Colors.LightGreen : Colors.White;

            if (state.GameOverRoles != null)
            {
                foreach (var p in Players)
                {
                    if (state.GameOverRoles.TryGetValue(p.Name, out string? roleName))
                    {
                        p.StatusText = roleName;
                        bool isEvil = roleName == "Assassin" || roleName.Contains("Evil") || roleName == "Witch" || roleName == "Lone Nomad" || roleName == "Envious Drover";
                        p.StatusColor = isEvil ? Colors.Red : Colors.LightGreen;
                    }
                }
            }

            // Show Play Again button
            PlayAgainBtn.IsVisible = true;
        }

        // Handle Vote Results Visualization (Only keep if we are back in Selection or GameOver)
        if (state.LastVoteResults != null && state.LastVoteResults.Any() && state.Phase != GamePhase.Voting)
        {
            foreach (var playerItem in Players)
            {
                if (state.LastVoteResults.TryGetValue(playerItem.Name, out bool approved))
                {
                    playerItem.StatusText = approved ? "APPROVED" : "REJECTED";
                    playerItem.StatusColor = approved ? Colors.LightGreen : Colors.Red;
                }
            }
        }
    }

    // --- TAB HANDLERS ---
    private void OnTabBoardClicked(object sender, EventArgs e)
    {
        BoardView.IsVisible = true;
        HistoryCollection.IsVisible = false;
        TabBoardBtn.BackgroundColor = Color.FromArgb("#F59E0B");
        TabBoardBtn.TextColor = Colors.Black;
        TabHistoryBtn.BackgroundColor = Color.FromArgb("#333");
        TabHistoryBtn.TextColor = Colors.Gray;
    }

    private void OnTabHistoryClicked(object sender, EventArgs e)
    {
        BoardView.IsVisible = false;
        HistoryCollection.IsVisible = true;
        TabBoardBtn.BackgroundColor = Color.FromArgb("#333");
        TabBoardBtn.TextColor = Colors.Gray;
        TabHistoryBtn.BackgroundColor = Color.FromArgb("#F59E0B");
        TabHistoryBtn.TextColor = Colors.Black;
    }

    private async void OnPlayAgainClicked(object sender, EventArgs e)
    {
        // Disable button to prevent double clicks
        PlayAgainBtn.IsEnabled = false;

        try
        {
            // Invoke server method and expect the Session DTO back
            var session = await _hubConnection.InvokeAsync<GameSessionDto>("PlayAgain", _gameCode, _myNickname);

            // Determine if I am the host (Index 0 in the new list)
            bool amIHost = session.Players.Count > 0 && session.Players[0] == _myNickname;

            // Navigate to Lobby
            var lobbyPage = new LobbyPage(session, _hubConnection, _myNickname);
            lobbyPage.SetHostPrivileges(amIHost);

            var navPage = new NavigationPage(lobbyPage);

            NavigationPage.SetHasNavigationBar(navPage, false);
            NavigationPage.SetHasNavigationBar(lobbyPage, false);

            var window = Application.Current?.Windows.FirstOrDefault();
            if (window is not null)
            {
                // Reset navigation stack
                window.Page = new NavigationPage(lobbyPage);
            }
        }
        catch (Exception ex)
        {
            PlayAgainBtn.IsEnabled = true;
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    // --- ACTION HANDLERS ---
    private void OnPlayerTapped(object sender, TappedEventArgs e)
    {
        if (_currentState == null) return;
        if (_currentState.Phase != GamePhase.TeamSelection) return;
        if (_currentState.LeaderName != _myNickname) return;

        if (e.Parameter is PlayerBoardItem item)
        {
            item.IsSelected = !item.IsSelected;
            int count = Players.Count(p => p.IsSelected);
            ConfirmTeamBtn.Text = $"CONFIRM ({count}/{_currentState.RequiredTeamSize})";
            ConfirmTeamBtn.IsEnabled = count == _currentState.RequiredTeamSize;
        }
    }

    private async void OnConfirmTeamClicked(object sender, EventArgs e)
    {
        var selectedNames = Players.Where(p => p.IsSelected).Select(p => p.Name).ToList();
        try
        {
            await _hubConnection.InvokeAsync("ProposeTeam", _gameCode, selectedNames);
        }
        catch (Exception ex) { await DisplayAlertAsync("Error", ex.Message, "OK"); }
    }

    private async void OnVoteApprove(object sender, EventArgs e) => await SendVote(true);
    private async void OnVoteReject(object sender, EventArgs e) => await SendVote(false);

    private async Task SendVote(bool approve)
    {
        VotingControls.IsVisible = false;
        SystemMessageLabel.Text = "Vote Submitted. Waiting for others...";
        try
        {
            await _hubConnection.InvokeAsync("VoteForTeam", _gameCode, _myNickname, approve);
        }
        catch (Exception ex)
        {
            VotingControls.IsVisible = true;
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    // Mission Handlers
    private async void OnMissionSuccess(object sender, EventArgs e) => await SendCard(MissionCard.Success);
    private async void OnMissionFail(object sender, EventArgs e) => await SendCard(MissionCard.Failure);
    private async void OnMissionReverse(object sender, EventArgs e) => await SendCard(MissionCard.Reverse);

    private async Task SendCard(MissionCard card)
    {
        MissionControls.IsVisible = false;
        SystemMessageLabel.Text = "Card Submitted.";
        try
        {
            await _hubConnection.InvokeAsync("SubmitMissionCard", _gameCode, _myNickname, card);
        }
        catch (Exception ex)
        {
            MissionControls.IsVisible = true;
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    // Shoot Handler
    private async void OnShootClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string targetName)
        {
            bool confirm = await DisplayAlertAsync("Assassinate", $"Shoot {targetName}?", "Yes", "Cancel");
            if (confirm)
            {
                try
                {
                    await _hubConnection.InvokeAsync("AssassinShoot", _gameCode, _myNickname, targetName);
                }
                catch (Exception ex) { await DisplayAlertAsync("Error", ex.Message, "OK"); }
            }
        }
    }
}