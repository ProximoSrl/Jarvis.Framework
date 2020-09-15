using System;

namespace Jarvis.Framework.Shared.Messages
{
    /// <summary>
    /// http://mookid.dk/oncode/archives/2966
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class TimeToBeReceivedAttribute : Attribute
    {
        private readonly string _hmsString;

        public TimeToBeReceivedAttribute(string hmsString)
        {
            this._hmsString = hmsString;
        }

        public string HmsString
        {
            get { return _hmsString; }
        }
    }
}