using UnityEditor;

namespace Project.Bootstrap.Editor
{
public static class BootstrapMenu
{
    private const string MENU_PATH = "Tools/Core/Auto-Bootstrap On Play";

    [MenuItem(MENU_PATH)]
    private static void ToggleAutoBootstrap()
    {
        bool current = EditorPrefs.GetBool("AutoBootstrap_Enabled", true);
        EditorPrefs.SetBool("AutoBootstrap_Enabled", !current);
    }

    [MenuItem(MENU_PATH, true)]
    private static bool ValidateToggleAutoBootstrap()
    {
        Menu.SetChecked(MENU_PATH, EditorPrefs.GetBool("AutoBootstrap_Enabled", true));
        return true;
    }
}
}