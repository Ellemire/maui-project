using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.SignalR.Client;
using TheOasis.Shared;

namespace TheOasis.Client;

// ViewModel for Player List
public class PlayerBoardItem : System.ComponentModel.INotifyPropertyChanged
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

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string prop = "")
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}

// ViewModel for Vote Track Bubbles
public class VoteTrackItem
{
    public required Color Color { get; set; } 
}

public class HistoryItemViewModel
{
    public string Title { get; set; } = ""; // e.g. "Mission 1 - Attempt 2"
    public string ResultText { get; set; } = ""; // "APPROVED" or "REJECTED"
    public Color ResultColor { get; set; } = Colors.Gray;
    public Color BorderColor { get; set; } = Colors.Gray;

    public string TeamString { get; set; } = ""; // "Leader: X | Team: A, B"

    public List<string> VotesDetail { get; set; } = new();

    public bool HasOutcome { get; set; }
    public string OutcomeString { get; set; } = ""; // "Mission SUCCESS"
}

public partial class MissionBoardPage : ContentPage
{
    private readonly HubConnection _hubConnection;
    private readonly string _gameCode;
    private readonly string _myNickname;
    private GameStateDto? _currentState;

    public ObservableCollection<PlayerBoardItem> Players { get; set; } = new();
    public ObservableCollection<VoteTrackItem> VoteTrack { get; set; } = new();
    public ObservableCollection<HistoryItemViewModel> History { get; set; } = new();

    public MissionBoardPage(HubConnection hubConnection, string gameCode, string nickname, GameStateDto initialState)
    {
        InitializeComponent();
        _hubConnection = hubConnection;
        _gameCode = gameCode;
        _myNickname = nickname;

        PlayersCollection.ItemsSource = Players;
        VoteTrackView.ItemsSource = VoteTrack;
        HistoryCollection.ItemsSource = History;

        UpdateUI(initialState);
        ConfigureSignalR();
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

        History.Clear();
        if (state.GameHistory != null)
        {
            // Iterate reverse to show newest on top
            foreach (var entry in state.GameHistory.AsEnumerable().Reverse())
            {
                var vm = new HistoryItemViewModel
                {
                    Title = $"Mission {entry.MissionNumber} - Attempt {entry.AttemptNumber}",

                    // 1. ALWAYS SHOW THE TEAM
                    TeamString = $"Leader: {entry.LeaderName}\nTeam: {string.Join(", ", entry.ProposedTeam)}"
                };

                // 2. COLOR LOGIC
                if (!entry.WasApproved)
                {
                    // Case: VOTE REJECTED -> GRAY
                    vm.ResultText = "VOTE REJECTED";
                    vm.ResultColor = Colors.Gray;
                    vm.BorderColor = Colors.Gray;
                    vm.HasOutcome = false;
                }
                else
                {
                    // Case: VOTE APPROVED -> Check Mission Outcome
                    if (string.IsNullOrEmpty(entry.MissionOutcome))
                    {
                        // Approved but mission pending (or executing) -> ORANGE
                        vm.ResultText = "MISSION PENDING";
                        vm.ResultColor = Colors.Orange;
                        vm.BorderColor = Colors.Orange;
                        vm.HasOutcome = false;
                    }
                    else if (entry.MissionOutcome.Contains("Success", StringComparison.OrdinalIgnoreCase))
                    {
                        // Mission Result: SUCCESS -> GREEN
                        vm.ResultText = "MISSION SUCCESS";
                        vm.ResultColor = Colors.LightGreen;
                        vm.BorderColor = Colors.LightGreen;
                        vm.HasOutcome = true;
                        vm.OutcomeString = entry.MissionOutcome;
                    }
                    else
                    {
                        // Mission Result: FAIL -> RED
                        vm.ResultText = "MISSION FAILED";
                        vm.ResultColor = Colors.Red;
                        vm.BorderColor = Colors.Red;
                        vm.HasOutcome = true;
                        vm.OutcomeString = entry.MissionOutcome;
                    }
                }

                // Add Vote Details (Who voted Yes/No)
                foreach (var vote in entry.Votes)
                {
                    string symbol = vote.Value ? "✅" : "❌";
                    vm.VotesDetail.Add($"{vote.Key} {symbol}");
                }

                History.Add(vm);
            }
        }

        // Logic depending on Phase
        bool amILeader = state.LeaderName == _myNickname;

        ConfirmTeamBtn.IsVisible = false;
        VotingControls.IsVisible = false;

        // --- Refresh Player List Visuals ---
        foreach (var playerItem in Players)
        {
            playerItem.IsLeader = playerItem.Name == state.LeaderName;

            // Phase: TEAM SELECTION
            if (state.Phase == GamePhase.TeamSelection)
            {
                playerItem.IsSelectionMode = true; // Show checkboxes
                                                   // If I am leader, I control checkboxes. If not, I just see them.
                                                   // Actually, only Leader sees checkboxes to interact, others see highlight? 
                                                   // Let's keep it simple: Checkboxes visible for everyone to see proposal building up?
                                                   // No, usually only leader selects locally, then confirms.

                playerItem.IsSelectionMode = amILeader;
                playerItem.StatusText = "";

                // If not leader, maybe clear selection visualization until confirmed?
                if (!amILeader) playerItem.IsSelected = false;
            }
            // Phase: VOTING
            else if (state.Phase == GamePhase.Voting)
            {
                playerItem.IsSelectionMode = false;
                // Highlight who is in the proposal
                bool isInTeam = state.ProposedTeam.Contains(playerItem.Name);
                playerItem.StatusText = isInTeam ? "ON MISSION TEAM" : "";
                playerItem.StatusColor = isInTeam ? Colors.Orange : Colors.Gray;
            }
        }

        // --- Phase Specific Controls ---
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
            VotingControls.IsVisible = true; // Everyone votes
        }
        else if (state.Phase == GamePhase.GameOver)
        {
            DisplayAlertAsync("GAME OVER", state.SystemMessage, "Close");
        }

        // --- Handle Vote Results (After voting finishes) ---
        if (state.LastVoteResults != null && state.LastVoteResults.Any())
        {
            // Show how everyone voted in the list
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

    private void OnTabBoardClicked(object sender, EventArgs e)
    {
        BoardView.IsVisible = true;
        HistoryCollection.IsVisible = false;

        TabBoardBtn.BackgroundColor = Color.FromArgb("#F59E0B"); // Active Orange
        TabBoardBtn.TextColor = Colors.Black;

        TabHistoryBtn.BackgroundColor = Color.FromArgb("#333"); // Inactive Dark
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

    // Handle Tap on Player (Only for Leader during Selection)
    private void OnPlayerTapped(object sender, TappedEventArgs e)
    {
        if (_currentState == null) return;
        if (_currentState.Phase != GamePhase.TeamSelection) return;
        if (_currentState.LeaderName != _myNickname) return;

        if (e.Parameter is PlayerBoardItem item)
        {
            item.IsSelected = !item.IsSelected;

            // Update Button Text
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
        VotingControls.IsVisible = false; // Hide buttons so user can't spam
        SystemMessageLabel.Text = "Vote Submitted. Waiting for others...";
        try
        {
            await _hubConnection.InvokeAsync("VoteForTeam", _gameCode, _myNickname, approve);
        }
        catch (Exception ex)
        {
            VotingControls.IsVisible = true; // Restore if error
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }
}