using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PedronsaDev.Console.Utils
{
	public static class StringUtil
	{
		/// <summary>
		/// Tokenizes an input string into arguments, properly preserving multi-word strings wrapped in double quotes.
		/// Example: 'host_scene "Mission 01 - Mines" 10' -> ['host_scene', 'Mission 01 - Mines', '10']
		/// </summary>
		public static string[] TokenizeInput(string input)
		{
			if (string.IsNullOrWhiteSpace(input))
			{
				return Array.Empty<string>();
			}

			var tokens = new List<string>();
			bool inQuotes = false;
			var current = new StringBuilder();

			for (int i = 0; i < input.Length; i++)
			{
				char c = input[i];

				if (c == '\"')
				{
					inQuotes = !inQuotes;
				}
				else if (c == ' ' && !inQuotes)
				{
					if (current.Length > 0)
					{
						tokens.Add(current.ToString());
						current.Clear();
					}
				}
				else
				{
					current.Append(c);
				}
			}

			if (current.Length > 0)
			{
				tokens.Add(current.ToString());
			}

			return tokens.ToArray();
		}

		/// <summary>
		/// Parses a string to an integer.
		/// </summary>
		public static int ToInt(string input)
		{
			if (int.TryParse(input, out int result))
			{
				return result;
			}

			throw new InvalidCastException($"Cannot cast '{input}' to int.");
		}

		/// <summary>
		/// Parses a string to a float.
		/// </summary>
		public static float ToFloat(string input)
		{
			input = input.Replace(".", System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);

			if (float.TryParse(input, out float result))
			{
				return result;
			}

			throw new InvalidCastException($"Cannot cast '{input}' to float.");
		}

		/// <summary>
		/// Parses a string to a boolean.
		/// </summary>
		public static bool ToBool(string input)
		{
			if (bool.TryParse(input, out bool result))
			{
				return result;
			}

			throw new InvalidCastException($"Cannot cast '{input}' to bool.");
		}

		/// <summary>
		/// Parses a string to an enumeration.
		/// </summary>
		public static Enum ToEnum(string input, Type type)
		{
			int pointCount = input.Count(c => c == '.');

			if (pointCount > 1)
			{
				throw new InvalidCastException();
			}

			if (pointCount == 1)
			{
				input = input.Substring(input.IndexOf('.') + 1);
			}

			string[] enumNames = Enum.GetNames(type);
			Array enumValues = Enum.GetValues(type);

			for (int i = 0; i < enumNames.Length; i++)
			{
				if (string.Equals(enumNames[i], input, StringComparison.OrdinalIgnoreCase))
				{
					return (Enum)enumValues.GetValue(i);
				}
			}

			throw new InvalidCastException($"Cannot cast '{input}' to enum '{type.Name}'.");
		}

		/// <summary>
		/// Parses a string into a Vector3. Supports format 'x,y,z' or '(x,y,z)' or 'x y z'.
		/// </summary>
		public static Vector3 ToVector3(string input)
		{
			input = input.Trim('(', ')', ' ');
			string[] parts = input.Contains(',') ? input.Split(',') : input.Split(' ');
			if (parts.Length >= 3)
			{
				return new Vector3(ToFloat(parts[0].Trim()), ToFloat(parts[1].Trim()), ToFloat(parts[2].Trim()));
			}

			throw new InvalidCastException($"Cannot cast '{input}' to Vector3. Format: 'x,y,z'");
		}

		/// <summary>
		/// Parses a string into a Vector2. Supports format 'x,y' or '(x,y)' or 'x y'.
		/// </summary>
		public static Vector2 ToVector2(string input)
		{
			input = input.Trim('(', ')', ' ');
			string[] parts = input.Contains(',') ? input.Split(',') : input.Split(' ');
			if (parts.Length >= 2)
			{
				return new Vector2(ToFloat(parts[0].Trim()), ToFloat(parts[1].Trim()));
			}

			throw new InvalidCastException($"Cannot cast '{input}' to Vector2. Format: 'x,y'");
		}

		/// <summary>
		/// Parses a string into a Color. Supports hex code ('#FF0000', 'FF0000') or named colors ('red', 'blue', etc.).
		/// </summary>
		public static Color ToColor(string input)
		{
			input = input.Trim();
			if (ColorUtility.TryParseHtmlString(input.StartsWith("#") ? input : "#" + input, out Color color))
			{
				return color;
			}

			return input.ToLowerInvariant() switch
			{
				"red" => Color.red,
				"green" => Color.green,
				"blue" => Color.blue,
				"white" => Color.white,
				"black" => Color.black,
				"yellow" => Color.yellow,
				"cyan" => Color.cyan,
				"magenta" => Color.magenta,
				"gray" or "grey" => Color.gray,
				_ => throw new InvalidCastException($"Cannot cast '{input}' to Color.")
			};
		}

		/// <summary>
		/// Replaces the word at a specified index in a string with a new word.
		/// </summary>
		public static string ReplaceWordAtIndex(string input, int wordIndex, string newWord)
		{
			string[] words = input.Split(' ');

			if (wordIndex < 0 || wordIndex >= words.Length)
			{
				throw new ArgumentOutOfRangeException(nameof(wordIndex), "The word index must be within the range of the input string.");
			}

			words[wordIndex] = newWord;

			return string.Join(" ", words);
		}
	}
}
