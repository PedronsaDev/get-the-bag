using System;

namespace PedronsaDev.Console
{
	internal class SmartException : Exception
	{
		internal SmartException(string message)
		: base($"Smart Error: {message}")
		{
		}
	}
}
