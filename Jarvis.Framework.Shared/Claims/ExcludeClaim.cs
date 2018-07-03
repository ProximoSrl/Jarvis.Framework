using System;

namespace Jarvis.Framework.Shared.Claims
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class ExcludeClaimAttribute : ClaimAttribute
    {
        private readonly IClaimsMatcher _matcher;
        private readonly string _claim;
        private readonly string _value;

        public ExcludeClaimAttribute(string claim, string value)
        {
            _matcher = ClaimsMatcher.Not(claim, value);
            _claim = claim;
            _value = value;
        }

        public override IClaimsMatcher Build()
        {
            return _matcher;
        }

        public override string Describe()
        {
            return $"Claim {_claim} should not be present with value {_value}";
        }
    }
}