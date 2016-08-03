using Jarvis.NEventStoreEx.CommonDomainEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Unfolder
{
    public interface IQueryModelRepository : IDisposable
    {
        TUnfolder GetById<TUnfolder, TQueryModel>(IIdentity id, Int32 to)
            where TUnfolder : Projector<TQueryModel>
            where TQueryModel : BaseAggregateQueryModel, new();
    }

    public interface IProjectorManager
    {
        /// <summary>
        /// This is an helper function that help to create the projector, apply all 
        /// events and immediately returns the resulting <see cref="BaseAggregateQueryModel"/>
        /// that is the result of the operation.
        /// </summary>
        /// <typeparam name="TUnfolder"></typeparam>
        /// <typeparam name="TQueryModel"></typeparam>
        /// <param name="id"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        TQueryModel Project<TUnfolder, TQueryModel>(IIdentity id, Int32 to)
            where TUnfolder : Projector<TQueryModel>
            where TQueryModel : BaseAggregateQueryModel, new();
    }
}
