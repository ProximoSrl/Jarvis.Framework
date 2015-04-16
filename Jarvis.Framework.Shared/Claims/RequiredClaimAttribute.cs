using System;

namespace Jarvis.Framework.Shared.Claims
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class RequiredClaimAttribute : ClaimAttribute
    {
        readonly IClaimsMatcher _matcher;

        public RequiredClaimAttribute(string claim, string value)
        {
            if (string.IsNullOrWhiteSpace(claim)) throw new ArgumentNullException("claim");
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentNullException("value");
            
            _matcher = ClaimsMatcher.Require(claim, value);
        }

        public RequiredClaimAttribute(string claim, bool value = true) : this(claim, value.ToString())
        {
        }
        
        public override IClaimsMatcher Build()
        {
            return _matcher;
        }
    }
}