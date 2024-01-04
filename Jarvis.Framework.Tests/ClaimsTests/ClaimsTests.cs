using Jarvis.Framework.Shared.Claims;
using NUnit.Framework;

namespace Jarvis.Framework.Tests.ClaimsTests
{
    [RequiredClaim("role", "admin")]
    public class RequireAdminRoleCommand
    {
    }

    [ExcludeClaim("role", "user")]
    public class ForbiddenToUsers
    {
    }


    [TestFixture]
    public class ClaimsTests
    {
        [Test]
        public void simple_match_true()
        {
            var matcher = ClaimsMatcher.Require("admin", "True");
            NUnit.Framework.Legacy.ClassicAssert.IsTrue(matcher.Matches(Claim.For("admin", "True")));
        }

        [Test]
        public void simple_match_false()
        {
            var matcher = ClaimsMatcher.Require("admin", "True");
            NUnit.Framework.Legacy.ClassicAssert.IsFalse(matcher.Matches(Claim.For("admin", "False")));
        }

        [Test]
        public void should_match_claim_negation()
        {
            var matcher = ClaimsMatcher.Not("disabled", "True");
            NUnit.Framework.Legacy.ClassicAssert.IsFalse(matcher.Matches(Claim.For("disabled", "True")));
        }

        [Test]
        public void shold_match_or_operator()
        {
            var matcher = ClaimsMatcher.Or(
                ClaimsMatcher.Require("role", "user"),
                ClaimsMatcher.Require("role", "admin")
            );

            NUnit.Framework.Legacy.ClassicAssert.IsFalse(matcher.Matches(Claim.For("role", "guest")));
            NUnit.Framework.Legacy.ClassicAssert.IsTrue(matcher.Matches(Claim.For("role", "user")));
            NUnit.Framework.Legacy.ClassicAssert.IsTrue(matcher.Matches(Claim.For("role", "admin")));
        }

        [Test]
        public void should_match_and_operator()
        {
            var matcher = ClaimsMatcher.And(
                ClaimsMatcher.Require("role", "user"),
                ClaimsMatcher.Require("role", "admin")
            );

            NUnit.Framework.Legacy.ClassicAssert.IsFalse(matcher.Matches(Claim.For("role", "user")));
            NUnit.Framework.Legacy.ClassicAssert.IsFalse(matcher.Matches(Claim.For("role", "admin")));
            NUnit.Framework.Legacy.ClassicAssert.IsTrue(matcher.Matches(
                Claim.For("role", "admin"),
                Claim.For("role", "user")
            ));
            NUnit.Framework.Legacy.ClassicAssert.IsTrue(matcher.Matches(
                Claim.For("role", "user"),
                Claim.For("role", "admin")
            ));
        }

        [Test]
        public void should_match_by_attributes()
        {
            var adminRole = Claim.For("role", "admin");
            var matched = ClaimsMatcher.Matches<RequireAdminRoleCommand>(adminRole);
            NUnit.Framework.Legacy.ClassicAssert.IsTrue(matched);
        }

        [Test]
        public void should_not_match_by_attributes()
        {
            var userRole = Claim.For("role", "user");
            var matched = ClaimsMatcher.Matches<RequireAdminRoleCommand>(userRole);
            NUnit.Framework.Legacy.ClassicAssert.IsFalse(matched);
        }

        [Test]
        public void should_be_forbidded_to_users()
        {
            var userRole = Claim.For("role", "user");
            var matched = ClaimsMatcher.Matches<ForbiddenToUsers>(userRole);
            NUnit.Framework.Legacy.ClassicAssert.IsFalse(matched);
        }

        [Test]
        public void should_be_allowd_to_non_users()
        {
            var guestRole = Claim.For("role", "guest");
            var matched = ClaimsMatcher.Matches<ForbiddenToUsers>(guestRole);
            NUnit.Framework.Legacy.ClassicAssert.IsTrue(matched);
        }
    }
}
