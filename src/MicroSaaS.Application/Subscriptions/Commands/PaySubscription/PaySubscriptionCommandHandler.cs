using MediatR;
using Microsoft.EntityFrameworkCore;
using MicroSaaS.Core.Entities;
using MicroSaaS.Core.Interfaces;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.Application.Subscriptions.Commands.PaySubscription;

/// <summary>
/// Manejador de <see cref="PaySubscriptionCommand"/> que reactiva al Tenant y extiende su suscripción por 30 días.
/// </summary>
public class PaySubscriptionCommandHandler : IRequestHandler<PaySubscriptionCommand, DateTime>
{
    private const decimal DefaultMonthlyFee = 10000m;

    private readonly MasterDbContext _masterDbContext;
    private readonly ITenantProvider _tenantProvider;

    public PaySubscriptionCommandHandler(
        MasterDbContext masterDbContext,
        ITenantProvider tenantProvider)
    {
        _masterDbContext = masterDbContext ?? throw new ArgumentNullException(nameof(masterDbContext));
        _tenantProvider = tenantProvider ?? throw new ArgumentNullException(nameof(tenantProvider));
    }

    public async Task<DateTime> Handle(PaySubscriptionCommand request, CancellationToken cancellationToken)
    {
        Tenant? tenant = null;

        if (request.TenantId.HasValue && request.TenantId.Value != Guid.Empty)
        {
            tenant = await _masterDbContext.Tenants
                .FirstOrDefaultAsync(t => t.Id == request.TenantId.Value, cancellationToken);
        }

        // Fallback por SchemaName si el TenantId del token cambió tras recrear el volumen de BD
        if (tenant is null)
        {
            var currentSchema = _tenantProvider.GetCurrentSchema();
            if (!string.IsNullOrWhiteSpace(currentSchema) &&
                !string.Equals(currentSchema, "public", StringComparison.OrdinalIgnoreCase))
            {
                tenant = await _masterDbContext.Tenants
                    .FirstOrDefaultAsync(t => t.SchemaName == currentSchema, cancellationToken);
            }
        }

        if (tenant is null)
        {
            throw new InvalidOperationException("No se encontró el Tenant asociado para procesar el pago de la suscripción.");
        }

        var subscription = await _masterDbContext.Subscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenant.Id, cancellationToken);

        var now = DateTime.UtcNow;

        if (subscription is null)
        {
            var initialDueDate = now.AddDays(30);
            subscription = Subscription.Create(
                tenantId: tenant.Id,
                monthlyFee: DefaultMonthlyFee,
                startDate: now,
                nextDueDate: initialDueDate);

            tenant.Activate();
            await _masterDbContext.Subscriptions.AddAsync(subscription, cancellationToken);
            await _masterDbContext.SaveChangesAsync(cancellationToken);

            return initialDueDate;
        }

        tenant.Activate();

        var baseDate = subscription.NextDueDate > now
            ? subscription.NextDueDate
            : now;

        var newDueDate = baseDate.AddDays(30);
        subscription.Activate(newDueDate);

        await _masterDbContext.SaveChangesAsync(cancellationToken);

        return newDueDate;
    }
}
