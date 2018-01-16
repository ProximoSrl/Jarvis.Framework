using System;

namespace Jarvis.Framework.Shared.Claims
{
	[AttributeUsage(AttributeTargets.All, AllowMultiple = false)]
	public abstract class ClaimAttribute : Attribute
    {
        public abstract IClaimsMatcher Build();
    }
}