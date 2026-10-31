namespace MicroSaaS.Core.Entities;

/// <summary>
/// Representa a un usuario perteneciente a un Tenant dentro del Esquema Maestro.
/// </summary>
public class User
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;

    // Constructor sin parámetros requerido por EF Core
    private User() { }

    public User(Guid tenantId, string email, string passwordHash)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("El identificador del Tenant no puede ser vacío.", nameof(tenantId));

        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        Id = Guid.NewGuid();
        TenantId = tenantId;
        Email = email.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
    }

    public static User Create(Guid tenantId, string email, string passwordHash) =>
        new(tenantId, email, passwordHash);
}

