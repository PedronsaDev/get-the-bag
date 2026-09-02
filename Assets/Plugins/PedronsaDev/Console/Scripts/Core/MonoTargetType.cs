namespace PedronsaDev.Console
{
	public enum MonoTargetType
	{
		/// <summary>
		/// Targets the first active instance found of the MonoBehaviour.
		/// </summary>
		Single,
		/// <summary>
		/// Targets all active instances found of the MonoBehaviour.
		/// </summary>
		Active,
		/// <summary>
		/// Targets both active and inactive instances found of the MonoBehaviour.
		/// </summary>
		All
	}
}
