using System.Collections.Generic;

namespace Jarvis.Framework.Shared.Claims
{
    public interface IClaimsMatcher
    {
        bool Matches(params Claim[] claims);

        bool Matches(IEnumerable<Claim> claims);
    }
}