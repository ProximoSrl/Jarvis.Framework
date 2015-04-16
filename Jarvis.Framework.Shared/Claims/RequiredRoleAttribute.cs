using System;

namespace Jarvis.Framework.Shared.Claims
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class RequiredRoleAttribute : RequiredClaimAttribute
    {
        public RequiredRoleAttribute(string role)
            : base("role", role)
        {
        }
    }
}