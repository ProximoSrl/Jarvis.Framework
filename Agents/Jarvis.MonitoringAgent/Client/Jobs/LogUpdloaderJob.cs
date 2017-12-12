using Castle.Core.Logging;
using Jarvis.MonitoringAgent.Support;
using Quartz;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Jarvis.MonitoringAgent.Common.Jarvis.MonitoringAgent.Common;

namespace Jarvis.MonitoringAgent.Client.Jobs
{
    [DisallowConcurrentExecution]
    public class LogUploaderJob : IJob
    {
        public ILogger Logger { get; set; }

        private readonly MonitoringAgentConfiguration _configuration;

        public LogUploaderJob(MonitoringAgentConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void Execute(IJobExecutionContext context)
        {
            StringBuilder retMessage = new StringBuilder();
            foreach (var fileToUpload in _configuration.UploadQueueFolder.GetFiles("*.logdump"))
            {
                try
                {
                    UploadFile(fileToUpload);
                    retMessage.AppendFormat("Uploaded {0}\n", fileToUpload);
                }
                catch (Exception ex)
                {
                    retMessage.AppendFormat("Error uploading {0} - {1}\n", fileToUpload, ex.Message);
                }
            }
            if (retMessage.Length > 10000)
            {
                retMessage.Length = 10000;
                retMessage.Append("...");
            }
            context.Result = retMessage.ToString();
        }

        private void UploadFile(FileInfo fileToUpload)
        {
            Logger.DebugFormat("Encrypting File {0}", fileToUpload.FullName);
            var zippedFileName = Path.ChangeExtension(fileToUpload.FullName, ".zip");
            if (File.Exists(zippedFileName)) File.Delete(zippedFileName);
            using (ZipArchive archive = ZipFile.Open(zippedFileName, ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(fileToUpload.FullName, fileToUpload.Name);
            }

            var encryptedFileName = fileToUpload.FullName + ".encrypted";
            if (File.Exists(encryptedFileName))
            {
                File.Delete(encryptedFileName);
            }
            var encryptedKey = EncryptionUtils.EncryptFile(
                zippedFileName,
                encryptedFileName,
                _configuration.Key);

            var keyFileName = fileToUpload.FullName + ".key";
            File.WriteAllText(keyFileName, encryptedKey);

            //now delete the original zip, and re-create the zip adding key and encrypted file
            File.Delete(zippedFileName);

            Logger.DebugFormat("Creating Zip File {0} from logfile {1}", zippedFileName, fileToUpload.FullName);
            using (ZipArchive archive = ZipFile.Open(zippedFileName, ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(encryptedFileName, Path.GetFileName(encryptedFileName));
                archive.CreateEntryFromFile(keyFileName, Path.GetFileName(keyFileName));
            }

            var url = String.Format("{0}/api/logs/upload/{1}",
                _configuration.ServerAddress, _configuration.CustomerId);
            using (WebClient client = new WebClient())
            {
                client.UploadFile(url, zippedFileName);
            }

            File.Delete(fileToUpload.FullName);
            File.Delete(zippedFileName);
            File.Delete(encryptedFileName);
            File.Delete(keyFileName);
        }
    }
}
