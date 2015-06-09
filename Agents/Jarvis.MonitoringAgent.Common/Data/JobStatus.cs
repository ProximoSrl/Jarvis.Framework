using MongoDB.Bson.Serialization.Attributes;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.MonitoringAgent.Common.Data
{
    public class JobStatus
    {
        [BsonId]
        public String JobId { get; set; }

        public DateTime LastExecution { get; set; }

        public Boolean LastExecutionStatus { get; set; }

        public List<SingleJobExecutionStatus> ExecutionList { get; set; }

        public void AddExecutionList(
            IJobExecutionContext context,
            JobExecutionException exception)
        {
            if (ExecutionList == null)
                ExecutionList = new List<SingleJobExecutionStatus>();

            if (ExecutionList.Count >= 20)
            {
                ExecutionList = ExecutionList
                    .Take(19)
                    .ToList();
            }
            SingleJobExecutionStatus status = new SingleJobExecutionStatus()
            {
                ExecutionStatus = exception == null,
                ExecutionTimestamp = DateTime.UtcNow,
            };
            if (context.Result != null)
            {
                status.Message = context.Result.ToString();
            }
            if (exception != null)
            {
                status.ExceptionMessage = exception.ToString();
            }
            ExecutionList.Insert(0, status);
        }
    }

    public class SingleJobExecutionStatus
    {
        public String Message { get; set; }

        public Boolean ExecutionStatus { get; set; }

        public DateTime ExecutionTimestamp { get; set; }

        public String ExceptionMessage { get; set; }
    }
}
