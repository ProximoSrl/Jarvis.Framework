#if NETSTANDARD

using System.Collections.Concurrent;
using System.Threading;

namespace Jarvis.Framework.Shared.Support
{
    /// <summary>
    /// Provides a way to set contextual data that flows with the call and 
    /// async context of a test or invocation.
    /// </summary>
    public static class NetCoreCallContext
    {
        static ConcurrentDictionary<string, AsyncLocal<object>> state = new ConcurrentDictionary<string, AsyncLocal<object>>();

        /// <summary>
        /// Stores a given object and associates it with the specified name.
        /// </summary>
        /// <param name="name">The name with which to associate the new item in the call context.</param>
        /// <param name="data">The object to store in the call context.</param>
        public static void LogicalSetData(string name, object data) =>
            state.GetOrAdd(name, _ => new AsyncLocal<object>()).Value = data;

        /// <summary>
        /// Retrieves an object with the specified name from the <see cref="CallContext"/>.
        /// </summary>
        /// <param name="name">The name of the item in the call context.</param>
        /// <returns>The object in the call context associated with the specified name, or <see langword="null"/> if not found.</returns>
        public static object LogicalGetData(string name) =>
            state.TryGetValue(name, out AsyncLocal<object> data) ? data.Value : null;

        public static void FreeNamedDataSlot(string name) =>
            state.TryRemove(name, out AsyncLocal<object> data);
    }

    /// <summary>
    /// Call context generic.
    /// </summary>
    /// <typeparam name="T">The type of the data being retrieved. Must match the type used when the name parameter was set via <see cref="SetData{T}(string, T)"/>.</typeparam>
    public static class CallContext<T>
    {
        static readonly ConcurrentDictionary<string, AsyncLocal<T>> state = new ConcurrentDictionary<string, AsyncLocal<T>>();

        /// <summary>
        /// Stores a given object and associates it with the specified name.
        /// </summary>
        /// <param name="name">The name with which to associate the new item in the call context.</param>
        /// <param name="data">The object to store in the call context.</param>
        public static void LogicalSetData(string name, T data) =>
            state.GetOrAdd(name, _ => new AsyncLocal<T>()).Value = data;

        /// <summary>
        /// Retrieves an object with the specified name from the <see cref="CallContext"/>.
        /// </summary>
        /// <param name="name">The name of the item in the call context.</param>
        /// <returns>The object in the call context associated with the specified name, or a default value for <typeparamref name="T"/> if none is found.</returns>
        public static T LogicalGetData(string name) =>
            state.TryGetValue(name, out AsyncLocal<T> data) ? data.Value : default(T);

        public static void FreeNamedDataSlot(string name) =>
            state.TryRemove(name, out AsyncLocal<T> _);
    }
}
#endif