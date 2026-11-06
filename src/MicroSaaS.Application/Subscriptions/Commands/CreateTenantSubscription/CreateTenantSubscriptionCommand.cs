using MediatR;
using MicroSaaS.Core.Enums;

namespace MicroSaaS.Application.Subscriptions.Commands.CreateTenantSubscription;

/// <summary>
/// Comando CQRS para crear una suscripción recurrente de un Tenant en la pasarela de pago seleccionada
/// y retornar la URL de pago (<c>init_point</c>).
/// </summary>
public sealed record CreateTenantSubscriptionCommand(
    Guid TenantId,
    string PlanName,
    decimal Amount,
    string PayerEmail,
    PaymentProvider ProviderEnum) : IRequest<string>;

