
namespace PedronsaDev.Console
{
	internal class SmartParameterNullException : SmartException
	{
		internal SmartParameterNullException(string parameterName, Command command)
		: base($"Parameter '{parameterName}' of command '{command.Name}' cannot be null.")
		{
		}
	}
}
