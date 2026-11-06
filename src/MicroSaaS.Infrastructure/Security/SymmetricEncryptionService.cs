using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using MicroSaaS.Core.Interfaces;

namespace MicroSaaS.Infrastructure.Security;

/// <summary>
/// Servicio de cifrado simétrico AES-256 para proteger las contraseñas de certificados digitales (.pfx) en la bóveda SII.
/// </summary>
public class SymmetricEncryptionService : IEncryptionService
{
    private readonly byte[] _key;

    public SymmetricEncryptionService(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var rawMasterKey = new[]
            {
                configuration["MasterKey_SII_Vault"],
                configuration["EncryptionSettings:MasterKey"],
                "MasterKey_SII_Vault_Default_AES256_Secret_Key_2026!"
            }
            .First(v => !string.IsNullOrWhiteSpace(v))!
            .Trim();

        // Derivar una llave exacta de 256 bits (32 bytes) mediante SHA-256 para AES-256
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(rawMasterKey));
    }

    public string Encrypt(string plainText)
    {
        ArgumentException.ThrowIfNullOrEmpty(plainText);

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Key = _key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string cipherText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cipherText);

        var fullCipher = Convert.FromBase64String(cipherText);

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Key = _key;

        var ivLength = aes.BlockSize / 8;
        if (fullCipher.Length <= ivLength)
        {
            throw new CryptographicException("El texto cifrado proporcionado no es válido.");
        }

        var iv = new byte[ivLength];
        var cipherBytes = new byte[fullCipher.Length - ivLength];

        Buffer.BlockCopy(fullCipher, 0, iv, 0, ivLength);
        Buffer.BlockCopy(fullCipher, ivLength, cipherBytes, 0, cipherBytes.Length);

        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        var decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

        return Encoding.UTF8.GetString(decryptedBytes);
    }
}
