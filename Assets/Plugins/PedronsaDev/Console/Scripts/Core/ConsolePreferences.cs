using System;
using TMPro;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PedronsaDev.Console
{
	public enum ThemePreset
	{
		DefaultDark,
		Matrix,
		Cyberpunk,
		Dracula,
		Amber
	}

	[Serializable]
	public struct ConsoleFontEntry
	{
		public string Name;
		public TMP_FontAsset FontAsset;

		public ConsoleFontEntry(string name, TMP_FontAsset fontAsset)
		{
			Name = name;
			FontAsset = fontAsset;
		}
	}

	public class ConsolePreferences : ScriptableObject
	{
		public TMP_FontAsset GlobalFont;
		public System.Collections.Generic.List<ConsoleFontEntry> AvailableFonts = new();

		/// <summary>
		/// Attempts to switch the active font by name (case-insensitive).
		/// </summary>
		public bool TrySetFont(string fontName)
		{
			if (string.IsNullOrWhiteSpace(fontName)) return false;

			for (int i = 0; i < AvailableFonts.Count; i++)
			{
				var entry = AvailableFonts[i];
				if (entry.FontAsset && (entry.Name.Equals(fontName, StringComparison.OrdinalIgnoreCase) ||
				                        entry.FontAsset.name.Equals(fontName, StringComparison.OrdinalIgnoreCase)))
				{
					GlobalFont = entry.FontAsset;
					NotifyPreferencesChanged();
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Registers a new font into the available fonts catalog.
		/// </summary>
		public void RegisterFont(string name, TMP_FontAsset fontAsset)
		{
			if (!fontAsset) return;

			for (int i = 0; i < AvailableFonts.Count; i++)
			{
				if (AvailableFonts[i].FontAsset == fontAsset || AvailableFonts[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
				{
					AvailableFonts[i] = new ConsoleFontEntry(name, fontAsset);
					return;
				}
			}

			AvailableFonts.Add(new ConsoleFontEntry(name, fontAsset));
		}

		[Range(0.1f, 1f)] public float Opacity = 1.0f;

		[Min(8f)] public float LogTextFontSize = 24.0f;
		[Min(8f)] public float InputTextFontSize = 24.0f;
		[Min(8f)] public float AutocompleteTextFontSize = 20.0f;

		[Header("Text Colors")]
		public Color LogTextColor = new(0.87f, 0.87f, 0.87f, 1.0f);
		public Color CommandTextColor = new(0.02f, 0.60f, 1.0f, 1.0f);
		public Color AutocompleteTextColor = new(0.87f, 0.87f, 0.87f, 1.0f);
		public Color AutocompleteParameterTextColor = new(0.87f, 0.87f, 0.87f, 0.5f);
		public Color WarningTextColor = new(0.95f, 0.80f, 0.02f, 1.0f);
		public Color ErrorTextColor = new(1.0f, 0.26f, 0.23f, 1.0f);
		public Color HighlightTextColor = new(1.0f, 0.71f, 0.0f, 1.0f);

		[Header("Background & Frame Colors")]
		public Color BackgroundMainColor = new(0.12f, 0.12f, 0.12f, 0.95f);
		public Color BackgroundEvenColor = new(0.16f, 0.16f, 0.16f, 0.4f);
		public Color BackgroundOddColor = new(0.0f, 0.0f, 0.0f, 0.0f);
		public Color BackgroundHoverTextColor = new(0.2f, 0.2f, 0.2f, 1.0f);
		public Color BackgroundSelectionColor = new(0.17f, 0.36f, 0.53f, 0.5f);
		public Color BackgroundHeaderColor = new(0.08f, 0.08f, 0.08f, 1.0f);
		public Color BackgroundBottomColor = new(0.10f, 0.10f, 0.10f, 1.0f);
		public Color BackgroundAutocompleteColor = new(0.10f, 0.12f, 0.15f, 0.95f);
		public Color SeparatorColor = new(0.17f, 0.36f, 0.53f, 0.5f);

		[Header("Engine & Session Settings")]
		public bool SaveConsoleOutput = false;
		public bool CacheReloadOnPlayMode = true;
		public bool PersistConsole = true;
		public bool PauseGameOnConsole = false;

		public event Action OnPreferencesChanged;

		public void NotifyPreferencesChanged()
		{
			OnPreferencesChanged?.Invoke();
		}

		public void ApplyPreset(ThemePreset preset)
		{
			switch (preset)
			{
				case ThemePreset.DefaultDark:
					BackgroundMainColor = new Color(0.12f, 0.12f, 0.12f, 0.95f);
					BackgroundEvenColor = new Color(0.16f, 0.16f, 0.16f, 0.4f);
					BackgroundOddColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);
					BackgroundHeaderColor = new Color(0.08f, 0.08f, 0.08f, 1.0f);
					BackgroundBottomColor = new Color(0.10f, 0.10f, 0.10f, 1.0f);
					BackgroundAutocompleteColor = new Color(0.10f, 0.12f, 0.15f, 0.95f);
					SeparatorColor = new Color(0.17f, 0.36f, 0.53f, 0.5f);
					CommandTextColor = new Color(0.02f, 0.60f, 1.0f, 1.0f);
					LogTextColor = new Color(0.87f, 0.87f, 0.87f, 1.0f);
					WarningTextColor = new Color(0.95f, 0.80f, 0.02f, 1.0f);
					ErrorTextColor = new Color(1.0f, 0.26f, 0.23f, 1.0f);
					HighlightTextColor = new Color(1.0f, 0.71f, 0.0f, 1.0f);
					break;

				case ThemePreset.Matrix:
					BackgroundMainColor = new Color(0.04f, 0.07f, 0.04f, 0.96f);
					BackgroundEvenColor = new Color(0.06f, 0.10f, 0.06f, 0.5f);
					BackgroundOddColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);
					BackgroundHeaderColor = new Color(0.02f, 0.04f, 0.02f, 1.0f);
					BackgroundBottomColor = new Color(0.03f, 0.05f, 0.03f, 1.0f);
					BackgroundAutocompleteColor = new Color(0.03f, 0.08f, 0.04f, 0.95f);
					SeparatorColor = new Color(0.0f, 0.8f, 0.3f, 0.6f);
					CommandTextColor = new Color(0.0f, 1.0f, 0.4f, 1.0f);
					LogTextColor = new Color(0.4f, 0.95f, 0.5f, 1.0f);
					WarningTextColor = new Color(0.8f, 1.0f, 0.2f, 1.0f);
					ErrorTextColor = new Color(1.0f, 0.2f, 0.3f, 1.0f);
					HighlightTextColor = new Color(0.2f, 1.0f, 0.6f, 1.0f);
					break;

				case ThemePreset.Cyberpunk:
					BackgroundMainColor = new Color(0.08f, 0.04f, 0.15f, 0.96f);
					BackgroundEvenColor = new Color(0.12f, 0.06f, 0.22f, 0.5f);
					BackgroundOddColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);
					BackgroundHeaderColor = new Color(0.05f, 0.02f, 0.10f, 1.0f);
					BackgroundBottomColor = new Color(0.06f, 0.03f, 0.12f, 1.0f);
					BackgroundAutocompleteColor = new Color(0.14f, 0.05f, 0.20f, 0.95f);
					SeparatorColor = new Color(1.0f, 0.0f, 0.45f, 0.8f);
					CommandTextColor = new Color(0.0f, 0.95f, 1.0f, 1.0f);
					LogTextColor = new Color(0.88f, 0.82f, 1.0f, 1.0f);
					WarningTextColor = new Color(1.0f, 0.85f, 0.0f, 1.0f);
					ErrorTextColor = new Color(1.0f, 0.0f, 0.35f, 1.0f);
					HighlightTextColor = new Color(1.0f, 0.0f, 0.55f, 1.0f);
					break;

				case ThemePreset.Dracula:
					BackgroundMainColor = new Color(0.16f, 0.16f, 0.21f, 0.96f);
					BackgroundEvenColor = new Color(0.21f, 0.22f, 0.27f, 0.5f);
					BackgroundOddColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);
					BackgroundHeaderColor = new Color(0.12f, 0.12f, 0.16f, 1.0f);
					BackgroundBottomColor = new Color(0.13f, 0.13f, 0.17f, 1.0f);
					BackgroundAutocompleteColor = new Color(0.14f, 0.14f, 0.19f, 0.95f);
					SeparatorColor = new Color(0.38f, 0.45f, 0.64f, 0.7f);
					CommandTextColor = new Color(0.74f, 0.58f, 0.98f, 1.0f);
					LogTextColor = new Color(0.97f, 0.97f, 0.95f, 1.0f);
					WarningTextColor = new Color(0.95f, 0.98f, 0.55f, 1.0f);
					ErrorTextColor = new Color(1.0f, 0.33f, 0.33f, 1.0f);
					HighlightTextColor = new Color(1.0f, 0.47f, 0.78f, 1.0f);
					break;

				case ThemePreset.Amber:
					BackgroundMainColor = new Color(0.10f, 0.07f, 0.03f, 0.96f);
					BackgroundEvenColor = new Color(0.14f, 0.10f, 0.04f, 0.5f);
					BackgroundOddColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);
					BackgroundHeaderColor = new Color(0.06f, 0.04f, 0.02f, 1.0f);
					BackgroundBottomColor = new Color(0.07f, 0.05f, 0.02f, 1.0f);
					BackgroundAutocompleteColor = new Color(0.12f, 0.08f, 0.03f, 0.95f);
					SeparatorColor = new Color(1.0f, 0.60f, 0.0f, 0.7f);
					CommandTextColor = new Color(1.0f, 0.70f, 0.0f, 1.0f);
					LogTextColor = new Color(1.0f, 0.80f, 0.40f, 1.0f);
					WarningTextColor = new Color(1.0f, 0.90f, 0.50f, 1.0f);
					ErrorTextColor = new Color(1.0f, 0.20f, 0.0f, 1.0f);
					HighlightTextColor = new Color(1.0f, 0.60f, 0.0f, 1.0f);
					break;
			}

			NotifyPreferencesChanged();
		}

		public void Reset()
		{
#if UNITY_EDITOR
			GlobalFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Plugins/PedronsaDev/Console/Fonts/JetBrainsMono-Regular SDF.asset");
#endif
			ApplyPreset(ThemePreset.DefaultDark);
			Opacity = 1.0f;
			LogTextFontSize = 24.0f;
			InputTextFontSize = 24.0f;
			AutocompleteTextFontSize = 20.0f;
			SaveConsoleOutput = false;
			CacheReloadOnPlayMode = true;
			PersistConsole = true;
			PauseGameOnConsole = false;
			NotifyPreferencesChanged();
		}
	}
}