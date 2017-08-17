//using System;
//using System.Collections.Generic;
//using System.Globalization;
//using System.Text;
//using System.Threading;
//using Jarvis.Framework.Shared.Domain.Serialization;
//using Jarvis.Framework.Shared.Messages;
//using Newtonsoft.Json;
//using Newtonsoft.Json.Serialization;
//using Rebus;
//using Rebus.Messages;
//using Rebus.Serialization.Json;
//using Rebus.Shared;

//namespace Jarvis.Framework.Bus.Rebus.Integration.Serializers
//{
//    /// <summary>
//    /// Implementation of <see cref="ISerializeMessages"/> that uses Newtonsoft JSON.NET internally to serialize transport messages.
//    /// The used JSON.NET DLL is merged into Rebus, which allows it to be used by Rebus without bothering people by an extra dependency.
//    /// 
//    /// JSON.NET has the following license:
//    /// ----------------------------------------------------------------------------------------------------------------------------
//    /// Copyright (c) 2007 James Newton-King
//    /// 
//    /// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation 
//    /// files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, 
//    /// modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software 
//    /// is furnished to do so, subject to the following conditions:
//    /// 
//    /// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
//    /// 
//    /// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
//    /// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS 
//    /// BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF 
//    /// OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//    /// ----------------------------------------------------------------------------------------------------------------------------
//    /// Bam!1 Thanks James :)
//    /// </summary>
//    public class CustomJsonSerializer : ISerializeMessages
//    {
//        const string JsonContentTypeName = "text/json";

//        static readonly Encoding DefaultEncoding = Encoding.UTF7;

//        readonly JsonSerializerSettings settings =
//            new JsonSerializerSettings
//            {
//                TypeNameHandling = TypeNameHandling.All,
//                Converters = new JsonConverter[]
//                {
//                    new StringValueJsonConverter()
//                }
//            };

//        readonly CultureInfo serializationCulture = CultureInfo.InvariantCulture;

//        readonly NonDefaultSerializationBinder binder;

//        /// <summary>
//        /// Constructs the serializer
//        /// </summary>
//        public CustomJsonSerializer()
//        {
//            binder = new NonDefaultSerializationBinder();
//            settings.Binder = binder;

//            // PRXM
//            settings.ContractResolver = new MessagesContractResolver();
//            settings.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor;
//            // PRXM
//        }

//        /// <summary>
//        /// Serializes the transport message <see cref="Message"/> using JSON.NET and wraps it in a <see cref="TransportMessageToSend"/>
//        /// </summary>
//        public TransportMessageToSend Serialize(Message message)
//        {
//            using (new CultureContext(serializationCulture))
//            {
//                var messageAsString = JsonConvert.SerializeObject(message.Messages, Formatting.Indented, settings);

//                var headers = message.Headers.Clone();
//                headers[Headers.ContentType] = JsonContentTypeName;
//                headers[Headers.Encoding] = DefaultEncoding.WebName;

//                return new TransportMessageToSend
//                {
//                    Body = DefaultEncoding.GetBytes(messageAsString),
//                    Headers = headers,
//                    Label = message.GetLabel(),
//                };
//            }
//        }

//        /// <summary>
//        /// Deserializes the transport message using JSON.NET from a <see cref="ReceivedTransportMessage"/> and wraps it in a <see cref="Message"/>
//        /// </summary>
//        public Message Deserialize(ReceivedTransportMessage transportMessage)
//        {
//            using (new CultureContext(serializationCulture))
//            {
//                var headers = transportMessage.Headers.Clone();
//                var encodingToUse = GetEncodingOrThrow(headers);

//                var serializedTransportMessage = encodingToUse.GetString(transportMessage.Body);
//                try
//                {
//                    var messages = (object[])JsonConvert.DeserializeObject(serializedTransportMessage, settings);

//                    return new Message
//                    {
//                        Headers = headers,
//                        Messages = messages
//                    };
//                }
//                catch (Exception e)
//                {
//                    throw new ArgumentException(
//                        string.Format(
//                            "An error occurred while attempting to deserialize JSON text '{0}' into an object[]",
//                            serializedTransportMessage), e);
//                }
//            }
//        }

//        Encoding GetEncodingOrThrow(IDictionary<string, object> headers)
//        {
//            if (!headers.ContainsKey(Headers.ContentType))
//            {
//                throw new ArgumentException(
//                    string.Format("Received message does not have a proper '{0}' header defined!",
//                                  Headers.ContentType));
//            }

//            var contentType = (headers[Headers.ContentType] ?? "").ToString();
//            if (!JsonContentTypeName.Equals(contentType, StringComparison.InvariantCultureIgnoreCase))
//            {
//                throw new ArgumentException(
//                    string.Format(
//                        "Received message has content type header with '{0}' which is not supported by the JSON serializer!",
//                        contentType));
//            }

//            if (!headers.ContainsKey(Headers.Encoding))
//            {
//                throw new ArgumentException(
//                    string.Format(
//                        "Received message has content type '{0}', but the corresponding '{1}' header was not present!",
//                        contentType, Headers.Encoding));
//            }

//            var encodingWebName = (headers[Headers.Encoding] ?? "").ToString();

//            try
//            {
//                return Encoding.GetEncoding(encodingWebName);
//            }
//            catch (Exception e)
//            {
//                throw new ArgumentException(
//                    string.Format("An error occurred while attempting to treat '{0}' as a proper text encoding",
//                                  encodingWebName), e);
//            }
//        }

//        class CultureContext : IDisposable
//        {
//            readonly CultureInfo currentCulture;
//            readonly CultureInfo currentUiCulture;

//            public CultureContext(CultureInfo cultureInfo)
//            {
//                var thread = Thread.CurrentThread;

//                currentCulture = thread.CurrentCulture;
//                currentUiCulture = thread.CurrentUICulture;

//                thread.CurrentCulture = cultureInfo;
//                thread.CurrentUICulture = cultureInfo;
//            }

//            public void Dispose()
//            {
//                var thread = Thread.CurrentThread;

//                thread.CurrentCulture = currentCulture;
//                thread.CurrentUICulture = currentUiCulture;
//            }
//        }

//        /// <summary>
//        /// JSON.NET serialization binder that can be extended with a pipeline of name and type resolvers,
//        /// allowing for customizing how types are bound
//        /// </summary>
//        class NonDefaultSerializationBinder : DefaultSerializationBinder
//        {
//            readonly List<Func<Type, TypeDescriptor>> nameResolvers = new List<Func<Type, TypeDescriptor>>();
//            readonly List<Func<TypeDescriptor, Type>> typeResolvers = new List<Func<TypeDescriptor, Type>>();

//            public override void BindToName(Type serializedType, out string assemblyName, out string typeName)
//            {
//                foreach (var tryResolve in nameResolvers)
//                {
//                    var typeDescriptor = tryResolve(serializedType);

//                    if (typeDescriptor != null)
//                    {
//                        assemblyName = typeDescriptor.AssemblyName;
//                        typeName = typeDescriptor.TypeName;
//                        return;
//                    }
//                }

//                base.BindToName(serializedType, out assemblyName, out typeName);
//            }

//            public override Type BindToType(string assemblyName, string typeName)
//            {
//                foreach (var tryResolve in typeResolvers)
//                {
//                    var typeDescriptor = new TypeDescriptor(assemblyName, typeName);
//                    var type = tryResolve(typeDescriptor);

//                    if (type != null)
//                    {
//                        return type;
//                    }
//                }

//                return base.BindToType(assemblyName, typeName);
//            }

//            public void Add(Func<Type, TypeDescriptor> resolve)
//            {
//                nameResolvers.Add(resolve);
//            }

//            public void Add(Func<TypeDescriptor, Type> resolve)
//            {
//                typeResolvers.Add(resolve);
//            }
//        }

//        /// <summary>
//        /// Adds the specified function to the pipeline of resolvers that can get a <see cref="TypeDescriptor"/>
//        /// from a .NET type. If the function returns null, it means that it doesn't care and the next resulver
//        /// will be called, until ultimately it will fall back to default JSON.NET behavior
//        /// </summary>
//        public void AddNameResolver(Func<Type, TypeDescriptor> resolver)
//        {
//            binder.Add(resolver);
//        }

//        /// <summary>
//        /// Adds the specified function to the pipeline of resolvers that can get a .NET type from a
//        /// <see cref="TypeDescriptor"/>. If the function returns null, it means that it doesn't care and the next resulver
//        /// will be called, until ultimately it will fall back to default JSON.NET behavior
//        /// </summary>
//        public void AddTypeResolver(Func<TypeDescriptor, Type> resolver)
//        {
//            binder.Add(resolver);
//        }
//    }

//    internal static class DictExt
//    {
//        public static IDictionary<TKey, TValue> Clone<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
//        {
//            return dictionary == null ? new Dictionary<TKey, TValue>() : new Dictionary<TKey, TValue>(dictionary);
//        }

//        public static TValue ValueOrNull<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key) where TValue : class
//        {
//            return dictionary.ContainsKey(key)
//                       ? dictionary[key]
//                       : null;
//        }
//    }
//}
