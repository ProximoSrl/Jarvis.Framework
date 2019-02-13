using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
	/// <summary>
	/// Class that contains information about projection change.
	/// </summary>
	public class ProjectionChangeInfo
	{
		public ProjectionChangeInfo(
			string commonName,
			string actualSlot,
			string actualSignature,
			Boolean offlineProjection)
		{
			CommonName = commonName;
			ActualSlot = actualSlot;
			ActualSignature = actualSignature;
			OfflineProjection = offlineProjection;
		}

		public void SetNew()
		{
			ChangeType = ProjectionChangeType.NewProjection;
		}

		internal void AddChangeSlot(String oldSlotName)
		{
			ChangeType |= ProjectionChangeType.SlotChange;
			OldSlot = oldSlotName;
		}

		internal void AddChangeSignature(String oldSignature)
		{
			ChangeType |= ProjectionChangeType.SignatureChange;
			OldSignature = oldSignature;
		}

		internal void SetMissing()
		{
			ChangeType |= ProjectionChangeType.Missing;
		}

		public ProjectionChangeType ChangeType { get; set; }

		public String CommonName { get; private set; }

		public String OldSlot { get; private set; }

		public String ActualSlot { get; private set; }

		public String OldSignature { get; private set; }

		public String ActualSignature { get; private set; }

		public Boolean OfflineProjection { get; set; }

		[Flags]
		public enum ProjectionChangeType
		{
			None = 0,
            /// <summary>
            /// This is a new projection that have not record in checkpoints collection
            /// </summary>
			NewProjection = 1,

            /// <summary>
            /// This projection was moved to a new slot
            /// </summary>
			SlotChange = 2,

            /// <summary>
            /// This projection changed signature
            /// </summary>
			SignatureChange = 4,

            /// <summary>
            /// this projection is present in checkpoint, but is not present anymore
            /// in code.
            /// </summary>
			Missing = 8,
		}
	}
}
