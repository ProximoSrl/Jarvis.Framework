using System;
using System.Collections.Generic;
using System.Text;

namespace Jarvis.Framework.Shared.Commands.Tracking
{
    public class TrackedMessageModelPaginated
    {
        /// <summary>
        /// Total page of the 
        /// </summary>
        public int TotalPages { get; set; }

        /// <summary>
        /// List of data
        /// </summary>
        public TrackedMessageModel[] Messages { get; set; }
    }
}
