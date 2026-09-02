using UnityEngine;
using UnityEditor;

namespace PedronsaDev.Console
{
	[CustomEditor(typeof(ConsoleCache))]
	public class ConsoleCacheEditor : UnityEditor.Editor
	{
		private ConsoleCache m_ConsoleCache;

		private void OnEnable()
		{
			m_ConsoleCache = (ConsoleCache)target;
		}

		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			if (m_ConsoleCache != null)
			{
				EditorGUILayout.Space();

				GUILayout.BeginHorizontal();

				if (GUILayout.Button("Load"))
				{
					m_ConsoleCache.Load();
				}

				if (GUILayout.Button("Clear"))
				{
					m_ConsoleCache.Clear();
				}

				GUILayout.EndHorizontal();
			}
		}
	}
}