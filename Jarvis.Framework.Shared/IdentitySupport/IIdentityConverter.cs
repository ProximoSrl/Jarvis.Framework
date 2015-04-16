using Jarvis.NEventStoreEx.CommonDomainEx;

namespace Jarvis.Framework.Shared.IdentitySupport
{
    public interface IIdentityGenerator
    {
        TIdentity New<TIdentity>();
    }

    public interface IIdentityManager : IIdentityConverter, IIdentityGenerator
    {
    }
}