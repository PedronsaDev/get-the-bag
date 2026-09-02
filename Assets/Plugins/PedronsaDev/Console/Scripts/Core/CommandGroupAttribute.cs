using System;

namespace PedronsaDev.Console
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public class CommandGroupAttribute : Attribute
	{
		public string Name;

		public CommandGroupAttribute() : this("")
		{
		}

		public CommandGroupAttribute(string name)
		{
			Name = name;
		}

		public bool HasName() => !string.IsNullOrEmpty(Name);
	}
}