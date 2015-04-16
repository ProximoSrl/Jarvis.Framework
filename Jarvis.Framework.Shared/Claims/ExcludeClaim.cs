using System;

namespace Jarvis.Framework.Shared.Claims
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class ExcludeClaimAttribute : ClaimAttribute
    {
        readonly IClaimsMatcher _matcher;

        public ExcludeClaimAttribute(string claim, string value)
        {
            _matcher = ClaimsMatcher.Not(claim, value);
        }

        public override IClaimsMatcher Build()
        {
            return _matcher;
        }
    }
}