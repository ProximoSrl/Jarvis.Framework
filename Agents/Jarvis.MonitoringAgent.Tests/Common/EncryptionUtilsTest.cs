using Jarvis.MonitoringAgent.Common.Jarvis.MonitoringAgent.Common;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.MonitoringAgent.Tests.Common
{
    [TestFixture]
    public class EncryptionUtilsTest
    {

        [Test]
        public void Verify_basic_key_serialization()
        {
            var key = EncryptionUtils.GenerateKey();
            var serialized = key.SerializeAsString();
            var restored = EncryptionKey.CreateFromSerializedString(serialized);

            CollectionAssert.AreEqual(key.IV, restored.IV);
            CollectionAssert.AreEqual(key.Key, restored.Key);
        }


        [Test]
        public void verify_asimmetric_encryption()
        {
            var fileName = "TestFiles\\SampleFile.txt";
            var encryptedFileName = "TestFiles\\SampleFile.encrypted";

            if (File.Exists(encryptedFileName))
            {
                File.Delete(encryptedFileName);
            }
  
            var key = EncryptionUtils.EncryptFile(fileName, encryptedFileName);
            var encryptedContent = File.ReadAllBytes(encryptedFileName);
            var decryptedContent = EncryptionUtils.Decrypt(key, encryptedContent);
            var decryptedString = Encoding.UTF8.GetString(decryptedContent);
            Assert.That(decryptedString, Is.EqualTo("This is a sample text file."));
        }
    }
}
