using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Jarvis.MonitoringAgent.Common.Jarvis.MonitoringAgent.Common;

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
        public void verify_simmetric_file_encryption()
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

        [Test]
        public void verify_asimmetric_file_encryption()
        {
            var fileName = "TestFiles\\SampleFile.txt";
            var encryptedFileName = "TestFiles\\SampleFile.encrypted";

            if (File.Exists(encryptedFileName))
            {
                File.Delete(encryptedFileName);
            }

            var key = EncryptionUtils.GenerateAsimmetricKey();
            var publicKey = key.GetPublicKey();

            var encryptionKeyAsString = EncryptionUtils.EncryptFile(fileName, encryptedFileName, publicKey);
            var encryptedContent = File.ReadAllBytes(encryptedFileName);

            //now we need to decrypt the key used to encrypt the file
            var encryptedKey = EncryptionKey.CreateFromSerializedString(encryptionKeyAsString);
            var decryptedKey = EncryptionUtils.Decrypt(key, encryptedKey);

            var decryptedContent = EncryptionUtils.Decrypt(decryptedKey, encryptedContent);
            var decryptedString = Encoding.UTF8.GetString(decryptedContent);
            Assert.That(decryptedString, Is.EqualTo("This is a sample text file."));
        }

        [Test]
        public void verify_asimmetric_file_encryption_file_decrypt()
        {
            var fileName = "TestFiles\\SampleFile.txt";
            var encryptedFileName = "TestFiles\\SampleFile.encrypted";
            var decryptedFileName = "TestFiles\\SampleFile.decrypted";

            if (File.Exists(encryptedFileName))
            {
                File.Delete(encryptedFileName);
            }

            var key = EncryptionUtils.GenerateAsimmetricKey();
            var publicKey = key.GetPublicKey();

            var encryptionKeyAsString = EncryptionUtils.EncryptFile(fileName, encryptedFileName, publicKey);
           
            //now we need to decrypt the key used to encrypt the file
            var encryptedKey = EncryptionKey.CreateFromSerializedString(encryptionKeyAsString);
            var decryptedKey = EncryptionUtils.Decrypt(key, encryptedKey);

            EncryptionUtils.DecryptFile(decryptedKey, encryptedFileName, decryptedFileName);
            var decryptedString = File.ReadAllText(decryptedFileName);
            Assert.That(decryptedString, Is.EqualTo("This is a sample text file."));
        }


        [Test]
        public void Verify_asymmetric_encription()
        {
            var key = EncryptionUtils.GenerateAsimmetricKey();

            var publicKey = key.GetPublicKey();
            var encrypted = EncryptionUtils.Encrypt(publicKey, "This is a password");
              
            var decrypted = EncryptionUtils.Decrypt(key, encrypted);
            Assert.That(decrypted, Is.EqualTo("This is a password"));
        }

        [Test]
        [ExpectedException(typeof(CryptographicException))]
        public void Verify_cannot_decrypt_with_public_key()
        {
            var key = EncryptionUtils.GenerateAsimmetricKey();

            var publicKey = key.GetPublicKey();
            var encrypted = EncryptionUtils.Encrypt(publicKey, "This is a password");

            var decrypted = EncryptionUtils.Decrypt(publicKey, encrypted);
  
        }
    }
}
