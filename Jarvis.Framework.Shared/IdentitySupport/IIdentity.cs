using System;

namespace Jarvis.Framework.Shared.IdentitySupport
{
    // https://github.com/beingtheworst/btw-samples/blob/master/E012-now-serving-dsl/sample-csharp/E012.Domain/AbstractIdentity.cs

    /// <summary>
    /// Strongly-typed identity class. Essentially just an ID with a 
    /// distinct type. It introduces strong-typing and speeds up development
    /// on larger projects. Idea by Jeremie, implementation by Rinat
    /// 
    /// </summary>
    public interface IIdentity : IEquatable<IIdentity>
    {
        /// <summary>
        /// Gets the id, converted to a string.
        /// </summary>
        /// <returns>Identity converted to string</returns>
        string AsString();
    }
}
