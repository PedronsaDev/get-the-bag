using System;

namespace PedronsaDev.Console
{
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
	public class CommandAttribute : Attribute
	{
		public string Name;
		public string Description;
		public MonoTargetType MonoTargetType;

		public CommandAttribute() : this("", "", MonoTargetType.Single)
		{
		}

		public CommandAttribute(string description) : this("", description, MonoTargetType.Single)
		{
		}

		public CommandAttribute(string name, string description) : this(name, description, MonoTargetType.Single)
		{
		}

		public CommandAttribute(string description, MonoTargetType monoTargetType) : this("", description, monoTargetType)
		{
		}

		public CommandAttribute(MonoTargetType monoTargetType) : this("", "", monoTargetType)
		{
		}

		public CommandAttribute(string name, string description, MonoTargetType monoTargetType)
		{
			Name = name;
			Description = description;
			MonoTargetType = monoTargetType;
		}

		public bool HasName() => !string.IsNullOrEmpty(Name);
	}
}