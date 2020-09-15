using Fasterflect;
using Jarvis.Framework.Shared.Helpers;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;

namespace Jarvis.Framework.Shared.Domain.Serialization
{
    public class StringValueTypeConverter<T> : TypeConverter where T : StringValue
    {
#pragma warning disable S2743 // We know that this is a static field in generic class, but it is used as cache.
        private static readonly ConcurrentDictionary<Type, FastReflectionHelper.ObjectActivator> _activators
          = new ConcurrentDictionary<Type, FastReflectionHelper.ObjectActivator>();
#pragma warning restore S2743

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof(string))
            {
                return true;
            }
            return base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            var nominalType = typeof(T);
            FastReflectionHelper.ObjectActivator activator;
            if (!_activators.TryGetValue(nominalType, out activator))
            {
                var ctor = nominalType.Constructor(new Type[] { typeof(string) });
                activator = FastReflectionHelper.GetActivator(ctor);
                _activators[nominalType] = activator;
            }
            return activator(new object[] { (string)value });
        }
    }
}
