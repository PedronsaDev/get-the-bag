using PedronsaDev.Console;

namespace Project.Player.Profile
{
[CommandGroup("PlayerProfile")]
public static class PlayerProfileCommands
{
    
    [Command("pp_change_name", "Changes the player's name to the specified value. Usage: pp_change_name <newName>")]
    public static void ChangePlayerName(string newName)
    {
        PlayerProfileManager.Instance.ChangePlayerName(newName);
        Console.Log($"[PlayerProfile] Player name changed to {newName}.");
    }
    
    [Command("pp_add_experience", "Adds the specified amount of experience to the player. Usage: pp_add_experience <amount>")]
    public static void AddExperience(int amount)
    {
        PlayerProfileManager.Instance.AddExperience(amount);
        Console.Log($"[PlayerProfile] Adding {amount} experience to the player.");
    }

    [Command("pp_add_scrip", "Adds the specified amount of scrip to the player. Usage: pp_add_scrip <amount>")]
    public static void AddScrip(int amount)
    {
        PlayerProfileManager.Instance.AddScrip(amount);
        Console.Log($"[PlayerProfile] Adding {amount} scrip to the player.");
    }
    
    [Command("pp_set_level", "Sets the player's level to the specified value. Usage: pp_set_level <level>")]
    public static void SetLevel(int level)
    {
        PlayerProfileManager.Instance.SetPlayerLevel(level);
        Console.Log($"[PlayerProfile] Player level set to {level}.");
    }

}
}