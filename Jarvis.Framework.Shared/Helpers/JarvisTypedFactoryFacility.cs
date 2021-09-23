using Castle.Facilities.TypedFactory;
using Castle.Facilities.TypedFactory.Internal;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.SubSystems.Conversion;

namespace Jarvis.Framework.Shared.Helpers
{
    public class JarvisTypedFactoryFacility : TypedFactoryFacility
    {
        internal static readonly string DefaultDelegateSelectorKey =
               "Castle.TypedFactory.DefaultDelegateFactoryComponentSelector";

        internal static readonly string DefaultInterfaceSelectorKey =
            "Castle.TypedFactory.DefaultInterfaceFactoryComponentSelector";

        protected override void Init()
        {
            Kernel.Register(Component.For<TypedFactoryInterceptor>()
                                .NamedAutomatically(InterceptorKey),
                            Component.For<ITypedFactoryComponentSelector>()
                                .ImplementedBy<DefaultTypedFactoryComponentSelector>()
                                .NamedAutomatically(DefaultInterfaceSelectorKey),
                            Component.For<ITypedFactoryComponentSelector>()
                                .ImplementedBy<DefaultDelegateComponentSelector>()
                                .NamedAutomatically(DefaultDelegateSelectorKey));

            Kernel.ComponentModelBuilder.AddContributor(new TypedFactoryCachingInspector());

            //this is the legacyInit
            Kernel.Register(Component.For<FactoryInterceptor>().NamedAutomatically("typed.fac.interceptor"));

            var converter = Kernel.GetConversionManager();
            AddFactories(FacilityConfig, converter);
        }
    }
}
