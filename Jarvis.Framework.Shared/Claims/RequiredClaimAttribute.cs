using System;

namespace Jarvis.Framework.Shared.Claims
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
	public class RequiredClaimAttribute : ClaimAttribute
	{
		private readonly IClaimsMatcher _matcher;
        private readonly string _claim;
        private readonly string _value;

        public RequiredClaimAttribute(string claim, string value)
		{
			if (string.IsNullOrWhiteSpace(claim)) throw new ArgumentNullException(nameof(claim));
			if (string.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(value));

			_matcher = ClaimsMatcher.Require(claim, value);
            _claim = claim;
            _value = value;
        }

        public RequiredClaimAttribute(string claim, bool value) : this(claim, value.ToString())
        {
        }

        public RequiredClaimAttribute(string claim) : this(claim, "true")
		{
		}

		public override IClaimsMatcher Build()
		{
			return _matcher;
		}

        public override string Describe()
        {
            return $"Claim {_claim} should be present with value {_value}";
        }
    }
}