using UnityEngine;
using UnityEngine.UI;

namespace PedronsaDev.Console.Components
{
	public class UIBackgroundSetter : MonoBehaviour
	{
		[SerializeField] private ConsolePreferences _preferences;
		[SerializeField] private CanvasGroup _canvasGroup;
		[SerializeField] private Image[] _backgrounds;
		[SerializeField] private Image _header;
		[SerializeField] private Image _bottom;
		[SerializeField] private Image[] _separators;

		private void Start()
		{
			ApplyVisuals();
		}

		private void OnEnable()
		{
			if (_preferences != null)
			{
				_preferences.OnPreferencesChanged += ApplyVisuals;
			}
		}

		private void OnDisable()
		{
			if (_preferences != null)
			{
				_preferences.OnPreferencesChanged -= ApplyVisuals;
			}
		}

		public void ApplyVisuals()
		{
			if (_preferences == null) return;

			if (_canvasGroup != null)
			{
				_canvasGroup.alpha = _preferences.Opacity;
			}

			if (_backgrounds != null)
			{
				foreach (var background in _backgrounds)
				{
					if (background != null) background.color = _preferences.BackgroundMainColor;
				}
			}

			if (_separators != null)
			{
				foreach (var separator in _separators)
				{
					if (separator != null) separator.color = _preferences.SeparatorColor;
				}
			}

			if (_header != null) _header.color = _preferences.BackgroundHeaderColor;
			if (_bottom != null) _bottom.color = _preferences.BackgroundBottomColor;
		}

		[Command("set_console_opacity", "Sets console opacity from 0.1 to 1.0", Aliases = new[] { "opacity" })]
		public void SetConsoleOpacity(float value)
		{
			if (_preferences == null) return;
			_preferences.Opacity = Mathf.Clamp(value, 0.1f, 1.0f);
			_preferences.NotifyPreferencesChanged();
			Console.Log($"[Theme] Console Opacity set to {_preferences.Opacity:0.00}");
		}

		[Command("set_theme", "Changes console theme preset: default, matrix, cyberpunk, dracula, amber", Aliases = new[] { "theme" })]
		public void SetTheme(string themeName)
		{
			if (_preferences == null) return;

			switch (themeName.ToLowerInvariant())
			{
				case "matrix":
					_preferences.ApplyPreset(ThemePreset.Matrix);
					Console.Log("[Theme] Switched to Matrix Green theme.");
					break;
				case "cyberpunk":
				case "neon":
					_preferences.ApplyPreset(ThemePreset.Cyberpunk);
					Console.Log("[Theme] Switched to Cyberpunk Neon theme.");
					break;
				case "dracula":
					_preferences.ApplyPreset(ThemePreset.Dracula);
					Console.Log("[Theme] Switched to Dracula theme.");
					break;
				case "amber":
				case "retro":
				case "crt":
					_preferences.ApplyPreset(ThemePreset.Amber);
					Console.Log("[Theme] Switched to Retro Amber theme.");
					break;
				default:
					_preferences.ApplyPreset(ThemePreset.DefaultDark);
					Console.Log("[Theme] Switched to Default Dark theme.");
					break;
			}
		}
	}
}