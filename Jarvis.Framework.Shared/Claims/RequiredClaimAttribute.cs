using System;

namespace Jarvis.Framework.Shared.Claims
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
	public class RequiredClaimAttribute : ClaimAttribute
	{
		private readonly IClaimsMatcher _matcher;

		public RequiredClaimAttribute(string claim, string value)
		{
			if (string.IsNullOrWhiteSpace(claim)) throw new ArgumentNullException(nameof(claim));
			if (string.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(value));

			_matcher = ClaimsMatcher.Require(claim, value);
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
	}
}