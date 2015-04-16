using System.Reflection;
using Jarvis.Framework.Shared.Storage;

namespace Jarvis.Framework.Shared.IdentitySupport
{
    public static class IdentitiesRegistration
    {
        public static void RegisterFromAssembly(Assembly assembly)
        {
            MessagesRegistration.RegisterIdentities(assembly);
        }
    }
}
