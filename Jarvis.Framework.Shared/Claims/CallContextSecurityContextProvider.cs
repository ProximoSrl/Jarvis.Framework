namespace Jarvis.Framework.Shared.Claims
{
    using Jarvis.Framework.Shared.Support;
    using System;
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>
    /// Standard implementation of <see cref="ISecurityContextManager"/> that will use
    /// the standard AsyncLocal helper class
    /// </summary>
    public class AsyncLocalSecurityContextProvider : ISecurityContextManager
    {
        private readonly AsyncLocal<ContextDataEntry> _asyncLocal;

        public AsyncLocalSecurityContextProvider()
        {
            _asyncLocal = new AsyncLocal<ContextDataEntry>();
        }

        public IEnumerable<Claim> GetCurrentClaims()
        {
            return GetDataEntry().Claims;
        }

        private ContextDataEntry GetDataEntry()
        {
            return _asyncLocal.Value ?? ContextDataEntry.Empty;
        }

        /// <summary>
        /// Disposable is not really necessary, AsyncLocal is reset after the method exits
        /// </summary>
        /// <param name="claims"></param>
        /// <returns></returns>
        public IDisposable SetCurrentClaims(IEnumerable<Claim> claims)
        {
            _asyncLocal.Value = new ContextDataEntry(claims);
            return new DisposableAction(ResetContext);
        }

        private void ResetContext()
        {
            _asyncLocal.Value = ContextDataEntry.Empty;
        }

        private class ContextDataEntry
        {
            public readonly static ContextDataEntry Empty = new ContextDataEntry(new List<Claim>());

            public ContextDataEntry(IEnumerable<Claim> claims)
            {
                Claims = claims;
            }

            public IEnumerable<Claim> Claims { get; set; }
        }
    }
}
