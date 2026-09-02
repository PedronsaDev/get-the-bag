
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace PedronsaDev.Console.Components
{
	public class UIBackgroundSetter : MonoBehaviour
	{
		[SerializeField] private ConsolePreferences _preferences;
		[SerializeField] CanvasGroup _canvasGroup;
		[SerializeField] Image[] _backgrounds;
		[SerializeField] Image _header;
		[SerializeField] Image _bottom;
		[SerializeField] Image[] _separators;

		private void Start()
		{
			_canvasGroup.alpha = _preferences.Opacity;

			foreach (var background in _backgrounds) 
				background.color = _preferences.BackgroundMainColor;
			
			foreach (var separator in _separators) 
				separator.color = _preferences.SeparatorColor;
			_header.color = _preferences.BackgroundHeaderColor;
			_bottom.color = _preferences.BackgroundBottomColor;
		}

		[Command("set_console_opacity", "Sets console opacity from 0 to 1")]
		public void SetConsoleOpacity(float value)
		{
			_canvasGroup.alpha = value;
			Console.Log($"Console Opacity set to {value}");
		}
	}
}