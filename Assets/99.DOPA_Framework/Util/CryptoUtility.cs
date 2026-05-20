using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public static class CryptoUtility
{
    private static readonly byte[] Key = Generate256BitKey();
    // IV 초기화 개선
    private static readonly byte[] IV = new byte[16]; // 문제 코드


    public static byte[] Encrypt(byte[] data, byte[] key)
    {
        using Aes aes = Aes.Create();
        aes.Key = key;
        aes.IV = new byte[16]; // AES-CBC 모드 사용 시 필요

        using MemoryStream ms = new();
        using CryptoStream cs = new(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
        cs.Write(data, 0, data.Length);
        cs.FlushFinalBlock();
        return ms.ToArray();
    }


    public static byte[] EncryptString(string plainText, byte[] key)
    {
        byte[] data = Encoding.UTF8.GetBytes(plainText);
        return Encrypt(data,key);
    }


    public static byte[] Decrypt(byte[] encryptedData, byte[] key)
    {
        using Aes aes = Aes.Create();
        
        // 키 길이 검증 추가
        if (key.Length != 32)
            throw new ArgumentException("Invalid key size. Required: 256-bit (32 bytes)");

        aes.Key = key;
        aes.IV = new byte[16]; // AES 블록 크기는 128비트

        using MemoryStream ms = new(encryptedData);
        using CryptoStream cs = new(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using MemoryStream output = new();
        cs.CopyTo(output);
        return output.ToArray();
    }


    private static byte[] Generate256BitKey()
    {
        using RNGCryptoServiceProvider rng = new();
        byte[] key = new byte[32]; // 256 bits
        rng.GetBytes(key);
        return key;
    }

    private static byte[] GenerateRandomIV()
    {
        using var rng = new RNGCryptoServiceProvider();
        byte[] iv = new byte[16];
        rng.GetBytes(iv);
        return iv;
    }

    public static byte[] GenerateAes256Key()
    {
        byte[] key = new byte[32]; // 256-bit
        using (var rng = new RNGCryptoServiceProvider())
        {
            rng.GetBytes(key);
        }
        return key;
    }

    public static string GenerateBase64Key()
    {
        return Convert.ToBase64String(GenerateAes256Key());
    }

}
