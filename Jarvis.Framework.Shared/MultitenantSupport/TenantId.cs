using System;
using System.ComponentModel;
using Jarvis.Framework.Shared.Domain;
using Jarvis.Framework.Shared.Domain.Serialization;

namespace Jarvis.Framework.Shared.MultitenantSupport
{
    [TypeConverter(typeof(StringValueTypeConverter<TenantId>))]
    [Serializable]
    public class TenantId : LowercaseStringValue
    {
        public TenantId(string value)
            : base(value)
        {
        }
    }
}