namespace Jarvis.Framework.Kernel.Events
{
    public interface IEventHandler
    {
    }

    public interface IEventHandler<T> : IEventHandler
    {
        void On(T e);
    }
/*
    public interface IEventToCommandHandler
    {
        string GetSlotName();
    }
 */ 
}
