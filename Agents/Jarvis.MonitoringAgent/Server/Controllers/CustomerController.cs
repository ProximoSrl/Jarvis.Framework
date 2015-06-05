using Jarvis.MonitoringAgent.Server.Controllers.Models;
using Jarvis.MonitoringAgent.Server.Data;
using Jarvis.MonitoringAgent.Server.Dto;
using Jarvis.MonitoringAgent.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace Jarvis.MonitoringAgent.Server.Controllers
{
    public class CustomerController : ApiController
    {
        public MongoCollection<Customer> _customers { get; set; }

        public CustomerController(
                MongoCollection<Customer> customers
            )
        {
            _customers = customers;
        }

        [Route("server/api/customers")]
        [HttpGet]
        public object GetCustomers()
        {
            return Json(_customers.FindAll()
                .Select(c => new CustomerDto(c.Name, c.Enabled)));
        }

        [Route("server/api/customers")]
        [HttpPut]
        public object AddCustomer(CreateCustomer createCustomer)
        {
            var existing = _customers.FindOneById(BsonValue.Create(createCustomer.Name));
            if (existing != null)
                return Json(new { Success = false, Error = "User already existing" });

            var customer = new Customer();
            customer.Name = createCustomer.Name;
            var key = EncryptionUtils.GenerateAsimmetricKey();
            customer.PublicKey = key.PublicKey;
            customer.PrivateKey = key.PrivateKey;
            customer.Enabled = true;
            _customers.Save(customer);

            return Json(new { CustomerName = createCustomer.Name,
                PublicKey = customer.PublicKey});
        }
    }
}
