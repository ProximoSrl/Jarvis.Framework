using System;
using Castle.Windsor;
using Jarvis.Framework.Shared.IdentitySupport;

namespace Jarvis.Framework.Shared.MultitenantSupport
{
    public interface ITenant : IDisposable
    {
        ICounterService CounterService { get; }
        T Get<T>(string key);
        bool HasBeedDisposed { get; }
        TenantId Id { get; }
        IWindsorContainer Container { get; }
        string GetConnectionString(string name);
    }
}