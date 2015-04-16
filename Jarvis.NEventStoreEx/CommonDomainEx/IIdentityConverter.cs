namespace Jarvis.NEventStoreEx.CommonDomainEx
{
    public interface IIdentityConverter
    {
        string ToString(IIdentity identity);
        IIdentity ToIdentity(string identityAsString);
        TIdentity ToIdentity<TIdentity>(string identityAsString);
        bool TryParse<T>(string id, out T typedIdentity) where T : class, IIdentity;
    }
}