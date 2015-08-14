using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace System
{
    public static class StringExtensions
    {
        /// <summary>
        /// Pythons definition of string.join
        /// </summary>
        /// <param name="self"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static string Join(this string self, ICollection<string> parameters)
        {
            return String.Join(self, parameters);
        }


        public static string Encrypt(this string self, SymmetricAlgorithm cipher)
        {
            byte[] encrypted;
            var valueBytes = Encoding.UTF8.GetBytes(self);

            using (ICryptoTransform encryptor = cipher.CreateEncryptor())
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
            return Convert.ToBase64String(encrypted);
        }

        public static string Encrypt(this string self, byte[] iv, byte[] key)
        {
            using (var cipher = new AesManaged())
            {
                cipher.IV = iv;
                cipher.Key = key;

                return self.Encrypt(cipher);
            }
        }



        public static byte[] Decrypt(this string self, SymmetricAlgorithm cipher)
        {
            var valueBytes = Convert.FromBase64String(self);

            using (ICryptoTransform encryptor = cipher.CreateDecryptor())
            {
                    using (CryptoStream writer = new CryptoStream(new MemoryStream(valueBytes), encryptor, CryptoStreamMode.Read))
                    {
                        var bytes = new List<byte>();
                        int currentByte;
                        while ((currentByte = writer.ReadByte()) > -1) { 
                            bytes.Add((byte)currentByte);
                        }

                        return bytes.ToArray();
                    }
            }
        }

        public static byte[] Decrypt(this string self, byte[] iv, byte[] key)
        {
            using (var cipher = new AesManaged())
            {
                cipher.IV = iv;
                cipher.Key = key;

                return self.Decrypt(cipher);
            }
        }
    }
}