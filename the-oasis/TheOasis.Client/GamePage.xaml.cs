using TheOasis.Shared;

namespace TheOasis.Client;

public partial class GamePage : ContentPage
{
    private readonly PlayerRoleDto _roleData;

    public string RoleName => _roleData.RoleName;
    public string Description => _roleData.Description;
    public string FactionName => _roleData.Faction == Faction.RoyalConvoy ? "ROYAL CONVOY" : "DESERT NOMADS";
    public Color FactionColor => _roleData.Faction == Faction.RoyalConvoy ? Colors.LightGreen : Colors.Red;

    public bool HasSecretInfo => _roleData.KnownInformation.Any();
    public string SecretInfo => string.Join("\n", _roleData.KnownInformation);

    public GamePage(PlayerRoleDto roleData)
    {
        InitializeComponent();
        _roleData = roleData;
        BindingContext = this;
    }
}