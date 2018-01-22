using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.ElasticLogPoller.Importers
{
	public class ImportException : Exception
	{
		public ImportException() : base()
		{
		}

		public ImportException(string message) : base(message)
		{
		}

		public ImportException(string message, Exception innerException) : base(message, innerException)
		{
		}

		protected ImportException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
		{
		}
	}
}
