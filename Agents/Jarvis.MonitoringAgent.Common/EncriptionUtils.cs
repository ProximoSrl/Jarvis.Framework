using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.MonitoringAgent.Common
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;

    namespace Jarvis.MonitoringAgent.Common
    {
        public class EncryptionKey
        {
            public Byte[] Key { get; set; }

            public Byte[] IV { get; set; }

            public String SerializeAsString()
            {
                StringBuilder sb = new StringBuilder(Key.Length * 2 + IV.Length * 2 +1);
                sb.Append(BitConverter.ToString(Key).Replace("-", ""));
                sb.Append("|");
                sb.Append(BitConverter.ToString(IV).Replace("-", ""));
                return sb.ToString();
            }

            public static EncryptionKey CreateFromSerializedString(String serializedString)
            {
                EncryptionKey retValue = new EncryptionKey();
                var splitted = serializedString.Split('|');
                retValue.Key = HexEncoding.GetBytes(splitted[0]);
                retValue.IV = HexEncoding.GetBytes(splitted[1]);
                return retValue;
            }
        }

        public class AsymmetricEncryptionKey
        {
            public String PublicKey { get; private set; }
            public String PrivateKey { get; private set; }

            public AsymmetricEncryptionKey(RSACryptoServiceProvider rsa)
            {
                PublicKey = BitConverter.ToString(rsa.ExportCspBlob(false)).Replace("-", "");
                PrivateKey = BitConverter.ToString(rsa.ExportCspBlob(true)).Replace("-", "");
            }

            private AsymmetricEncryptionKey() { }

            public AsymmetricEncryptionKey GetPublicKey()
            {
                return new AsymmetricEncryptionKey() { PublicKey = this.PublicKey };
            }

            public RSACryptoServiceProvider GetProvider()
            {
                RSACryptoServiceProvider retValue = new RSACryptoServiceProvider();
                Byte[] cspBlob;
                if (!String.IsNullOrEmpty(PrivateKey))
                {
                    cspBlob = HexEncoding.GetBytes(PrivateKey);
                }
                else
                {
                    cspBlob = HexEncoding.GetBytes(PublicKey);
                }
                retValue.ImportCspBlob(cspBlob);
                return retValue;
            }
        }

        public static class EncryptionUtils
        {

            public static AsymmetricEncryptionKey GenerateAsimmetricKey()
            {
                //Generate a public/private key pair.
                using (RSACryptoServiceProvider RSA = new RSACryptoServiceProvider())
                {
                    //Save the public key information to an RSAParameters structure.
                    AsymmetricEncryptionKey key = new AsymmetricEncryptionKey(RSA);

                    return key;
                }
            }

            public static EncryptionKey GenerateKey()
            {
                using (var rm = new RijndaelManaged())
                {
                    rm.GenerateKey();
                    rm.GenerateIV();
                    EncryptionKey key = new EncryptionKey();
                    key.Key = rm.Key;
                    key.IV = rm.IV;
                    return key;
                }
            }

            public static EncryptionKey Encrypt(AsymmetricEncryptionKey asymmetricKey, EncryptionKey symmetricKey)
            {
                var IV = Encrypt(asymmetricKey, symmetricKey.IV);
                var Key = Encrypt(asymmetricKey, symmetricKey.Key);
                return new EncryptionKey() { IV = IV, Key = Key };
            }

            public static EncryptionKey Decrypt(AsymmetricEncryptionKey asymmetricKey, EncryptionKey encryptedSimmetricKey)
            {
                var IV = Decrypt(asymmetricKey, encryptedSimmetricKey.IV);
                var Key = Decrypt(asymmetricKey, encryptedSimmetricKey.Key);
                return new EncryptionKey() { IV = IV, Key = Key };
            }

            public static String Encrypt(AsymmetricEncryptionKey key, String data)
            {
                var encrypted = Encrypt(key, Encoding.UTF8.GetBytes(data));
                return BitConverter.ToString(encrypted).Replace("-", "");
            }

            public static Byte[] Encrypt(AsymmetricEncryptionKey key, Byte[] data)
            {
                using (var rsa = key.GetProvider())
                {
                    return rsa.Encrypt(data, true);
                }
            }

            public static String Decrypt(AsymmetricEncryptionKey key, String data)
            {
                var decrypted = Decrypt(key, HexEncoding.GetBytes(data));
                return Encoding.UTF8.GetString(decrypted);
            }

            public static Byte[] Decrypt(AsymmetricEncryptionKey key, Byte[] data)
            {
                using (var rsa = key.GetProvider())
                {
                    return rsa.Decrypt(data, true);
                }
            }

            public static String Decrypt(String key, String data)
            {
                return Decrypt(EncryptionKey.CreateFromSerializedString(key), data);
            }

            public static String Decrypt(EncryptionKey key, String data)
            {
                return Decrypt(key.Key, key.IV, data);
            }

            public static String Decrypt(Byte[] key, Byte[] iv, String data)
            {
                using (MemoryStream ms = new MemoryStream())
                using (RijndaelManaged crypto = new RijndaelManaged())
                {

                    Byte[] rawData = HexEncoding.GetBytes(data);
                    ICryptoTransform ct = crypto.CreateDecryptor(key, iv);
                    using (CryptoStream cs = new CryptoStream(ms, ct, CryptoStreamMode.Write))
                    {
                        cs.Write(rawData, 0, rawData.Length);
                        cs.Close();
                    }

                    return Encoding.UTF8.GetString(ms.ToArray());
                }

            }

            public static Byte[] Decrypt(String key, Byte[] data)
            {
                return Decrypt(EncryptionKey.CreateFromSerializedString(key), data);
            }

            public static Byte[] Decrypt(EncryptionKey key, Byte[] data)
            {
                return Decrypt(key.Key, key.IV, data);
            }

            public static Byte[] Decrypt(Byte[] key, Byte[] iv, Byte[] data)
            {
                using (MemoryStream ms = new MemoryStream())
                using (RijndaelManaged crypto = new RijndaelManaged())
                {
                    ICryptoTransform ct = crypto.CreateDecryptor(key, iv);
                    using (CryptoStream cs = new CryptoStream(ms, ct, CryptoStreamMode.Write))
                    {
                        cs.Write(data, 0, data.Length);
                        cs.Close();
                    }

                    return ms.ToArray();
                }

            }

            public static String Encrypt(Byte[] key, Byte[] iv, String data)
            {
                using (MemoryStream ms = new MemoryStream())
                using (RijndaelManaged crypto = new RijndaelManaged())
                {
                    ICryptoTransform ct = crypto.CreateEncryptor(key, iv);
                    Byte[] rawData = Encoding.UTF8.GetBytes(data);
                    using (CryptoStream cs = new CryptoStream(ms, ct, CryptoStreamMode.Write))
                    {
                        cs.Write(rawData, 0, rawData.Length);
                        cs.Close();
                    };
                    return BitConverter.ToString(ms.ToArray()).Replace("-", "");
                }

            }

            public static Byte[] Encrypt(Byte[] key, Byte[] iv, byte[] data)
            {
                using (MemoryStream ms = new MemoryStream())
                using (RijndaelManaged crypto = new RijndaelManaged())
                {
                    ICryptoTransform ct = crypto.CreateEncryptor(key, iv);
                    using (CryptoStream cs = new CryptoStream(ms, ct, CryptoStreamMode.Write))
                    {
                        cs.Write(data, 0, data.Length);
                        cs.Close();
                    };
                    return ms.ToArray();
                }

            }

            /// <summary>
            /// encrypt a file with a new fresh generated simmetric key.
            /// </summary>
            /// <param name="fileName"></param>
            /// <param name="destinationFileName"></param>
            /// <returns></returns>
            public static String EncryptFile(String fileName, String destinationFileName)
            {
                var key = EncryptionUtils.GenerateKey();
                using (FileStream sourceFs = new FileStream(fileName, FileMode.Open))
                using (FileStream destinationFs = new FileStream(destinationFileName, FileMode.Create))
                using (RijndaelManaged crypto = new RijndaelManaged())
                {
                    ICryptoTransform ct = crypto.CreateEncryptor(key.Key, key.IV);
                    using (CryptoStream cs = new CryptoStream(destinationFs, ct, CryptoStreamMode.Write))
                    {
                        sourceFs.CopyTo(cs);
                    }
                }
                return key.SerializeAsString();
            }

            /// <summary>
            /// Encrypt a file generating new simmetric key, the simmetric key is then 
            /// encrypted with an asimmetric key
            /// </summary>
            /// <param name="fileName"></param>
            /// <param name="destinationFileName"></param>
            /// <param name="key"></param>
            /// <returns></returns>
            public static String EncryptFile(String fileName, String destinationFileName, AsymmetricEncryptionKey key)
            {
                var newSimmetricKey = GenerateKey();
                using (FileStream sourceFs = new FileStream(fileName, FileMode.Open))
                using (FileStream destinationFs = new FileStream(destinationFileName, FileMode.Create))
                using (RijndaelManaged crypto = new RijndaelManaged())
                {
                    ICryptoTransform ct = crypto.CreateEncryptor(newSimmetricKey.Key, newSimmetricKey.IV);
                    using (CryptoStream cs = new CryptoStream(destinationFs, ct, CryptoStreamMode.Write))
                    {
                        sourceFs.CopyTo(cs);
                    }
                }

                var encryptedKey = Encrypt(key, newSimmetricKey);
                return encryptedKey.SerializeAsString();
            }


        }

        public class HexEncoding
        {
            /// <summary>
            /// Creates a byte array from the hexadecimal string. Each two characters are combined
            /// to create one byte. First two hexadecimal characters become first byte in returned array.
            /// Non-hexadecimal characters are ignored. 
            /// </summary>
            /// <param name="hexString">string to convert to byte array</param>
            /// <returns>byte array, in the same left-to-right order as the hexString</returns>
            public static byte[] GetBytes(string hexString)
            {
                int byteLength = hexString.Length / 2;
                byte[] bytes = new byte[byteLength];
                string hex;
                int j = 0;
                for (int i = 0; i < bytes.Length; i++)
                {
                    hex = new String(new Char[] { hexString[j], hexString[j + 1] });
                    bytes[i] = HexToByte(hex);
                    j = j + 2;
                }
                return bytes;
            }

            /// <summary>
            /// Converts 1 or 2 character string into equivalant byte value
            /// </summary>
            /// <param name="hex">1 or 2 character string</param>
            /// <returns>byte</returns>
            private static byte HexToByte(string hex)
            {
                if (hex.Length > 2 || hex.Length <= 0)
                    throw new ArgumentException("hex must be 1 or 2 characters in length");
                byte newByte = byte.Parse(hex, System.Globalization.NumberStyles.HexNumber);
                return newByte;
            }


        }
    }

}
