using System;
using System.ComponentModel;
using System.Globalization;

namespace Jarvis.Framework.Shared.Domain.Serialization
{
    public class StringValueTypeConverter<T> : TypeConverter where T : StringValue
    {
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
            return Activator.CreateInstance(typeof(T), new object[] {(string) value});
        }
    }
}
