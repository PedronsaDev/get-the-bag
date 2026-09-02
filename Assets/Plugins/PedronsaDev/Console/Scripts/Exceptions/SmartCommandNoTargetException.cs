
namespace PedronsaDev.Console
{
	internal class SmartCommandNoTargetException : SmartException
	{
		internal SmartCommandNoTargetException(Command command)
		: base($"Command '{command.Name}' could not be executed because it has no target available.")
		{
		}
	}
}
