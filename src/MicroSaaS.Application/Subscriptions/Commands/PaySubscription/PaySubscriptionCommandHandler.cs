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
        else
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

        if (subscription is null)
        {
            throw new InvalidOperationException("No se encontró una suscripción asociada al Tenant.");
        }

        // Reactivar el Tenant y extender la fecha de expiración por 30 días desde ahora (o desde su vencimiento si aún es futuro)
        tenant.Activate();

        var baseDate = subscription.NextDueDate > DateTime.UtcNow
            ? subscription.NextDueDate
            : DateTime.UtcNow;

        var newDueDate = baseDate.AddDays(30);
        subscription.Activate(newDueDate);

        await _masterDbContext.SaveChangesAsync(cancellationToken);

        return newDueDate;
    }
}

