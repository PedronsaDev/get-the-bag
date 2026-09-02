namespace PedronsaDev.Console
{
	internal class SmartParameterTypeNotSupportedException : SmartException
	{
		internal SmartParameterTypeNotSupportedException(string parameterTypeName)
		: base($"Parameter type '{parameterTypeName}' is not supported.")
		{
		}
	}
}
