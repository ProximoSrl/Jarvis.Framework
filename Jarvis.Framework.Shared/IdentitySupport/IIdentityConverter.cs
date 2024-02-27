namespace Jarvis.Framework.Shared.IdentitySupport
{
    /// <summary>
    /// This interface is used to convert identity to string and viceversa.
    /// </summary>
    public interface IIdentityConverter
    {
        string ToString(IIdentity identity);
        IIdentity ToIdentity(string identityAsString);
        TIdentity ToIdentity<TIdentity>(string identityAsString) where TIdentity : IIdentity;

        /// <summary>
        /// Try parsing a string to a specific identity, this method is used to avoid
        /// throwing exception when parsing identity.
        /// </summary>
        /// <typeparam name="T">Type you want to check, it can be the <see cref="IIdentity"/> base interface
        /// if you want to parse in any of known id, or you can pass concrete class to verify that the id
        /// is indeed of the current type and parse at the same time.</typeparam>
        /// <param name="id"></param>
        /// <param name="typedIdentity"></param>
        /// <returns></returns>
        bool TryParse<T>(string id, out T typedIdentity) where T : IIdentity;

        /// <summary>
        /// <para>
        /// Try to get the tag for a specific identity, this method is created to do a
        /// fast check if <paramref name="id"/> is a valid identity registerd in
        /// <see cref="IdentityManager"/>.
        /// </para>
        /// <para>
        /// Needed to quick understand if a string is a valid identity without creating
        /// a real identity and without using try/catch slow logic
        /// </para>
        /// </summary>
        /// <param name="id"></param>
        /// <param name="tag"></param>
        /// <returns></returns>
        bool TryGetTag(string id, out string tag);
    }
}