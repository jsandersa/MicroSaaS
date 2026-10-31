namespace MicroSaaS.Core.Interfaces;

/// <summary>
/// Contrato para el servicio de cifrado simétrico AES-256 de la Bóveda de Certificados SII.
/// </summary>
public interface IEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}

