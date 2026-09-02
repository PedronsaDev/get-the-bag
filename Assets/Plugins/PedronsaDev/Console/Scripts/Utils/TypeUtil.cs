using System;
using System.Linq;
using UnityEngine;

namespace PedronsaDev.Console.Utils
{
	public static class TypeUtil
	{
		private static int m_IntModifier = 1;
		private static float m_FloatModifier = 0.1f;

		public static string GetDefaultStringValue(Type type)
		{
			if (type == typeof(string))
			{
				return "message";
			}
			if (type == typeof(int))
			{
				return default(int).ToString();
			}
			if (type == typeof(bool))
			{
				return default(bool).ToString().ToLower();
			}
			if (type == typeof(float))
			{
				return default(float).ToString();
			}
			if (type == typeof(Vector3))
			{
				return "0,0,0";
			}
			if (type == typeof(Vector2))
			{
				return "0,0";
			}
			if (type == typeof(Color))
			{
				return "#FFFFFF";
			}
			if (type == typeof(string[]))
			{
				return "item1,item2";
			}
			if (type == typeof(int[]))
			{
				return "0,1,2";
			}
			if (type.IsEnum)
			{
				return EnumUtils.GetFullValueName((Enum)Enum.GetValues(type).GetValue(0));
			}

			throw new SmartParameterTypeNotSupportedException(type.Name);
		}

		public static string GetStringValue(Type type, object value)
		{
			if (type == typeof(string))
			{
				return (string)value;
			}
			if (type == typeof(int))
			{
				return value.ToString();
			}
			if (type == typeof(bool))
			{
				return value.ToString().ToLower();
			}
			if (type == typeof(float))
			{
				return value.ToString();
			}
			if (type == typeof(Vector3))
			{
				Vector3 v = (Vector3)value;
				return $"{v.x},{v.y},{v.z}";
			}
			if (type == typeof(Vector2))
			{
				Vector2 v = (Vector2)value;
				return $"{v.x},{v.y}";
			}
			if (type == typeof(Color))
			{
				return $"#{ColorUtility.ToHtmlStringRGBA((Color)value)}";
			}
			if (type == typeof(string[]) && value is string[] arr)
			{
				return string.Join(",", arr);
			}
			if (type == typeof(int[]) && value is int[] intArr)
			{
				return string.Join(",", intArr);
			}
			if (type.IsEnum)
			{
				return EnumUtils.GetFullValueName((Enum)value);
			}

			throw new SmartParameterTypeNotSupportedException(type.Name);
		}

		public static object GetObjectValue(Type type, string value)
		{
			if (type == typeof(string))
			{
				return value;
			}
			if (type == typeof(int))
			{
				return StringUtil.ToInt(value);
			}
			if (type == typeof(bool))
			{
				return StringUtil.ToBool(value);
			}
			if (type == typeof(float))
			{
				return StringUtil.ToFloat(value);
			}
			if (type == typeof(Vector3))
			{
				return StringUtil.ToVector3(value);
			}
			if (type == typeof(Vector2))
			{
				return StringUtil.ToVector2(value);
			}
			if (type == typeof(Color))
			{
				return StringUtil.ToColor(value);
			}
			if (type == typeof(string[]))
			{
				return value.Split(',').Select(s => s.Trim()).ToArray();
			}
			if (type == typeof(int[]))
			{
				return value.Split(',').Select(s => StringUtil.ToInt(s.Trim())).ToArray();
			}
			if (type.IsEnum)
			{
				return StringUtil.ToEnum(value, type);
			}

			throw new SmartParameterTypeNotSupportedException(type.Name);
		}

		public static object GetNextObjectValue(Type type, object value)
		{
			if (type == typeof(string))
			{
				return (string)value;
			}
			if (type == typeof(int))
			{
				return (int)value + m_IntModifier;
			}
			if (type == typeof(bool))
			{
				return !(bool)value;
			}
			if (type == typeof(float))
			{
				return (float)value + m_FloatModifier;
			}
			if (type.IsEnum)
			{
				return EnumUtils.GetNextValue((Enum)value);
			}

			return value;
		}
	}
}
