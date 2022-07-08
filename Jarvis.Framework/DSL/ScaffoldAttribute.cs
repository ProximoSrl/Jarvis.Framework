using System;

namespace Jarvis.Framework.Kernel.DSL
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class ScaffoldAttribute : Attribute
    {
    }
}
