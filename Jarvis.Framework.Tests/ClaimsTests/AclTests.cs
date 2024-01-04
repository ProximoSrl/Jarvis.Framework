using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Shared.Claims;
using NUnit.Framework;

namespace Jarvis.Framework.Tests.ClaimsTests
{
    [TestFixture]
    public class AclTests
    {
        readonly Claim[] _claims = new[]
            {
                Claim.For("user", "User_1"), 
                Claim.For("group", "Group_1")
            };

        [Test]
        public void missing_is_inherited()
        {
            var acl = new AccessControlList(new[] { "group", "user" });
            NUnit.Framework.Legacy.ClassicAssert.AreEqual(GrantType.Inherited, acl.GetGrantAccess("not_found", _claims));
        }

        [Test]
        public void allow_read()
        {
            var acl = new AccessControlList(new[] { "group", "user" });
            acl.Set("read", Claim.For("group", "Group_1"), GrantType.Allowed);
            var grant = acl.GetGrantAccess("read", _claims);
            NUnit.Framework.Legacy.ClassicAssert.AreEqual(GrantType.Allowed, grant);
        }

        [Test]
        public void allow_to_star()
        {
            var acl = new AccessControlList(new[] { "group", "user" });
            acl.Set("read", Claim.For("group", "*"), GrantType.Allowed);
            var grant = acl.GetGrantAccess("read", _claims);
            NUnit.Framework.Legacy.ClassicAssert.AreEqual(GrantType.Allowed, grant);
        }

        [Test]
        public void inherited_to_group_2()
        {
            var acl = new AccessControlList(new[] { "group", "user" });
            acl.Set("read", Claim.For("group", "Group_1"), GrantType.Allowed);
            var grant = acl.GetGrantAccess("read", new []{Claim.For("group", "Group_2")});
            NUnit.Framework.Legacy.ClassicAssert.AreEqual(GrantType.Inherited, grant);
        }

        [Test]
        public void denied_to_group_2()
        {
            var acl = new AccessControlList(new[] { "group", "user" });
            acl.Set("read", Claim.For("group", "Group_1"), GrantType.Allowed);
            acl.Set("read", Claim.For("group", "Group_2"), GrantType.Denied);
            
            var grant = acl.GetGrantAccess("read", new[] { Claim.For("group", "Group_2") });
            NUnit.Framework.Legacy.ClassicAssert.AreEqual(GrantType.Denied, grant);
        }

        [Test]
        public void denied_on_conflicting_grants()
        {
            var acl = new AccessControlList(new[] { "group", "user" });
            acl.Set("read", Claim.For("group", "Group_1"), GrantType.Allowed);
            acl.Set("read", Claim.For("group", "Group_2"), GrantType.Denied);

            var grant = acl.GetGrantAccess("read", new[]{
                    Claim.For("group", "Group_2"),
                    Claim.For("group", "Group_1")
            });
            
            NUnit.Framework.Legacy.ClassicAssert.AreEqual(GrantType.Denied, grant);
        }

        [Test]
        public void denied_on_conflicting_grants_inverted_order()
        {
            var acl = new AccessControlList(new []{"group", "user"});
            acl.Set("read", Claim.For("group", "Group_1"), GrantType.Denied);
            acl.Set("read", Claim.For("group", "Group_2"), GrantType.Allowed);

            var grant = acl.GetGrantAccess("read", new[]{
                    Claim.For("group", "Group_2"),
                    Claim.For("group", "Group_1")
            });

            NUnit.Framework.Legacy.ClassicAssert.AreEqual(GrantType.Denied, grant);
        }

        [Test]
        public void allowed_to_user_1()
        {
            var acl = new AccessControlList(new[] { "group", "user" });
            acl.Set("read", Claim.For("group", "Group_1"), GrantType.Denied);
            acl.Set("read", Claim.For("user", "User_1"), GrantType.Allowed);

            var grant = acl.GetGrantAccess("read", _claims);
            NUnit.Framework.Legacy.ClassicAssert.AreEqual(GrantType.Allowed, grant);
        }

        [Test]
        public void denied_to_all_user_but_allowed_for_user1()
        {
            var acl = new AccessControlList(new[] { "group", "user" });
            acl.Set("read", Claim.For("user", "*"), GrantType.Denied);
            acl.Set("read", Claim.For("user", "User_1"), GrantType.Allowed);

            var grant = acl.GetGrantAccess("read", _claims);
            NUnit.Framework.Legacy.ClassicAssert.AreEqual(GrantType.Allowed, grant);
        }

        [Test]
        public void denied_to_all_groups_but_allowed_for_user1()
        {
            var acl = new AccessControlList(new[] { "group", "user" });
            acl.Set("read", Claim.For("group", "*"), GrantType.Denied);
            acl.Set("read", Claim.For("user", "User_1"), GrantType.Allowed);

            var grant = acl.GetGrantAccess("read", _claims);
            NUnit.Framework.Legacy.ClassicAssert.AreEqual(GrantType.Allowed, grant);
        }

        [Test]
        public void denied_to_all_groups_allowed_to_group1_denied_to_user1()
        {
            var acl = new AccessControlList(new[] { "group", "user" });
            acl.Set("read", Claim.For("group", "*"), GrantType.Denied);
            acl.Set("read", Claim.For("group", "Group_1"), GrantType.Allowed);
            acl.Set("read", Claim.For("user", "User_1"), GrantType.Denied);

            var grant = acl.GetGrantAccess("read", _claims);
            NUnit.Framework.Legacy.ClassicAssert.AreEqual(GrantType.Denied, grant);
        }

        [Test]
        public void acl_update()
        {
            var acl = new AccessControlList(new[] { "group", "user" });
            acl.Set("read", Claim.For("group", "*"), GrantType.Denied);
            acl.Set("read", Claim.For("group", "*"), GrantType.Allowed);
            var grant = acl.GetGrantAccess("read", _claims);
            NUnit.Framework.Legacy.ClassicAssert.AreEqual(GrantType.Allowed, grant);
            NUnit.Framework.Legacy.ClassicAssert.AreEqual(1, acl.Count());
        }

        [Test]
        public void acl_unset()
        {
            var acl = new AccessControlList(new[] { "group", "user" });
            acl.Set("read", Claim.For("group", "*"), GrantType.Allowed);
            acl.Set("read", Claim.For("group", "Group_1"), GrantType.Denied);

            acl.UnSet("read", Claim.For("group", "Group_1"));

            var grant = acl.GetGrantAccess("read", _claims);
            NUnit.Framework.Legacy.ClassicAssert.AreEqual(GrantType.Allowed, grant);
            NUnit.Framework.Legacy.ClassicAssert.AreEqual(1, acl.Count());
        }

        [Test]
        public void acl_unset_all_claims()
        {
            var acl = new AccessControlList(new[] { "group", "user" });
            acl.Set("read", Claim.For("group", "*"), GrantType.Allowed);
            acl.UnSet("read", Claim.For("group", "*"));

            var grant = acl.GetGrantAccess("read", _claims);

            NUnit.Framework.Legacy.ClassicAssert.AreEqual(GrantType.Inherited, grant);
            NUnit.Framework.Legacy.ClassicAssert.AreEqual(0, acl.Count());
        }
    }
}
