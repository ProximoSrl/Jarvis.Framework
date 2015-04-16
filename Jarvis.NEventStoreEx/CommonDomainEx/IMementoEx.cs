namespace Jarvis.NEventStoreEx.CommonDomainEx
{
    public interface IMementoEx
    {
        IIdentity Id { get; set; }
        int Version { get; set; }
    }
}