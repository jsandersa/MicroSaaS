using MediatR;

namespace MicroSaaS.Application.Subscriptions.Commands.PaySubscription;

/// <summary>
/// Comando para simular el pago de la suscripción del Tenant actual,
/// reactivando su cuenta y extendiendo la fecha de vencimiento por 30 días.
/// </summary>
public record PaySubscriptionCommand(Guid? TenantId = null) : IRequest<DateTime>;

