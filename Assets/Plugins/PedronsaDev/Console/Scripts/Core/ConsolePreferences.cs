using TMPro;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PedronsaDev.Console
{
	public class ConsolePreferences : ScriptableObject
	{
		public TMP_FontAsset GlobalFont;

		[Range(0f, 1f)] public float Opacity;

		[Min(0f)] public float LogTextFontSize;
		[Min(0f)] public float AutocompleteTextFontSize;

		public Color LogTextColor;
		public Color CommandTextColor;
		public Color AutocompleteTextColor;
		public Color AutocompleteParameterTextColor;
		public Color WarningTextColor;
		public Color ErrorTextColor;
		public Color HighlightTextColor;

		public Color BackgroundMainColor;
		public Color BackgroundEvenColor;
		public Color BackgroundHoverTextColor;
		public Color BackgroundSelectionColor;
		public Color BackgroundHeaderColor;
		public Color BackgroundBottomColor;
		public Color BackgroundAutocompleteColor;
		public Color SeparatorColor;

		public bool SaveConsoleOutput;
		public bool CacheReloadOnPlayMode;
		public bool PersistConsole;
		public bool PauseGameOnConsole;

		public void Reset()
		{
			#if UNITY_EDITOR
			GlobalFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Plugins/PedronsaDev/Smart Console/Fonts/JetBrainsMono-Regular SDF.asset");
			#endif

			Opacity = 1.0f;

			LogTextFontSize = 28.0f;
			AutocompleteTextFontSize = 24.0f;

			LogTextColor = new Color(0.867f, 0.867f, 0.867f, 1.0f);
			CommandTextColor = new Color(0.02352941f, 0.6039216f, 1.0f, 1.0f);
			AutocompleteTextColor = new Color(0.867f, 0.867f, 0.867f, 1.0f);
			AutocompleteParameterTextColor = new Color(0.867f, 0.867f, 0.867f, 0.5f);
			WarningTextColor = new Color(0.9f, 0.8f, 0.02352941f, 1.0f);
			ErrorTextColor = new Color(1.0f, 0.2588235f, 0.2313726f, 1.0f);
			HighlightTextColor = new Color(0.996f, 0.706f, 0.0f, 1.0f);

			BackgroundMainColor = new Color(0.176f, 0.176f, 0.176f, 1.0f);
			BackgroundEvenColor = new Color(0.22f, 0.22f, 0.22f, 0.5f);
			BackgroundSelectionColor = new Color(0.173f, 0.365f, 0.529f, 0.5f);
			BackgroundAutocompleteColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);
			SeparatorColor = new Color(0.173f, 0.365f, 0.529f, 0.5f);

			SaveConsoleOutput = false;
			CacheReloadOnPlayMode = true;
			PersistConsole = false;
			PauseGameOnConsole = false;
		}
	}
}