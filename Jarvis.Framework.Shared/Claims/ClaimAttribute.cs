using System;

namespace Jarvis.Framework.Shared.Claims
{
    public abstract class ClaimAttribute : Attribute
    {
        public abstract IClaimsMatcher Build();
    }
}