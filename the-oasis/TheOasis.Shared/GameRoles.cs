namespace TheOasis.Shared;

public enum Faction
{
    RoyalConvoy,   // Good
    DesertNomads   // Evil
}

public enum RoleType
{
    // --- Royal Convoy (Good) ---
    NumbianPrincess,    // Required leader
    HighPriestess,      // Merlin equivalent (knows bad guys)
    TaSetiGuard,        // Percival equivalent (knows Priestess)
    ChabirGuide,        // Special ability
    TranslatorGood,     // Good Negotiator
    ScoutGood,          // Swapper
    LoyalServant,       // Generic Good (Standard Villager)

    // --- Desert Nomads (Evil) ---
    Assassin,           // Kills Priestess at end
    TranslatorEvil,     // Evil Negotiator
    ScoutEvil,          // Swapper
    Witch,              // Morgana equivalent (appears as Priestess)
    LoneNomad,          // Oberon equivalent (unknown to other evils)
    EnviousDrover,      // Mordred equivalent (hidden from Priestess)
    MinionOfSeth        // Generic Evil
}

public class RoleDefinition
{
    public RoleType Type { get; set; }
    public string Name { get; set; } = "";
    public Faction Faction { get; set; }
    public string Description { get; set; } = "";
    public bool IsUnique { get; set; } = true; // Most special roles are unique
}

public static class GameRules
{
    // Key: Number of Players, Value: (Good Count, Evil Count)
    public static readonly Dictionary<int, (int Good, int Evil)> PlayerDistributions = new()
    {
        { 5, (3, 2) },
        { 6, (4, 2) },
        { 7, (4, 3) },
        { 8, (5, 3) },
        { 9, (6, 3) },
        { 10, (6, 4) }
    };

    public static List<RoleDefinition> AllRoles => new()
    {
        new() { Type = RoleType.NumbianPrincess, Name = "Numbian Princess", Faction = Faction.RoyalConvoy, Description = "If she reaches the alliance, Convoy wins." },
        new() { Type = RoleType.HighPriestess, Name = "High Priestess of Isis", Faction = Faction.RoyalConvoy, Description = "Knows the Nomads. Must remain hidden." },
        new() { Type = RoleType.TaSetiGuard, Name = "Ta-Seti Guard", Faction = Faction.RoyalConvoy, Description = "Knows the High Priestess." },
        new() { Type = RoleType.ChabirGuide, Name = "Chabir Guide", Faction = Faction.RoyalConvoy, Description = "Can ensure mission success once." },
        new() { Type = RoleType.TranslatorGood, Name = "Translator (Good)", Faction = Faction.RoyalConvoy, Description = "Can use reverse card." },
        new() { Type = RoleType.ScoutGood, Name = "Scout (Good)", Faction = Faction.RoyalConvoy, Description = "Can swap mission members." },

        new() { Type = RoleType.Assassin, Name = "Assassin", Faction = Faction.DesertNomads, Description = "Tries to kill the Priestess at the end." },
        new() { Type = RoleType.Witch, Name = "Witch", Faction = Faction.DesertNomads, Description = "Appears as Priestess to the Guard." },
        new() { Type = RoleType.TranslatorEvil, Name = "Translator (Evil)", Faction = Faction.DesertNomads, Description = "Sabotages negotiations." },
        new() { Type = RoleType.ScoutEvil, Name = "Scout (Evil)", Faction = Faction.DesertNomads, Description = "Can swap mission members." },
        new() { Type = RoleType.LoneNomad, Name = "Lone Nomad", Faction = Faction.DesertNomads, Description = "Does not know other Nomads." },
        new() { Type = RoleType.EnviousDrover, Name = "Envious Drover", Faction = Faction.DesertNomads, Description = "Hidden from the High Priestess." },
    };
}

// DTO to send the specific role to the player
public class PlayerRoleDto
{
    public RoleType Role { get; set; }
    public Faction Faction { get; set; }
    public string RoleName { get; set; } = "";
    public string Description { get; set; } = "";

    // Information revealed to this player (e.g., Priestess sees Evil players)
    public List<string> KnownInformation { get; set; } = new();
}