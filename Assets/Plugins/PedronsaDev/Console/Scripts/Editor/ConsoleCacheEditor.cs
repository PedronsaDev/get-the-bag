using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace PedronsaDev.Console
{
	[CustomEditor(typeof(ConsoleCache))]
	public class ConsoleCacheEditor : UnityEditor.Editor
	{
		private ConsoleCache _consoleCache;

		private void OnEnable()
		{
			_consoleCache = (ConsoleCache)target;
		}

		public override VisualElement CreateInspectorGUI()
		{
			var root = new VisualElement();
			root.style.paddingTop = 6;
			root.style.paddingBottom = 6;
			root.style.paddingLeft = 4;
			root.style.paddingRight = 4;

			// Title Box
			var titleBox = new VisualElement();
			titleBox.style.backgroundColor = new UnityEngine.Color(0.18f, 0.18f, 0.18f);
			titleBox.style.paddingTop = 8;
			titleBox.style.paddingBottom = 8;
			titleBox.style.paddingLeft = 10;
			titleBox.style.paddingRight = 10;
			titleBox.style.marginBottom = 10;
			titleBox.style.borderTopLeftRadius = 4;
			titleBox.style.borderTopRightRadius = 4;
			titleBox.style.borderBottomLeftRadius = 4;
			titleBox.style.borderBottomRightRadius = 4;

			var titleLabel = new Label("PedronsaDev Console Cache");
			titleLabel.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
			titleLabel.style.fontSize = 14;
			titleLabel.style.color = new UnityEngine.Color(0.9f, 0.9f, 0.9f);
			titleBox.Add(titleLabel);

			int commandCount = _consoleCache.CommandRegistry != null ? _consoleCache.CommandRegistry.Length : 0;
			var statusLabel = new Label($"Total Cached Commands: {commandCount}");
			statusLabel.style.fontSize = 11;
			statusLabel.style.color = new UnityEngine.Color(0.7f, 0.7f, 0.7f);
			statusLabel.style.marginTop = 2;
			titleBox.Add(statusLabel);
			root.Add(titleBox);

			// Default serialized fields
			var showLogsProperty = serializedObject.FindProperty("ShowLogs");
			if (showLogsProperty != null)
			{
				var showLogsField = new PropertyField(showLogsProperty);
				root.Add(showLogsField);
			}

			var preferencesProperty = serializedObject.FindProperty("m_Preferences");
			if (preferencesProperty != null)
			{
				var preferencesField = new PropertyField(preferencesProperty);
				preferencesField.style.marginTop = 4;
				root.Add(preferencesField);
			}

			// Buttons Row
			var buttonContainer = new VisualElement();
			buttonContainer.style.flexDirection = FlexDirection.Row;
			buttonContainer.style.marginTop = 12;

			var loadButton = new Button(() =>
			{
				_consoleCache.Load();
				int count = _consoleCache.CommandRegistry != null ? _consoleCache.CommandRegistry.Length : 0;
				statusLabel.text = $"Total Cached Commands: {count}";
			})
			{
				text = "Load / Refresh Cache"
			};
			loadButton.style.flexGrow = 1;
			loadButton.style.height = 28;
			buttonContainer.Add(loadButton);

			var clearButton = new Button(() =>
			{
				_consoleCache.Clear();
				statusLabel.text = "Total Cached Commands: 0";
			})
			{
				text = "Clear Cache"
			};
			clearButton.style.flexGrow = 1;
			clearButton.style.height = 28;
			clearButton.style.marginLeft = 6;
			buttonContainer.Add(clearButton);

			root.Add(buttonContainer);

			return root;
		}
	}
}