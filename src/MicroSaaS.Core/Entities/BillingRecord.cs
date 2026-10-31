namespace MicroSaaS.Core.Entities;

/// <summary>
/// Representa el registro mensual de servicio dispensado para facturar al Tenant.
/// </summary>
public class BillingRecord
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public DateTime BillingPeriod { get; private set; }
    public decimal Amount { get; private set; }
    public bool IsInvoiced { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Constructor sin parámetros requerido por EF Core
    private BillingRecord() { }

    public BillingRecord(
        Guid tenantId,
        DateTime billingPeriod,
        decimal amount)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("El identificador del Tenant no puede ser vacío.", nameof(tenantId));

        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        Id = Guid.NewGuid();
        TenantId = tenantId;
        BillingPeriod = billingPeriod;
        Amount = amount;
        IsInvoiced = false;
        CreatedAt = DateTime.UtcNow;
    }

    public static BillingRecord Create(
        Guid tenantId,
        DateTime billingPeriod,
        decimal amount) =>
        new(tenantId, billingPeriod, amount);

    public void MarkAsInvoiced() => IsInvoiced = true;
}

