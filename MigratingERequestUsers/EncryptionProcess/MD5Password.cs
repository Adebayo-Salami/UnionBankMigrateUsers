using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MigratingERequestUsers.EncryptionProcess
{
    public class MD5Password
    {
        private string ByteArrayToString(byte[] array)
        {
            StringBuilder builder = new StringBuilder(array.Length);
            for (int i = 0; i < array.Length; i++)
            {
                builder.Append(array[i].ToString("X2"));
            }
            return builder.ToString();
        }

        public string CreateSecurePassword(string clear)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(clear);
            byte[] array = new MD5CryptoServiceProvider().ComputeHash(bytes);
            return this.ByteArrayToString(array);
        }

        public string GetPasswordInClear(string secure)
        {
            throw new Exception("One way encryption service");
        }
    }
}
