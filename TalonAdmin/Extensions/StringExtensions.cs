using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace System
{
    public  static class StringExtensions
    {
        /// <summary>
        /// Pythons definition of string.join
        /// </summary>
        /// <param name="self"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static string Join(this string self,ICollection<string> parameters)
        {
            return String.Join(self, parameters);
        }


        public static string Encrypt(this string self, byte[] iv, byte[] key)
        {
            byte[] encrypted;
            using (var cipher = new AesManaged())
            {
                cipher.Mode = CipherMode.CBC;
                var valueBytes = Encoding.UTF8.GetBytes(self);

                using (ICryptoTransform encryptor = cipher.CreateEncryptor(key, iv))
                {
                    using (MemoryStream to = new MemoryStream())
                    {
                        using (CryptoStream writer = new CryptoStream(to, encryptor, CryptoStreamMode.Write))
                        {
                            writer.Write(valueBytes, 0, valueBytes.Length);
                            writer.FlushFinalBlock();
                            encrypted = to.ToArray();
                        }
                    }
                }
                cipher.Clear();
            }
            return Convert.ToBase64String(encrypted);
        }
    }
}