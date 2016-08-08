using System.Linq;
using System.Web.Http;
using Jarvis.MonitoringAgent.Common;
using Jarvis.MonitoringAgent.Common.Jarvis.MonitoringAgent.Common;
using Jarvis.MonitoringAgentServer.Server.Controllers.Models;
using Jarvis.MonitoringAgentServer.Server.Data;
using Jarvis.MonitoringAgentServer.Server.Dto;
using Jarvis.MonitoringAgentServer.Support;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Jarvis.MonitoringAgentServer.Server.Controllers
{
    public class CustomerController : ApiController
    {
        public IMongoCollection<Customer> _customers { get; set; }

        public CustomerController(
                IMongoCollection<Customer> customers
            )
        {
            _customers = customers;
        }

        [Route("api/customers")]
        [HttpGet]
        public object GetCustomers()
        {
            if (base.RequestContext.Url.Request.RequestUri.Host == "localhost")
            {
                return Json(_customers.Find(Builders<Customer>.Filter.Empty)
                    .ToList()
                    .Select(c => new CustomerDto(c.Name, c.Enabled, c.PublicKey)));
            }

            return Json(_customers.Find(Builders<Customer>.Filter.Empty)
                .ToList()
                .Select(c => new CustomerDto(c.Name, c.Enabled)));
        }

        [Route("api/customers")]
        [HttpPut]
        public object AddCustomer(CreateCustomer createCustomer)
        {
            var existing = _customers.Find(Builders<Customer>.Filter.Eq(c => c.Name, createCustomer.Name))
                .SingleOrDefault();
            if (existing != null)
                return Json(new { Success = false, Error = "User already existing" });

            var customer = new Customer();
            customer.Name = createCustomer.Name;
            var key = EncryptionUtils.GenerateAsimmetricKey();
            customer.PublicKey = key.PublicKey;
            customer.PrivateKey = key.PrivateKey;
            customer.Enabled = true;
            _customers.InsertOne(customer);

            return Json(new { CustomerName = createCustomer.Name,
                PublicKey = customer.PublicKey});
        }
    }
}
