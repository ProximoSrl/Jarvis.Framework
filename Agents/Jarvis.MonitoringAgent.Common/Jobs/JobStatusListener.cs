using Jarvis.MonitoringAgent.Common.Data;
using MongoDB.Bson;
using MongoDB.Driver;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.MonitoringAgent.Common.Jobs
{
    public class JobStatusListener : IJobListener
    {
        public String Name { get { return "JobStatusListener"; } }

        private IMongoCollection<JobStatus> _collection;

        public JobStatusListener(IMongoDatabase db)
        {
            _collection = db.GetCollection<JobStatus>("jobStatus");
        }

        public void JobToBeExecuted(IJobExecutionContext context)
        {
        }

        public void JobExecutionVetoed(IJobExecutionContext context)
        {
        }

        public void JobWasExecuted(
            IJobExecutionContext context, 
            JobExecutionException jobException)
        {

            UpdateStatus(context, jobException);
        }

        private void UpdateStatus(
            IJobExecutionContext context, 
            JobExecutionException exception)
        {
            try
            {
                var jobId = context.JobDetail.JobType.Name;
                var data = _collection.FindSync(Builders<JobStatus>.Filter.Eq(j => j.JobId, jobId))
                    .Single();

                if (data == null)
                {
                    data = new JobStatus();
                    data.JobId = jobId;
                }

                data.LastExecution = DateTime.UtcNow;
                data.AddExecutionList(context, exception);
                _collection.ReplaceOneAsync(
                       x => x.JobId == data.JobId,
                       data,
                       new UpdateOptions { IsUpsert = true }); 
            }
            catch (Exception ex)
            {

                throw;
            }
          
        }
    }
}
