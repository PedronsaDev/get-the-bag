namespace Project.AI.Configuration
{
public enum AIRole : byte
{
    // Guard stays 0 so archetype assets saved before roles existed still fight.
    Guard,
    Police,
    Civilian,
    Pedestrian
}

public static class AIRoleExtensions
{
    public static bool Fights(this AIRole role) => role is AIRole.Guard or AIRole.Police;

    public static bool Patrols(this AIRole role) => role.Fights();

    public static bool Flees(this AIRole role) => role is AIRole.Civilian or AIRole.Pedestrian;

    public static bool CallsPolice(this AIRole role) => role == AIRole.Civilian;

    public static bool Investigates(this AIRole role) => role is AIRole.Guard or AIRole.Police;
}
}
