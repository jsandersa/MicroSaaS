namespace MicroSaaS.Core.Entities;

/// <summary>
/// Representa el contrato de suscripción del servicio para un Tenant.
/// </summary>
public class Subscription
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public decimal MonthlyFee { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime NextDueDate { get; private set; }
    public SubscriptionStatus Status { get; private set; }

    // Constructor sin parámetros requerido por EF Core
    private Subscription() { }

    public Subscription(
        Guid tenantId,
        decimal monthlyFee,
        DateTime startDate,
        DateTime nextDueDate)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("El identificador del Tenant no puede ser vacío.", nameof(tenantId));

        ArgumentOutOfRangeException.ThrowIfNegative(monthlyFee);

        if (nextDueDate <= startDate)
            throw new ArgumentException("La próxima fecha de vencimiento debe ser posterior a la fecha de inicio.", nameof(nextDueDate));

        Id = Guid.NewGuid();
        TenantId = tenantId;
        MonthlyFee = monthlyFee;
        StartDate = startDate;
        NextDueDate = nextDueDate;
        Status = SubscriptionStatus.Active;
    }

    public static Subscription Create(
        Guid tenantId,
        decimal monthlyFee,
        DateTime startDate,
        DateTime nextDueDate) =>
        new(tenantId, monthlyFee, startDate, nextDueDate);

    public void MarkAsPastDue() => Status = SubscriptionStatus.PastDue;

    public void Suspend() => Status = SubscriptionStatus.Suspended;

    public void Activate(DateTime nextDueDate)
    {
        NextDueDate = nextDueDate;
        Status = SubscriptionStatus.Active;
    }
}

