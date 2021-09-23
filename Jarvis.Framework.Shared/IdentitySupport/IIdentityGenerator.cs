namespace Jarvis.Framework.Shared.IdentitySupport
{
    public interface IIdentityGenerator
    {
        TIdentity New<TIdentity>();
    }
}