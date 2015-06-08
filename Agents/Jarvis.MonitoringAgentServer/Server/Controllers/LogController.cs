using Castle.Core.Logging;
using Jarvis.MonitoringAgent.Common.Jarvis.MonitoringAgent.Common;
using Jarvis.MonitoringAgentServer.Server.Data;
using Jarvis.MonitoringAgentServer.Support;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace Jarvis.MonitoringAgentServer.Server.Controllers
{
    public class LogController : ApiController
    {
        public ILogger Logger { get; set; }

        MonitoringAgentServerConfiguration _configuration;

        public MongoCollection<Customer> _customers { get; set; }

        public MongoDatabase _mongoDatabase { get; set; }

        public LogController(
            MonitoringAgentServerConfiguration configuration, 
            MongoCollection<Customer> customers,
            MongoDatabase mongoDatabase)
        {
            _configuration = configuration;
            _customers = customers;
            _mongoDatabase = mongoDatabase;
        }

        [Route("api/logs/upload/{customerId}")]
        [HttpPost]
        public async Task<HttpResponseMessage> Upload(String customerId)
        {
            HttpRequestMessage request = this.Request;
            if (!request.Content.IsMimeMultipartContent())
            {
                throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);
            }


            var provider = new MultipartFormDataStreamProvider(_configuration.UploadQueueFolder.FullName);
            var readContent = await request.Content.ReadAsMultipartAsync(provider);
            String fileName = null;
            String tempFolder = null;
            try
            {
                // this is the file name on the server where the file was saved 
                var file = provider.Contents.Single();
                var fileData = provider.FileData.Single();
                fileName = fileData.LocalFileName;
                Logger.DebugFormat("Received file {0}", fileName);

                //unzipping to temp folder
                tempFolder = Path.Combine(_configuration.UploadQueueFolder.FullName, Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempFolder);

                ZipFile.ExtractToDirectory(fileName, tempFolder);
                //we expect two files, one zip archived file and the key file
                Logger.DebugFormat("File {0} Unzipped to {1}", fileName, tempFolder);

                var files = Directory.GetFiles(tempFolder);
                if (files.Length != 2)
                {
                    Logger.ErrorFormat("File {0} unzipped contains {1} files", fileName, files.Length);
                    return Request.CreateErrorResponse(
                       HttpStatusCode.BadRequest,
                       "Log file upload should contains only 2 files"
                   );
                }

                var keyFile = files.SingleOrDefault(f => f.EndsWith("key"));
                if (keyFile == null)
                {
                    Logger.ErrorFormat("File {0} Missing key file from Zip", fileName);
                    return Request.CreateErrorResponse(
                       HttpStatusCode.BadRequest,
                       String.Format("File {0} Missing key file from Zip", fileName)
                   );
                }
                var encryptedFile = files.SingleOrDefault(f => f.EndsWith("encrypted"));
                if (encryptedFile == null)
                {
                    Logger.ErrorFormat("File {0} Missing encrypted file from Zip", fileName);
                    return Request.CreateErrorResponse(
                       HttpStatusCode.BadRequest,
                       String.Format("File {0} Missing encrypted file from Zip", fileName)
                   );
                }

                var customer = _customers.FindOneById(BsonValue.Create(customerId));
                if (customer == null)
                {
                    Logger.ErrorFormat("Customer {0} Missing", customerId);
                    return Request.CreateErrorResponse(
                       HttpStatusCode.NotFound,
                       String.Format("Customer {0} Missing", customerId)
                    );
                }

                DecryptFile(tempFolder, keyFile, encryptedFile, customer);

                //now we can read all logdump that there are in the directory
                ImportLogfiles(tempFolder, customer);

                return new HttpResponseMessage()
                {
                    Content = new StringContent("File uploaded.")
                };
            }
            catch (CryptographicException)
            {
                return Request.CreateErrorResponse(
                      HttpStatusCode.InternalServerError,
                      String.Format("Invalid key for Customer {0}", customerId)
                   );
            }
            finally
            {
                try
                {
                    if (File.Exists(fileName)) File.Delete(fileName);
                    if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
                }
                catch (Exception ex)
                {
                    Logger.ErrorFormat(ex, "Error doing folder cleanup {0}", tempFolder);
                }
            }


        }

        private void ImportLogfiles(string tempFolder, Customer customer)
        {
            var filesWithLogDumps = Directory.GetFiles(tempFolder, "*.logdump");
            var collection = _mongoDatabase.GetCollection<BsonDocument>("logs." + customer.Name);
            foreach (var logDumpFile in filesWithLogDumps)
            {
                foreach (var log in File.ReadLines(logDumpFile))
                {
                    BsonDocument doc = BsonSerializer.Deserialize<BsonDocument>(log);
                    collection.Save(doc);
                }
            }
        }

        private void DecryptFile(string tempFolder, string keyFile, string encryptedFile, Customer customer)
        {
            var encryptionKeyString = File.ReadAllText(keyFile);
            var encryptedKey = EncryptionKey.CreateFromSerializedString(encryptionKeyString);
            var asymmetricKey = AsymmetricEncryptionKey.CreateFromString(customer.PrivateKey, true);
            var decryptedKey = EncryptionUtils.Decrypt(asymmetricKey, encryptedKey);

            //now we can decrypt
            var decryptedFileName = Path.Combine(tempFolder, "decrypted.zip");
            EncryptionUtils.DecryptFile(decryptedKey, encryptedFile, decryptedFileName);
            Logger.DebugFormat("Decrypted file {0} ", decryptedFileName);

            //now we can unzip
            ZipFile.ExtractToDirectory(decryptedFileName, tempFolder);
            Logger.DebugFormat("Unzipped file {0} ", decryptedFileName);
        }
    }
}
