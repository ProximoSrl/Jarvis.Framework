namespace Jarvis.Framework.Shared.IdentitySupport
{
    public interface ICounterService
    {
        long GetNext(string serie);
    }
}