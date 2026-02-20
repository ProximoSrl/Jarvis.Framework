using System;
using System.Threading;
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
        TIdentity New<TIdentity>() where TIdentity : IIdentity;

        /// <summary>
        /// Generate the next identity for a given type.
        /// </summary>
        /// <typeparam name="TIdentity">Type of the identity to generate.</typeparam>
        /// <returns>The newly generated identity</returns>
        Task<TIdentity> NewAsync<TIdentity>(CancellationToken cancellation = default) where TIdentity : IIdentity;

        /// <summary>
        /// Generate the next identity for a given type. This is the original non-async version,
        /// please prefer using the async version because this method usually does a roundtrip to the
        /// database.
        /// </summary>
        /// <param name="identityType">Type of the identity to generate.</param>
        /// <returns>The newly generated identity</returns>
        IIdentity New(Type identityType);

        /// <summary>
        /// Generate the next identity for a given type.
        /// </summary>
        /// <param name="identityType">Type of the identity to generate.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The newly generated identity</returns>
        Task<IIdentity> NewAsync(Type identityType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate a batch of new identities for a given type.
        /// </summary>
        /// <typeparam name="TIdentity">Type of the identity to generate.</typeparam>
        /// <param name="count">Number of identities to generate.</param>
        /// <param name="cancellation"></param>
        /// <returns>The newly generated identities</returns>
        Task<TIdentity[]> NewManyAsync<TIdentity>(int count, CancellationToken cancellation = default) where TIdentity : IIdentity;
    }
}