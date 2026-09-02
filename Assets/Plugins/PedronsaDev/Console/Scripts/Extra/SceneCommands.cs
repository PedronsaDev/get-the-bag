
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PedronsaDev.Console.Extra
{
	[CommandGroup("Scene")]
	public static class SceneCommands
	{
		[Command("reload_active_scene", "Reloads the current active scene")]
		public static void ReloadActiveScene()
		{
			SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

			AsyncOperation operation = SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex);

			operation.completed += (asyncOperation) =>
			{
				Console.Log($"Reloads of active scene is completed.");
			};
		}

		[Command("load_scene", "Loads a scene by name")]
		public static void LoadScene(string sceneName)
		{
			AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

			operation.completed += (asyncOperation) =>
			{
				Console.Log($"Loads of scene {sceneName} is completed.");
			};
		}

		[Command("load_scene_index", "Loads a scene by index")]
		public static void LoadSceneIndex(int sceneIndex)
		{
			AsyncOperation operation = SceneManager.LoadSceneAsync(sceneIndex);

			operation.completed += (asyncOperation) =>
			{
				Console.Log($"Loads of scene index {sceneIndex} is completed.");
			};
		}

		[Command("unload_scene", "Unloads a scene by name")]
		public static void UnloadScene(string sceneName)
		{
			AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

			operation.completed += (asyncOperation) =>
			{
				Console.Log($"Unloads of scene {sceneName} is completed.");
			};
		}

		[Command("unload_scene_index", "Unloads a scene by index")]
		public static void UnloadSceneIndex(int sceneIndex)
		{
			AsyncOperation operation = SceneManager.LoadSceneAsync(sceneIndex);

			operation.completed += (asyncOperation) =>
			{
				Console.Log($"Unloads of scene index {sceneIndex} is completed.");
			};
		}

		[Command("get_all_scene", "Gets the name and index of every scene included in the build")]
		public static void GetAllScenes()
		{
			string sceneInfos = "";

			for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
			{
				Scene scene = SceneManager.GetSceneByBuildIndex(i);
				sceneInfos += $"- {scene.name} ({scene.buildIndex})\r\n";
			}

			Console.Log($"List of all scenes :\r\n{sceneInfos}");
		}

		[Command("get_all_loaded_scenes", "Gets the name and index of every scene currently loaded")]
		public static void GetAllLoadedScenes()
		{
			string sceneInfos = "";

			for (int i = 0; i < SceneManager.sceneCount; i++)
			{
				Scene scene = SceneManager.GetSceneAt(i);
				sceneInfos += $"- {scene.name} ({scene.buildIndex})\r\n";
			}

			Console.Log($"List of all loaded scenes :\r\n{sceneInfos}");
		}

		[Command("get_active_scene", "Gets the name and index of the current active scene")]
		public static void GetActiveScene()
		{
			Scene scene = SceneManager.GetActiveScene();
			Console.Log($"Active scene is :{scene.name} ({scene.buildIndex})"); 
		}

		[Command("set_active_scene", "Sets the active scene by name")]
		public static void SetActiveScene(string sceneName)
		{
			try
			{
				SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));
			}
			catch (ArgumentException)
			{
				Console.LogWarning($"Scene {sceneName} is invalid.");
			}
		}
	}
}
