using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.IdentitySupport
{
    /// <summary>
    /// Interface used to generate new identity.
    /// </summary>
    public interface IIdentityGenerator
    {
        /// <summary>
        /// Generate the next identity for a given type. This is the original non-async version,
        /// please prefer using the async version because this method usually does a roundtrip to the
        /// database.
        /// </summary>
        /// <typeparam name="TIdentity">Type of the identity to generate.</typeparam>
        /// <returns>The newly generated identity</returns>
        TIdentity New<TIdentity>();

        /// <summary>
        /// Generate the next identity for a given type.
        /// </summary>
        /// <typeparam name="TIdentity">Type of the identity to generate.</typeparam>
        /// <returns>The newly generated identity</returns>
        Task<TIdentity> NewAsync<TIdentity>();
    }
}