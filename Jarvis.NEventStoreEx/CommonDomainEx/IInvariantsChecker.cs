
namespace Jarvis.NEventStoreEx.CommonDomainEx
{
    /// <summary>
    /// Throw DomainException if the invariants are not satisfied
    /// </summary>
    public interface IInvariantsChecker
    {
        /// <summary>
        /// Check if the invariants are satisfied
        /// <exception cref="Jarvis.NEventStoreEx.CommonDomainEx.Core.InvariantNotSatifiedException">when invariants are not satisfied</exception>
        /// </summary>
        InvariantCheckResult CheckInvariants();
    }
}