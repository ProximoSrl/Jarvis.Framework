using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Shared.Helpers;
using MongoDB.Driver;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    /// <summary>
    /// Slot status Manager. 
    /// </summary>
    public class SlotStatusManager : ISlotStatusManager
    {
        /// <summary>
        /// Create a slot status checker for all <see cref="IProjection"/> contained
        /// in assemblies specified in the parameter <paramref name="assemblies"/>.
        /// This static constructor is needed because this component is usually needed
        /// at the very first moment when the process start, before all container is 
        /// usually configured. 
        /// This allows also to avoid the need to have all <see cref="ProjectionInfoAttribute"/>
        /// registered in Inversion of control engine.
        /// </summary>
        /// <param name="db"></param>
        /// <param name="assemblies"></param>
        /// <returns></returns>
        public static SlotStatusManager CreateForAllProjectionsInAssemblies(
            IMongoDatabase db,
            params Assembly[] assemblies)
        {
            List<ProjectionInfoAttribute> projectionAttributes = new List<ProjectionInfoAttribute>();
            foreach (var assembly in assemblies)
            {
                var allTypes = assembly.GetTypes();
                var allProjectionTypes = allTypes.Where(t => typeof(IProjection).IsAssignableFrom(t) && !t.IsAbstract);
                var allProjectionAttributes = allProjectionTypes.Select(p =>
                {
                    var attribute = p.GetCustomAttribute<ProjectionInfoAttribute>();
                    if (attribute == null)
                        throw new ApplicationException($"Projection type {p.FullName} does not have ProjectionInfoAttribute attribute");
                    return attribute;
                });
                projectionAttributes.AddRange(allProjectionAttributes);
            }
            return new SlotStatusManager(db, projectionAttributes.ToArray());
        }

        private readonly IMongoCollection<Checkpoint> _checkpoints;
        private readonly IList<Checkpoint> _allCheckpoints;
        private readonly ProjectionInfoAttribute[] _projectionInfo;

        /// <summary>
        /// Initialize the status checker, we should have registered also an 
        /// array of projection info.
        /// </summary>
        /// <param name="db"></param>
        /// <param name="projectionInfo"></param>
        public SlotStatusManager(IMongoDatabase db, ProjectionInfoAttribute[] projectionInfo)
        {
            _checkpoints = db.GetCollection<Checkpoint>("checkpoints");
            _allCheckpoints = GetAllCheckpoint();
            _projectionInfo = projectionInfo;
        }

        public IEnumerable<string> GetCheckpointErrors()
        {
            List<String> retValue = new List<string>();
            if (!RebuildSettings.ShouldRebuild)
            {
                //need to check every slot for new projection 
                var slots = _projectionInfo.GroupBy(p => p.SlotName);
                foreach (var slot in slots)
                {
                    Int64 minCheckpoint = Int32.MaxValue;
                    Int64 maxCheckpoint = 0;
                    foreach (var projection in slot)
                    {
                        string projectionId = projection.CommonName;
                        var checkpoint = _allCheckpoints.SingleOrDefault(c => c.Id == projectionId);
                        if (checkpoint != null)
                        {
                            if (checkpoint.Signature != projection.Signature)
                            {
                                retValue.Add(String.Format("Projection {0} [slot {1}] has signature {2} but checkpoint on database has signature {3}.\n REBUILD NEEDED",
                                    projectionId, slot.Key, projection.Signature, checkpoint.Signature));
                            }
                            if (checkpoint.Value > maxCheckpoint) maxCheckpoint = checkpoint.Value;
                            if (checkpoint.Value < minCheckpoint) minCheckpoint = checkpoint.Value;
                        }
                    }

                    if (minCheckpoint != maxCheckpoint)
                    {
                        String error;
                        //error, ve have not all projection at the same checkpoint 
                        var allCheckpoints = _allCheckpoints.Where(c => c.Slot == slot.Key);
                        if (allCheckpoints
                            .Where(k => k.Value != 0)
                            .Select(k => k.Value)
                            .Distinct().Count() == 1)
                        {
                            //We have one or more new projection in slot
                            error = String.Format("Error in slot {0}: we have new projections at checkpoint 0.\n REBUILD NEEDED!", slot.Key);
                        }
                        else
                        {
                            error = String.Format("Error in slot {0}, not all projection at the same checkpoint value. Please check reamodel db!", slot.Key);
                        }
                        retValue.Add(error);
                    }
                }
            }
            return retValue;
        }

        public CheckpointSlotStatus GetSlotsStatus()
        {
            CheckpointSlotStatus returnValue = new CheckpointSlotStatus();
            //we need to group projection per slot
            var allCheckpoint = _allCheckpoints
                .ToDictionary(c => c.Id, c => c);
            var slots = _projectionInfo.GroupBy(
                p => p.SlotName,
                p => new
                {
                    Projection = p,
                    Checkpoint = allCheckpoint.ContainsKey(p.CommonName) ? allCheckpoint[p.CommonName] : null,
                });
            Int64 maxCheckpoint = 0;
            if (allCheckpoint.Count > 0)
            {
                maxCheckpoint = allCheckpoint.Select(c => c.Value.Value).Max();
            }
            if (maxCheckpoint > 0) //if we have no dispatched commit, we have no need to rebuild or do anything
            {
                foreach (var slot in slots)
                {
                    if (slot.All(s => s.Checkpoint == null || s.Checkpoint.Value == 0))
                    {
                        returnValue.NewSlots.Add(slot.Key);
                    }
                    else if (slot.Where(s => s.Checkpoint != null)
                        .Select(s => s.Checkpoint.Value).Distinct().Count() > 1)
                    {
                        returnValue.SlotsThatNeedsRebuild.Add(slot.Key);
                    }
                    else if (slot.Any(s => s.Checkpoint == null || s.Checkpoint.Signature != s.Projection.Signature))
                    {
                        returnValue.SlotsThatNeedsRebuild.Add(slot.Key);
                    }
                }
            }
            return returnValue;
        }

        public Int64 GetMaxDispatchedCheckpoint()
        {
            return _checkpoints.FindAll()
                .ToList()
                .Where(c => c.Id != "VERSION")
                .Max(c => c.Value);
        }

        private List<Checkpoint> GetAllCheckpoint()
        {
            return _checkpoints.AsQueryable()
                .Where(q => q.Id != "VERSION")
                .ToList();
        }

        public IEnumerable<ProjectionChangeInfo> GetProjectionChangeInfo()
        {
            List<ProjectionChangeInfo> retValue = new List<ProjectionChangeInfo>();
            foreach (var projection in _projectionInfo)
            {
                ProjectionChangeInfo projectionChangeInfo = new ProjectionChangeInfo(projection.CommonName, projection.SlotName, projection.Signature);
                var checkpoint = _allCheckpoints.SingleOrDefault(c => c.Id == projection.CommonName);
                if (checkpoint == null)
                {
                    //New projection
                    projectionChangeInfo.SetNew();
                }
                else
                {
                    if (checkpoint.Signature != projection.Signature)
                        projectionChangeInfo.AddChangeSignature(checkpoint.Signature);

                    if (checkpoint.Slot != projection.SlotName)
                        projectionChangeInfo.AddChangeSlot(checkpoint.Slot);
                }
                retValue.Add(projectionChangeInfo);
            }

            foreach (var checkpoint in _allCheckpoints.Where(c => !_projectionInfo.Any(p => p.CommonName == c.Id)))
            {
                ProjectionChangeInfo projectionChangeInfo = new ProjectionChangeInfo(checkpoint.Id, checkpoint.Slot, checkpoint.Signature);
                projectionChangeInfo.SetMissing();
                retValue.Add(projectionChangeInfo);
            }
            return retValue;
        }

        private IEnumerable<ProjectionInfoAttribute> GetProjectionIdsFromSlot(String slotName)
        {
            return _projectionInfo
                .Where(p => p.SlotName.Equals(slotName, StringComparison.OrdinalIgnoreCase));
        }

        public void CreateSlotAtCheckpoint(
            string slotName,
            Int64 valueCheckpointToken)
        {
            foreach (var projection in GetProjectionIdsFromSlot(slotName))
            {
                Checkpoint cp = new Checkpoint(projection.CommonName, valueCheckpointToken, projection.Signature);
                cp.Slot = projection.SlotName;
                _checkpoints.Save(cp, cp.Id);
            }
        }
    }
}
