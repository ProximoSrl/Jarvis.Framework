namespace Jarvis.Framework.Shared.Claims
{
    public interface IClaimsMatcher
    {
        bool Matches(params Claim[] claims);
    }
}