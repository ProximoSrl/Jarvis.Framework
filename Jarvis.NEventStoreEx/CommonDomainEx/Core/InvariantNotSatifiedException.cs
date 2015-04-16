namespace Jarvis.NEventStoreEx.CommonDomainEx.Core
{
	/// <summary>
	/// Thrown where an invariant is not satified
	/// </summary>
	public class InvariantNotSatifiedException : DomainException
	{
		public InvariantNotSatifiedException(IIdentity id, string message) : base(id, message)
		{
		}
	}
}