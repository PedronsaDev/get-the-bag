using UnityEditor;

namespace Project.Core.Editor
{
public static class BootstrapMenu
{
    private const string MenuPath = "Tools/Core/Auto-Bootstrap On Play";

    [MenuItem(MenuPath)]
    private static void ToggleAutoBootstrap()
    {
        bool current = EditorPrefs.GetBool("AutoBootstrap_Enabled", true);
        EditorPrefs.SetBool("AutoBootstrap_Enabled", !current);
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateToggleAutoBootstrap()
    {
        Menu.SetChecked(MenuPath, EditorPrefs.GetBool("AutoBootstrap_Enabled", true));
        return true;
    }
}
}