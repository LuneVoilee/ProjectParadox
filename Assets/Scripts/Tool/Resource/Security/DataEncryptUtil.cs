using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tool.Json
{
    public enum EncryptIOType
    {
        StringType,
        ByteType
    }

    public static class DataEncryptUtil
    {
        public static string EncryptKeyStr = "SeedExplore20200224";
        public static string EncryptFlag = "#EncryptedBySeed#";

        private static byte[] s_EncryptFlagBytes;
        private static byte[] s_EncryptKeyBytes;
        private static byte[] s_EncryptedEncryptFlagBytes;

        public static byte[] EncryptFlagBytes =>
            s_EncryptFlagBytes ?? (s_EncryptFlagBytes = Encoding.UTF8.GetBytes(EncryptFlag));

        public static byte[] EncryptKeyBytes =>
            s_EncryptKeyBytes ?? (s_EncryptKeyBytes = Encoding.UTF8.GetBytes(EncryptKeyStr));

        public static byte[] EncryptedEncryptFlagBytes =>
            s_EncryptedEncryptFlagBytes ?? (s_EncryptedEncryptFlagBytes = RC4(EncryptFlagBytes, EncryptKeyBytes));

        public static string RC4Encrypt(string data, string pwd)
        {
            if (string.IsNullOrEmpty(data))
            {
                return data;
            }

            if (IsEncrypt(data, pwd))
            {
                return data;
            }

            string encryptData = data + EncryptFlag;
            return RC4(encryptData, pwd);
        }

        public static byte[] RC4Encrypt(byte[] data, byte[] pwd)
        {
            if (data == null || data.Length == 0 || IsEncrypt(data, pwd))
            {
                return data;
            }

            var concatData = new byte[data.Length + EncryptFlagBytes.Length];
            data.CopyTo(concatData, 0);
            EncryptFlagBytes.CopyTo(concatData, data.Length);
            return RC4(concatData, pwd);
        }

        public static string RC4Decrypt(string data, string pwd)
        {
            if (string.IsNullOrEmpty(data))
            {
                return data;
            }

            if (IsEncrypt(data, pwd))
            {
                return RemoveEncryptFlag(RC4(data, pwd));
            }

            return data;
        }

        public static byte[] RC4Decrypt(byte[] data, byte[] pwd)
        {
            if (data == null || data.Length == 0)
            {
                return data;
            }

            if (!IsEncrypt(data, pwd))
            {
                return data;
            }

            byte[] decrypted = RC4(data, pwd);
            return RemoveEncryptFlag(decrypted);
        }

        private static bool IsEncrypt(string data, string pwd)
        {
            if (string.IsNullOrEmpty(data))
            {
                return false;
            }

            string decrypted = RC4(data, pwd);
            return decrypted.EndsWith(EncryptFlag, StringComparison.Ordinal);
        }

        private static bool IsEncrypt(byte[] data, byte[] pwd)
        {
            if (data == null || data.Length == 0)
            {
                return false;
            }

            byte[] decrypted = RC4(data, pwd);
            return decrypted.Length >= EncryptFlagBytes.Length &&
                   decrypted.Skip(decrypted.Length - EncryptFlagBytes.Length).SequenceEqual(EncryptFlagBytes);
        }

        private static string RemoveEncryptFlag(string text)
        {
            if (string.IsNullOrEmpty(text) || !text.EndsWith(EncryptFlag, StringComparison.Ordinal))
            {
                return text;
            }

            return text.Substring(0, text.Length - EncryptFlag.Length);
        }

        private static byte[] RemoveEncryptFlag(byte[] data)
        {
            if (data == null || data.Length < EncryptFlagBytes.Length)
            {
                return data;
            }

            if (!data.Skip(data.Length - EncryptFlagBytes.Length).SequenceEqual(EncryptFlagBytes))
            {
                return data;
            }

            int len = data.Length - EncryptFlagBytes.Length;
            var output = new byte[len];
            Buffer.BlockCopy(data, 0, output, 0, len);
            return output;
        }

        private static string RC4(string data, string pwd)
        {
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            byte[] pwdBytes = Encoding.UTF8.GetBytes(pwd);
            byte[] output = RC4(dataBytes, pwdBytes);
            return Encoding.UTF8.GetString(output);
        }

        private static byte[] RC4(byte[] data, byte[] pwd)
        {
            if (data == null || pwd == null || pwd.Length == 0)
            {
                return data;
            }

            byte[] s = new byte[256];
            for (int i = 0; i < 256; i++)
            {
                s[i] = (byte)i;
            }

            int j = 0;
            for (int i = 0; i < 256; i++)
            {
                j = (j + s[i] + pwd[i % pwd.Length]) & 255;
                Swap(s, i, j);
            }

            var output = new byte[data.Length];
            int x = 0;
            int y = 0;
            for (int k = 0; k < data.Length; k++)
            {
                x = (x + 1) & 255;
                y = (y + s[x]) & 255;
                Swap(s, x, y);
                output[k] = (byte)(data[k] ^ s[(s[x] + s[y]) & 255]);
            }

            return output;
        }

        private static void Swap(byte[] s, int i, int j)
        {
            byte t = s[i];
            s[i] = s[j];
            s[j] = t;
        }
    }
}
