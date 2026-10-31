namespace MicroSaaS.Core.Entities.TenantDomain;

/// <summary>
/// Representa el certificado digital (.pfx) de facturación electrónica SII almacenado en el esquema privado del Tenant.
/// </summary>
public class TenantDigitalCertificate
{
    public Guid Id { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public byte[] FileData { get; private set; } = [];
    public string EncryptedPassword { get; private set; } = string.Empty;
    public DateTime ExpirationDate { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    // Constructor sin parámetros requerido por EF Core
    private TenantDigitalCertificate() { }

    public TenantDigitalCertificate(
        string fileName,
        byte[] fileData,
        string encryptedPassword,
        DateTime expirationDate,
        string subject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(fileData);

        if (fileData.Length == 0)
        {
            throw new ArgumentException("El contenido binario del certificado (.pfx) no puede estar vacío.", nameof(fileData));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedPassword);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        Id = Guid.NewGuid();
        FileName = fileName.Trim();
        FileData = fileData;
        EncryptedPassword = encryptedPassword;
        ExpirationDate = expirationDate.Kind == DateTimeKind.Utc
            ? expirationDate
            : expirationDate.ToUniversalTime();
        Subject = subject.Trim();
        IsActive = true;
    }

    public static TenantDigitalCertificate Create(
        string fileName,
        byte[] fileData,
        string encryptedPassword,
        DateTime expirationDate,
        string subject) =>
        new(fileName, fileData, encryptedPassword, expirationDate, subject);

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}

