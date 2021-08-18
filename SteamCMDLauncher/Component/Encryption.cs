using System;
using System.Security.Cryptography;
using System.Text;
using System.Management;

namespace SteamCMDLauncher.Component
{
    class Encryption
    {
        // Orginal Owner: https://csharpcode.org/blog/simple-encryption-and-decryption-in-c/

        // [DON'T CHANGE - THIS WILL RESULT IN LOSS OF DATA]
        private static readonly byte[] fixed_pkey = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(getCPUID()));

        // https://jonlabelle.com/snippets/view/csharp/generate-a-unique-hardware-id
        private static string getCPUID()
        {
            string cpuInfo = "";

            ManagementClass managClass = new ManagementClass("win32_processor");
            ManagementObjectCollection managCollec = managClass.GetInstances();

            foreach (ManagementObject managObj in managCollec)
            {
                if (cpuInfo == "")
                {
                    //Get only the first CPU's ID
                    cpuInfo = managObj.Properties["processorID"].Value.ToString();
                    break;
                }
            }

            return cpuInfo;
        }

        static Aes getAes()
        {
            byte[] keyBytes = new byte[16];
            Array.Copy(fixed_pkey, keyBytes, Math.Min(keyBytes.Length, fixed_pkey.Length));

            Aes aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.KeySize = 128;
            aes.Key = keyBytes;
            aes.IV = keyBytes;

            return aes;
        }

        public string AES_Encrypt(string context)
        {
            byte[] b = Encoding.UTF8.GetBytes(context);
            byte[] encrypted = getAes().CreateEncryptor().TransformFinalBlock(b, 0, b.Length);
            return Convert.ToBase64String(encrypted);
        }

        public string AES_Decrypt(string context)
        {
            byte[] b = Convert.FromBase64String(context);
            byte[] decrypted = getAes().CreateDecryptor().TransformFinalBlock(b, 0, b.Length);
            return Encoding.UTF8.GetString(decrypted);
        }
    }
}
