using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;

namespace LombaxGuy.AVN
{
    [InitializeOnLoad]
    public static class AutomaticVersionNumbering
    {
        #region Constants
        private const string AUTO_VERSION_MENU_ITEM = "Tools/LombaxGuy/Automatic Version Numbering/Toggle";
        private const string PLUGIN_LOG_NAME = "Automatic Version Numbering";
        private const string IS_ENABLED_KEY = "automatic_version_numbering:is_enabled";
        #endregion

        #region Private fields
        private static bool _isEnabled;
        #endregion

        #region Constructor
        static AutomaticVersionNumbering()
        {
            // delay the call to setup, otherwise the menu items initial state wont be set
            EditorApplication.delayCall += OnSetup;
        }
        #endregion

        #region Callbacks
        private static void OnSetup()
        {
            EditorApplication.delayCall -= OnSetup;

            // set the initial state of the tool
            _isEnabled = EditorPrefs.GetBool(IS_ENABLED_KEY, false);

            // shows whether the tool is enabled or disabled
            Menu.SetChecked(AUTO_VERSION_MENU_ITEM, _isEnabled);
        }

        /// <summary>
        /// Executes after a game was successfully built. Changes the current version if it had any numbers in it and the tool was enabled.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="path"></param>
        [PostProcessBuild]
        private static void IncrementVersionNumber(BuildTarget target, string path)
        {
            // if the tool is disabled, just return
            if (!_isEnabled)
                return;

            string newVersion = ConstructNewVersionString(PlayerSettings.bundleVersion);

            // if newVersion is null it means that the original version number did not contain any numbers, log a warning and then return
            if (string.IsNullOrEmpty(newVersion))
            {
                Debug.LogWarning($"{PLUGIN_LOG_NAME}: Version number was not automatically increased because no numbers were detected in the current version: '{PlayerSettings.bundleVersion}'.");
                return;
            }

            // change the current version
            PlayerSettings.bundleVersion = newVersion;
        }
        #endregion

        #region Menu items
        /// <summary>
        /// Unity menu item used for enabeling or disabeling the tool.
        /// </summary>
        [MenuItem(AUTO_VERSION_MENU_ITEM)]
        private static void Toggle()
        {
            // toggels the tool on or off
            _isEnabled = !_isEnabled;

            // save the current setting
            EditorPrefs.SetBool(IS_ENABLED_KEY, _isEnabled);

            // shows whether the tool is enabled or disabled
            Menu.SetChecked(AUTO_VERSION_MENU_ITEM, _isEnabled);            
        }
        #endregion

        #region Private methods
        /// <summary>
        /// Method for constructing the new version string.
        /// </summary>
        /// <param name="oldVersionString">The old version string.</param>
        /// <returns>Returns a new version string with the last whole number increased by 1. Returns null if the old version string does not contain any numbers.</returns>
        private static string ConstructNewVersionString(string oldVersionString)
        {
            int lastIndex = -1;
            int firstIndex = -1;
            char[] characters = oldVersionString.ToCharArray();

            // find the first and last index of the last whole number in the version string
            for (int i = characters.Length - 1; i >= 0; i--)
            {
                if (char.IsDigit(characters[i]))
                {
                    if (lastIndex < 0)
                        lastIndex = i;

                    if (firstIndex < 0 || firstIndex > i)
                        firstIndex = i;
                }
                else if (lastIndex > 0 && firstIndex > 0)
                    break;
            }

            // version contains no numbers, so we return null
            if (firstIndex < 0 || lastIndex < 0)
                return null;

            int length = lastIndex - firstIndex + 1;
            int originalNumber = int.Parse(oldVersionString.Substring(firstIndex, length));
            int newNumber = originalNumber + 1;

            // replace the old number with the new one
            string newVersionString = oldVersionString.Remove(firstIndex, length);
            newVersionString = newVersionString.Insert(firstIndex, newNumber.ToString());

            // return the new version
            return newVersionString;
        }
        #endregion
    }
}
