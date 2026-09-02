
namespace PedronsaDev.Console
{
	internal class SmartParameterInvalidCastException : SmartException
	{
		internal SmartParameterInvalidCastException(string inputParameter, Command command, string parameterTypeName)
		: base($"Parameter '{inputParameter}' of command '{command.Name}' could not be cast to type '{parameterTypeName}'.")
		{
		}
	}
}
