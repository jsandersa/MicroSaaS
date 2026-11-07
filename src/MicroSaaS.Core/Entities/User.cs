namespace MicroSaaS.Core.Entities;

/// <summary>
/// Representa a un usuario perteneciente a un Tenant dentro del Esquema Maestro.
/// Soporta roles diferenciados como "Owner" (Administrador Pyme) y "Accountant" (Contador Externo / Auditor).
/// </summary>
public class User
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string Role { get; private set; } = "Owner";
    public string? CashRegisterId { get; private set; }
    public string? PaymentDeviceId { get; private set; }

    // Constructor sin parámetros requerido por EF Core
    private User() { }

    public User(
        Guid tenantId,
        string email,
        string passwordHash,
        string role = "Owner",
        string? cashRegisterId = null,
        string? paymentDeviceId = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("El identificador del Tenant no puede ser vacío.", nameof(tenantId));

        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        Id = Guid.NewGuid();
        TenantId = tenantId;
        Email = email.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
        Role = string.IsNullOrWhiteSpace(role) ? "Owner" : role.Trim();
        CashRegisterId = string.IsNullOrWhiteSpace(cashRegisterId)
            ? (Role == "Accountant" ? null : "CAJA-01")
            : cashRegisterId.Trim().ToUpperInvariant();
        PaymentDeviceId = string.IsNullOrWhiteSpace(paymentDeviceId)
            ? (Role == "Accountant" ? null : "MP-POINT-01")
            : paymentDeviceId.Trim().ToUpperInvariant();
    }

    public static User Create(
        Guid tenantId,
        string email,
        string passwordHash,
        string role = "Owner",
        string? cashRegisterId = null,
        string? paymentDeviceId = null) =>
        new(tenantId, email, passwordHash, role, cashRegisterId, paymentDeviceId);
}
