using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Jarvis.MonitoringAgentServer.Support
{
    public static class EncryptionUtils
    {

        public class EncryptionKey
        {
            public Byte[] Key { get; set; }

            public Byte[] IV { get; set; }

        }

        public class AsimmetricEncryptionKey
        {
            public String PublicKey { get; private set; }
            public String PrivateKey { get; private set; }

            public AsimmetricEncryptionKey(RSACryptoServiceProvider rsa)
            {
                PublicKey = BitConverter.ToString(rsa.ExportCspBlob(false)).Replace("-", "");
                PrivateKey = BitConverter.ToString(rsa.ExportCspBlob(true)).Replace("-", "");
            }

           
        }

        public static AsimmetricEncryptionKey GenerateAsimmetricKey()
        {
            //Generate a public/private key pair.
            RSACryptoServiceProvider RSA = new RSACryptoServiceProvider();
            //Save the public key information to an RSAParameters structure.
            AsimmetricEncryptionKey key = new AsimmetricEncryptionKey(RSA);

            return key;
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
