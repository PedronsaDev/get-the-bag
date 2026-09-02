using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using PedronsaDev.Console.Utils;
using Cysharp.Threading.Tasks;

namespace PedronsaDev.Console.Components
{
	[RequireComponent(typeof(ConsoleInputHandler))]
	public class ConsoleSystem : MonoBehaviour
	{
		[SerializeField] private ConsoleCache m_Cache;
		[SerializeField] private ConsolePreferences m_Preferences;
		[SerializeField] private Canvas m_Console;
		[SerializeField] private bool m_ShowApplicationLogs;
		[SerializeField] private bool m_OpenAtStart;
		[SerializeField] private bool m_OpenUnlockCursor;
		[SerializeField] private bool m_CloseLockCursor;

		[SerializeField] private TMP_InputField m_InputField;

		private CanvasGroup m_canvasGroup;
		private bool _pauseGameOnConsole;

		#region Events

		public event Action OnActivate;
		public event Action OnDeactivate;

		#endregion

		private ConsoleInputHandler m_InputHandler;

		private int m_LogMessageCopiedIndex;
		
		[Command("pause_game_on_open_console","Choose if opening the console the game will pause the game")]
		public void SetPauseGameOnConsole(bool pause) => _pauseGameOnConsole = pause;
		
		public bool ShowApplicationLogs
		{
			get => m_ShowApplicationLogs;
			set
			{
				if (m_ShowApplicationLogs == value)
				{
					return;
				}

				m_ShowApplicationLogs = value;

				if (m_ShowApplicationLogs)
				{
					Application.logMessageReceived += SubmitLog;
				}
				else
				{
					Application.logMessageReceived -= SubmitLog;
				}
			}
		}

		private void Awake()
		{
			m_InputHandler = gameObject.GetComponent<ConsoleInputHandler>();
			m_canvasGroup = m_Console.GetComponent<CanvasGroup>();
			Console.LogMessages.Clear();
			Console.AutocompletionLogMessages.Clear();
			Console.LogMessagesHistory.Clear();

			m_LogMessageCopiedIndex = -1;

			if (m_Preferences.PersistConsole) 
				DontDestroyOnLoad(this);

			_pauseGameOnConsole = m_Preferences.PauseGameOnConsole;

#if !UNITY_EDITOR
			//SmartConsole.RefreshAutocomplete();
#endif
		}

		private void OnEnable()
		{
			m_InputHandler.OnOpenCloseInput += ToggleConsole;
			m_InputHandler.OnCopyNextLogInput += CopyNextSentLogMessage;
			m_InputHandler.OnCopyPreviousLogInput += CopyPreviousSentLogMessage;

			Console.OnSubmit += EnsureCommandExecution;

			if (m_ShowApplicationLogs)
			{
				Application.logMessageReceived += SubmitLog;
			}

			if (m_OpenUnlockCursor)
			{
				OnActivate += UnlockCursor;
			}

			if (m_CloseLockCursor)
			{
				OnDeactivate += LockCursor;
			}
		}
		
		private void OnDisable()
		{
			m_InputHandler.OnOpenCloseInput -= ToggleConsole;
			m_InputHandler.OnCopyNextLogInput -= CopyNextSentLogMessage;
			m_InputHandler.OnCopyPreviousLogInput -= CopyPreviousSentLogMessage;

			Console.OnSubmit -= EnsureCommandExecution;
			Application.logMessageReceived -= SubmitLog;
			OnActivate -= UnlockCursor;
			OnDeactivate -= LockCursor;
		}

		private void Start()
		{
			if (m_OpenAtStart)
			{
				m_InputHandler.ToggleInputDispatcher();
			}
		}

		/// <summary>
		/// Find command that fits a specific text
		/// </summary>
		/// <param name="text">specific text</param>
		private Command FindCommandByText(string text)
		{
			Command command = null;

			for (int i = 0; i < m_Cache.CommandRegistry.Length && command == null; i++)
			{
				if (string.Equals(m_Cache.CommandRegistry[i].Name.ToLower(), text.ToLower()))
				{
					// found command occurence
					command = m_Cache.CommandRegistry[i];
				}
			}

			return command;
		}

		/// <summary>
		/// Submit the command that fits the input
		/// </summary>
		/// <param name="inputText">text field</param>
		private void EnsureCommandExecution(string inputText)
		{
			if (string.IsNullOrEmpty(inputText))
			{
				return;
			}
			
			// find the command by input text (supporting quoted multi-word strings)
			string[] inputParts = StringUtil.TokenizeInput(inputText);
			Command command = FindCommandByText(inputParts[0]);

			// create and log the log message
			LogMessage logMessage = new LogMessage(inputText,"" ,LogMessageType.Command);
			Console.LogMessagesHistory.Insert(0, logMessage);
			Console.LogCommand(logMessage);

			// try to execute the command if exists
			if (command != null)
			{
				if (command.HostOnly && Console.IsHostChecker != null && !Console.IsHostChecker())
				{
					Console.LogWarning($"[Console] Command '{command.Name}' requires Host privileges.");
					Console.Reset();
					m_LogMessageCopiedIndex = -1;
					return;
				}

				if (command.Method.GetParameters().Length > 0)
				{
					Exception exception = null;
					object[] targets = null;

					// get the parameters
					object[] parameters = null;
					string[] paramParts = new string[inputParts.Length - 1];
					Array.Copy(inputParts, 1, paramParts, 0, paramParts.Length);

					try
					{
						targets = FindCommandTargets(command);

						// cast the param into their params
						command.GetParameters(paramParts, out parameters);
					}
					catch (Exception e)
					{
						exception = e;
					}

					if (exception == null)
					{
						// execute command with parameter
						ExecuteCommandOnTargets(command, targets, parameters);
					}
					else
					{
						Console.LogError(exception.Message);
					}
				}
				else
				{
					Exception exception = null;
					object[] targets = null;

					try
					{
						targets = FindCommandTargets(command);

						if (inputParts.Length > 1)
						{
							// too many parameters
							exception = new SmartParameterTooManyException(command, inputParts.Length - 1);
						}
					}
					catch (Exception e)
					{
						exception = e;
					}

					if (exception == null)
					{
						// execute command without parameter
						ExecuteCommandOnTargets(command, targets);
					}
					else
					{
						Console.LogError(exception.Message);
					}
				}
			}
			else
			{
				Console.LogError($"Error: Command '{inputText}' could not be found.");
			}

			Console.Reset();
			m_LogMessageCopiedIndex = -1;
		}

		/// <summary>
		/// Find the command targets according to its MonoTargetType
		/// </summary>
		/// <param name="command">the command</param>
		/// <returns>object array of target</returns>
		private object[] FindCommandTargets(Command command)
		{
			if (command.Method.IsStatic)
			{
				return null;
			}

			// find target(s)
			List<object> targets = new List<object>();

			if (command.TargetType == MonoTargetType.Single)
			{
#if UNITY_2022_3_OR_NEWER
				targets.Add(FindAnyObjectByType(command.Method.DeclaringType));
#else
				targets.Add(FindObjectOfType(command.Method.DeclaringType));
#endif
			}
			else if (command.TargetType == MonoTargetType.Active)
			{
#if UNITY_2022_3_OR_NEWER
				targets.AddRange(FindObjectsByType(command.Method.DeclaringType, FindObjectsInactive.Exclude));
#else
				targets.AddRange(FindObjectsOfType(command.Method.DeclaringType, false));
#endif
			}
			else if (command.TargetType == MonoTargetType.All)
			{
#if UNITY_2022_3_OR_NEWER
				targets.AddRange(FindObjectsByType(command.Method.DeclaringType, FindObjectsInactive.Include));
#else
				targets.AddRange(FindObjectsOfType(command.Method.DeclaringType, true));
#endif
			}

			if (targets.Count == 0)
			{
				throw new SmartCommandNoTargetException(command);
			}

			if (targets.Count == 1 && targets[0] == null)
			{
				throw new SmartCommandNoTargetException(command);
			}

			return targets.ToArray();
		}

		/// <summary>
		/// Execute command on targets
		/// </summary>
		/// <param name="command">the command to execute</param>
		/// <param name="targets">the targets on which to execute</param>
		/// <param name="parameters">the command params if there is</param>
		private void ExecuteCommandOnTargets(Command command, object[] targets, object[] parameters = null)
		{
			if (command.Method.IsStatic)
			{
				// command is static and must not have target
				var result = command.Use(null, parameters);
				HandleCommandResult(result);
				return;
			}

			// use command on target(s)
			for (int i = 0; i < targets.Length; i++)
			{
				var result = command.Use(targets[i], parameters);
				HandleCommandResult(result);
			}
		}

		private void HandleCommandResult(object result)
		{
			if (result == null) return;

			if (result is IEnumerator coroutine)
			{
				StartCoroutine(coroutine);
			}
			else if (result is UniTask uniTask)
			{
				uniTask.Forget(ex => Console.LogError($"[Async Command Error] {ex.Message}"));
			}
			else if (result is System.Threading.Tasks.Task task)
			{
				task.ContinueWith(t =>
				{
					if (t.IsFaulted && t.Exception != null)
					{
						Console.LogError($"[Async Command Error] {t.Exception.InnerException?.Message ?? t.Exception.Message}");
					}
				}, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
			}
			else
			{
				Console.Log($"=> {result}");
			}
		}

		/// <summary>
		/// Submit the command as application log message
		/// </summary>
		private void SubmitLog(string logString, string stackTrace, LogType type)
		{
			LogMessageType messagetype = type switch
			{
				LogType.Log => LogMessageType.Log,
				LogType.Error => LogMessageType.Error,
				LogType.Assert => LogMessageType.Error,
				LogType.Warning => LogMessageType.Warning,
				LogType.Exception => LogMessageType.Error,
				_ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
			};
			
			LogMessage logMessage = new LogMessage(logString,stackTrace, messagetype);
			Console.LogCommand(logMessage);
		}

		/// <summary>
		/// Copy the next sent log message into the input.
		/// Move the caret after a frame to go over MoveUp input field's function call
		/// </summary>
		private void CopyNextSentLogMessage()
		{
			CopyLogMessageIndex(1);
			StartCoroutine(Console.MoveCaretCoroutine());
		}

		/// <summary>
		/// Copy the previous sent log message into the input
		/// </summary>
		private void CopyPreviousSentLogMessage() => CopyLogMessageIndex(-1);

		/// <summary>
		/// Copy log message history index
		/// </summary>
		/// <param name="addToIndex">the index to add to the current copy index</param>
		private void CopyLogMessageIndex(int addToIndex)
		{
			if (Console.LogMessagesHistory.Count == 0)
			{
				return;
			}

			m_LogMessageCopiedIndex += addToIndex;

			if (m_LogMessageCopiedIndex < 0)
			{
				m_LogMessageCopiedIndex = -1;
				Console.SetInputText("", true);
				return;
			}

			if (m_LogMessageCopiedIndex > Console.LogMessagesHistory.Count - 1)
			{
				m_LogMessageCopiedIndex = Console.LogMessagesHistory.Count - 1;
				return;
			}

			Console.Reset();
			Console.SetInputText(Console.LogMessagesHistory[m_LogMessageCopiedIndex].Text, true);
		}

		/// <summary>
		/// Open the console if disabled or close it if enabled
		/// </summary>
		private void ToggleConsole()
		{
			GameObject ui = m_Console.gameObject;
			ui.SetActive(!ui.activeInHierarchy);

			if (ui.activeInHierarchy)
			{
				// console canvas is enabled
				Console.ActivateInputField();
				StartCoroutine(WaitToSelectField());

				string inputFieldText = Console.GetInputText();

				if (!string.IsNullOrEmpty(inputFieldText))
				{
					Console.SetInputText(inputFieldText.Remove(inputFieldText.Length - 1));
				}
				m_canvasGroup.interactable = true;
				m_canvasGroup.blocksRaycasts = true;
				
				Console.NotifyToggle(true);
				OnActivate?.Invoke();
			}
			else
			{
				// console canvas is disabled
				Console.Reset();
				
				EventSystem.current.SetSelectedGameObject(null);
				m_LogMessageCopiedIndex = -1;
				m_canvasGroup.interactable = false;
				m_canvasGroup.blocksRaycasts = false;
				
				Console.NotifyToggle(false);
				OnDeactivate?.Invoke();
			}
		}

		private IEnumerator WaitToSelectField()
		{
			yield return 0;
			m_InputField.Select();
		}

		/// <summary>
		/// Lock the cursor and hide it
		/// </summary>
		private void LockCursor()
		{
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
		}

		/// <summary>
		/// Unlock the cursor and show it
		/// </summary>
		private void UnlockCursor()
		{
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
		}

		/// <summary>
		/// Clear for the button's UnityEvent assignation
		/// </summary>
		public void ClearDispatcher()
		{
			Console.Clear();
		}

		[Command("get_commands", "Gets available commands")]
		private void GetCommands()
		{
			string commands = "";

			for (int i = 0; i < m_Cache.AvailableCommands.Count; i++)
			{
				Command command = m_Cache.AvailableCommands[i];
				commands += $"- {command.Name}";

				if (!string.IsNullOrEmpty(command.Description))
				{
					commands += $": {command.Description}";
				}

				commands += "\r\n";
			}

			Console.Log($"List of available commands ({m_Cache.AvailableCommands.Count}):\r\n{commands}");
		}

		[Command("get_commands_from_group", "Gets available commands from a group")]
		private void GetCommandsFromGroup(string group = "Default")
		{
			List<Command> availableCommandsFromGroup = m_Cache.AvailableCommands.Where(cmd => cmd.Group.Equals(group)).ToList();

			if (availableCommandsFromGroup.Count == 0)
			{
				Console.Log($"No available commands from group '{group}'");
				return;
			}

			string commands = "";

			for (int i = 0; i < availableCommandsFromGroup.Count; i++)
			{
				Command command = m_Cache.AvailableCommands[i];

				commands += $"- {command.Name}";

				if (!string.IsNullOrEmpty(command.Description))
				{
					commands += $": {command.Description}";
				}

				commands += "\r\n";
			}

			Console.Log($"List of available commands from group ({availableCommandsFromGroup.Count}):\r\n{commands}");
		}

		[Command("get_all_commands", "Gets all commands")]
		private void GetAllCommands()
		{
			string commands = "";

			for (int i = 0; i < m_Cache.CommandRegistry.Length; i++)
			{
				Command command = m_Cache.CommandRegistry[i];
				commands += $"- {command.Name}";

				if (!string.IsNullOrEmpty(command.Description))
				{
					commands += $": {command.Description}";
				}

				commands += "\r\n";
			}

			Console.Log($"List of all commands ({m_Cache.CommandRegistry.Length}):\r\n{commands}");
		}

		private void SaveOutput()
		{
			string date = DateTime.Now.ToString("yyyy-MM-dd_HH-mm");
			string fileName = $"console-output-{date}.txt";
			string filePath = Path.Combine(Application.persistentDataPath, fileName);

			try
			{
				// Save output to the specified file path
				IEnumerable<string> lines = Console.LogMessages.Select(lm => (string)lm);
				File.WriteAllLines(filePath, lines);
				Console.Log($"Console output saved to {filePath}.\nPath has been copied to clipboard.");
				GUIUtility.systemCopyBuffer = filePath;
			}
			catch (Exception e)
			{
				Console.LogWarning(e.Message);
			}
		}

		private void OnApplicationQuit()
		{
			if (m_Preferences.SaveConsoleOutput)
			{
				SaveOutput();
			}
		}
	}
}
