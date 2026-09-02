using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PedronsaDev.Console
{
	/// <summary>
	/// Automatically updates and bakes the ConsoleCache ScriptableObject before any player build starts.
	/// Guarantees that standalone builds never miss newly added console commands.
	/// </summary>
	public class ConsoleBuildPreprocessor : IPreprocessBuildWithReport
	{
		public int callbackOrder => 0;

		public void OnPreprocessBuild(BuildReport report)
		{
			string[] guids = AssetDatabase.FindAssets("t:ConsoleCache");
			if (guids.Length == 0) return;

			foreach (var guid in guids)
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				var cache = AssetDatabase.LoadAssetAtPath<ConsoleCache>(path);
				if (cache != null)
				{
					Debug.Log($"[ConsoleBuildPreprocessor] Pre-baking console commands into: {path}");
					cache.Load();
				}
			}
		}
	}
}
