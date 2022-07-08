using Castle.Core.Logging;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Support;
using Newtonsoft.Json.Serialization;
using System;
using System.Runtime.CompilerServices;

namespace Jarvis.Framework.Rebus.Support
{
    public class JarvisFrameworkRebusSerializationBinder : DefaultSerializationBinder
    {
        private readonly ILogger _logger;

        public JarvisFrameworkRebusSerializationBinder(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// https://stackoverflow.com/questions/59991500/how-to-cleanly-send-data-between-net-core-and-net-framework-apps-serializing
        /// </summary>
        /// <param name="serializedType"></param>
        /// <param name="assemblyName"></param>
        /// <param name="typeName"></param>
        public override void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            base.BindToName(serializedType, out assemblyName, out typeName);
            if (_logger.IsDebugEnabled) _logger.DebugFormat("JarvisFrameworkRebusSerializationBinder: BindToName {0} -> assembly: {1} typeName {2}", serializedType.FullName, assemblyName, typeName);

            //fix type forwarded for outer type.
            var typeToCheckForForward = serializedType;
            if (serializedType.IsArray)
            {
                typeToCheckForForward = serializedType.GetElementType();
            }

            if (Attribute.GetCustomAttribute(typeToCheckForForward, typeof(TypeForwardedFromAttribute), false) is TypeForwardedFromAttribute attr)
            {
                var forwardedAssemblyName = attr.AssemblyFullName;
                if (_logger.IsDebugEnabled) _logger.DebugFormat("JarvisFrameworkRebusSerializationBinder: forwarded {0} to {1} [type {2}]", assemblyName, forwardedAssemblyName, serializedType.FullName);
                assemblyName = forwardedAssemblyName;
            }

            typeName = serializedType.GetFullNameWithTypeForwarded();

            FixType(serializedType, ref typeName);
        }

        private void FixType(Type serializedType, ref string typeName)
        {
            if (Attribute.GetCustomAttribute(serializedType, typeof(TypeForwardedFromAttribute), false) is TypeForwardedFromAttribute attr)
            {
                typeName = typeName.Replace(serializedType.AssemblyQualifiedName, $"{serializedType.FullName}, {attr.AssemblyFullName}");
            }

            if (serializedType.IsArray)
            {
                var elementType = serializedType.GetElementType();
                if (Attribute.GetCustomAttribute(elementType, typeof(TypeForwardedFromAttribute), false) is TypeForwardedFromAttribute typeArrAttr)
                {
                    typeName = typeName.Replace(serializedType.AssemblyQualifiedName, $"{serializedType.FullName}[], {typeArrAttr.AssemblyFullName}");
                }
            }

            if (serializedType.IsGenericType)
            {
                //Get type arguments
                foreach (var type in serializedType.GetGenericArguments())
                {
                    FixType(type, ref typeName);
                }
            }
        }

        public override Type BindToType(string assemblyName, string typeName)
        {
            try
            {
                var type = base.BindToType(assemblyName, typeName);

                if (_logger.IsDebugEnabled) _logger.DebugFormat("JarvisFrameworkRebusSerializationBinder: BindToType {0}/{1} -> {2}", assemblyName, typeName, type.FullName);
                return type;
            }
            //catch (JsonSerializationException sex) 
            //{
            //    //ok we have problem in serialization, it could be that some
            //    _logger.ErrorFormat(sex, "JarvisFrameworkRebusSerializationBinder: BindToType ERROR {0}/{1}", assemblyName, typeName);
            //    return Type.GetType(typeName, true); 
            //}
            catch (Exception ex)
            {
                _logger.ErrorFormat(ex, "JarvisFrameworkRebusSerializationBinder: BindToType ERROR {0}/{1}", assemblyName, typeName);
                throw new JarvisFrameworkEngineException($"Cannot bind to type {typeName} in assembly {assemblyName}");
            }
        }
    }
}
