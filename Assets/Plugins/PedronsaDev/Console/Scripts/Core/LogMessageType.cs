namespace PedronsaDev.Console
{
	public enum LogMessageType
	{
		/// <summary>
		/// LogMessageType used for regular log messages.
		/// </summary>
		Log,
		/// <summary>
		/// LogMessageType used for Commands.
		/// </summary>
		Command,
		/// <summary>
		/// LogMessageType used to Autocomplete commands.
		/// </summary>
		Autocompletion,
		/// <summary>
		/// LogMessageType used for Warnings.
		/// </summary>
		Warning,
		/// <summary>
		/// LogMessageType used for Errors.
		/// </summary>
		Error,
		Custom
	}
}
