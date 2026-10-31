namespace MicroSaaS.Core.Entities;

/// <summary>
/// Representa a la Pyme suscrita al servicio (Esquema Maestro).
/// </summary>
public class Tenant
{
    public Guid Id { get; private set; }
    public string Rut { get; private set; } = string.Empty;
    public string LegalName { get; private set; } = string.Empty;
    public string BusinessLine { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public string SchemaName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Constructor sin parámetros requerido por EF Core
    private Tenant() { }

    public Tenant(
        string rut,
        string legalName,
        string businessLine,
        string address,
        string schemaName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rut);
        ArgumentException.ThrowIfNullOrWhiteSpace(legalName);
        ArgumentException.ThrowIfNullOrWhiteSpace(businessLine);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);

        Id = Guid.NewGuid();
        Rut = rut.Trim();
        LegalName = legalName.Trim();
        BusinessLine = businessLine.Trim();
        Address = address.Trim();
        SchemaName = schemaName.Trim().ToLowerInvariant();
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public static Tenant Create(
        string rut,
        string legalName,
        string businessLine,
        string address,
        string schemaName) =>
        new(rut, legalName, businessLine, address, schemaName);

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}

