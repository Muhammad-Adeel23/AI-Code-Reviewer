using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Components.Common
{
    public class Encrypt_BT
    {
        //Key must be of 32 Characters -- 4bytes --
        private readonly string key = "b14ca5898a4e4133bbce2ea2315a11%1";

        //Signature that append in start of the string for validation
        private readonly string sign = "$%$";
        public string EncryptString(string data)
        {
            byte[] iv = new byte[16];
            byte[] array;

            using (Aes aes = Aes.Create())
            {
                //Symmetric key encoded into UTF8 
                aes.Key = Encoding.UTF8.GetBytes(key);
                //Initializing Vector Empty bytes -- OPTIONAL
                aes.IV = iv;

                //Getting encrpytion object with the custom symmetric key and initializing vector
                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                //Encrypting the data
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (CryptoStream cryptoStream = new CryptoStream((Stream)memoryStream, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter streamWriter = new StreamWriter((Stream)cryptoStream))
                        {
                            streamWriter.Write(data);
                        }

                        array = memoryStream.ToArray();
                    }
                }
            }

            var str = Convert.ToBase64String(array);

            //($@#) Signature appended to validate either it is encrypted by our algorithm or not
            str = sign + str;
            return str;
        }
        public bool isEncrypted(string data)
        {
            if (data.Length > 3)
            {
                if (!(data.Substring(0, 3) == sign))
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        public string DecryptString(string encrypted_data)
        {
            if (isEncrypted(encrypted_data))
            {
                encrypted_data = encrypted_data.Remove(0, 3);
                byte[] iV = new byte[16];
                byte[] buffer = Convert.FromBase64String(encrypted_data);
                using Aes aes = Aes.Create();
                aes.Key = Encoding.UTF8.GetBytes(key);
                aes.IV = iV;
                ICryptoTransform transform = aes.CreateDecryptor(aes.Key, aes.IV);
                using MemoryStream stream = new MemoryStream(buffer);
                using CryptoStream stream2 = new CryptoStream(stream, transform, CryptoStreamMode.Read);
                using StreamReader streamReader = new StreamReader(stream2);
                return streamReader.ReadToEnd();
            }
            return "";
        }
    }
}
