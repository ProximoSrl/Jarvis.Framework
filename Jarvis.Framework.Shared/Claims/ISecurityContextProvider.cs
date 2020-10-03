using System;
using System.Collections.Generic;
using Jarvis.Framework.Shared.Support;

namespace Jarvis.Framework.Shared.Claims
{
    /// <summary>
    /// <para>
    /// This interface identifies a provider capable to check and set claims for a give context.
    /// </para>
    /// <para>
    /// This class is used to retrieve a set of claim of current context. It is used
    /// by command handlers that verify current claim for a user, then set the current
    /// context list for the current execution of command.
    /// </para>
    /// <para>
    /// The purpose of this class is making claim handling completely automatic, giving the 
    /// user of the framework the duty to handle security context.
    /// </para>
    /// </summary>
    public interface ISecurityContextProvider
    {
        /// <summary>
        /// Return the list of claim of the currently executing code.
        /// </summary>
        /// <returns></returns>
        IEnumerable<Claim> GetCurrentClaims();
    }

    /// <summary>
    /// This interface identify a component that is capable not only of 
    /// getting current claims, but also to set claim for a given context.
    /// </summary>
    public interface ISecurityContextManager : ISecurityContextProvider
    {
        /// <summary>
        /// Sets claims for a currently executing piece of code
        /// </summary>
        /// <param name="claims"></param>
        /// <returns>A disposable action that will remove all claims once the scope is finished.</returns>
        IDisposable SetCurrentClaims(IEnumerable<Claim> claims);
    }

    public class NullSecurityContextManager : ISecurityContextManager
    {
        public static readonly NullSecurityContextManager Instance = new NullSecurityContextManager();

        public IEnumerable<Claim> GetCurrentClaims()
        {
            return new Claim[0];
        }

        public IDisposable SetCurrentClaims(IEnumerable<Claim> claims)
        {
            return new DisposableAction(() => { });
        }
    }
}
