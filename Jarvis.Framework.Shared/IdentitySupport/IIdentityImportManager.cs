using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.IdentitySupport
{
    /// <summary>
    /// Interface needed by importing function, this will be used to check actual
    /// status of the identity, but also to allow forcing to set next identity to
    /// avoid mappers to generate rubbish.
    /// </summary>
    public interface IIdentityImportManager
    {
        /// <summary>
        /// Force next id to be the one specified, will throw if the id is already used.
        /// </summary>
        /// <returns></returns>
        Task ForceNextIdAsync<TIdentity>(long nextIdToReturn);

        /// <summary>
        /// Return the next id that will be generated for the specified identity.
        /// </summary>
        /// <returns></returns>
        Task<long> PeekNextIdAsync<TIdentity>();
    }
}
