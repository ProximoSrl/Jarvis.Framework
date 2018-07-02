using System.Collections.Generic;
using System.Linq;

namespace Jarvis.Framework.Shared.Claims
{
#pragma warning disable S3218 // Inner class members should not shadow outer class "static" or type members
	public static class ClaimsMatcher
    {
        public class AlwaysMatcher : IClaimsMatcher
        {
            public bool Matches(params Claim[] claims)
            {
                return Matches((IEnumerable<Claim>)claims);
            }

            public bool Matches(IEnumerable<Claim> claims)
			{
                return true;
            }
        }

        public class RequireMatcher : IClaimsMatcher
        {
            private readonly Claim _claim;

            public RequireMatcher(Claim claim)
            {
                _claim = claim;
            }

            public bool Matches(Claim[] claims)
            {
                return Matches((IEnumerable<Claim>)claims);
            }

            public bool Matches(IEnumerable<Claim> claims)
            {
                return claims.Any(x => x == _claim);
            }
        }

        public class NotMatcher : IClaimsMatcher
        {
            private readonly IClaimsMatcher _matcher;

            public NotMatcher(IClaimsMatcher matcher)
            {
                _matcher = matcher;
            }

            public bool Matches(params Claim[] claims)
            {
                return Matches((IEnumerable<Claim>)claims);
            }


            public bool Matches(IEnumerable<Claim> claims)
            {
                return !_matcher.Matches(claims);
            }
        }

        public class AndMatcher : IClaimsMatcher
        {
            private readonly IClaimsMatcher[] _matchers;

            public AndMatcher(IClaimsMatcher[] matchers)
            {
                _matchers = matchers;
            }

            public bool Matches(params Claim[] claims)
            {
                return Matches((IEnumerable<Claim>)claims);
            }


            public bool Matches(IEnumerable<Claim> claims)
            {
                return _matchers.All(x => x.Matches(claims));
            }
        }

        public class OrMatcher : IClaimsMatcher
        {
            readonly IClaimsMatcher[] _matchers;

            public OrMatcher(IClaimsMatcher[] matchers)
            {
                _matchers = matchers;
            }

            public bool Matches(params Claim[] claims)
            {
                return Matches((IEnumerable<Claim>)claims);
            }

            public bool Matches(IEnumerable<Claim> claims)
            {
                return _matchers.Any(x => x.Matches(claims));
            }
        }

        public static IClaimsMatcher Require(string claim, string value)
        {
            return Require(Claim.For(claim, value));
        }

        public static IClaimsMatcher Require(Claim claim)
        {
            return new RequireMatcher(claim);
        }

        public static IClaimsMatcher Not(string claim, string value)
        {
            return Not(Claim.For(claim, value));
        }

        public static IClaimsMatcher Not(Claim claim)
        {
            return new NotMatcher(Require(claim));
        }

        public static IClaimsMatcher Or(params IClaimsMatcher[] matchers)
        {
            return new OrMatcher(matchers);
        }

        public static IClaimsMatcher And(params IClaimsMatcher[] matchers)
        {
            return new AndMatcher(matchers);
        }

        public static bool Matches<T>(params Claim[] claims)
        {
            var attributes = (ClaimAttribute[])(typeof(T).GetCustomAttributes(typeof(ClaimAttribute), true));
            return attributes.All(x => x.Build().Matches(claims));
        }

        public static IClaimsMatcher GetClaims(object o)
        {
            var attributes = (ClaimAttribute[])(o.GetType().GetCustomAttributes(typeof(ClaimAttribute), true));
            if (attributes.Any())
            {
                return new AndMatcher(attributes.Select(x=>x.Build()).ToArray());
            }

            return new AlwaysMatcher();
        }
    }
#pragma warning restore S3218 // Inner class members should not shadow outer class "static" or type members
}
