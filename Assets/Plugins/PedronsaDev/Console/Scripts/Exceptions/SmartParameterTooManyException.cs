namespace PedronsaDev.Console
{
	internal class SmartParameterTooManyException : SmartException
	{
		internal SmartParameterTooManyException(Command command, int inputParametersLength)
		: base($"Command '{command.Name}' takes {command.Method.GetParameters().Length} argument(s) but you are trying to execute it with {inputParametersLength} argument(s).")
		{
		}
	}
}
