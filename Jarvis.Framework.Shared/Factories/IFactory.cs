namespace Jarvis.Framework.Shared.Factories
{
    public interface IFactory<T>
    {
        T Create();
        void Release(T service);
    }
}
