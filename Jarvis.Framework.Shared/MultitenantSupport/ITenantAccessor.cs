namespace Jarvis.Framework.Shared.MultitenantSupport
{
    public interface ITenantAccessor
    {
        ITenant Current { get; }
        ITenant GetTenant(TenantId id);
        ITenant[] Tenants { get; }
    }
}