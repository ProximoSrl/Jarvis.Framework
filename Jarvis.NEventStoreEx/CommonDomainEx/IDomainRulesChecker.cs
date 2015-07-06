using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.NEventStoreEx.CommonDomainEx
{
    public interface IDomainRulesChecker
    {
        /// <summary>
        /// Check all external rules for the domain, if some rules is not satisfied
        /// it will raise a DomainException
        /// </summary>
        void CheckRules(); 
    }
}
