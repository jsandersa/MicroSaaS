using MediatR;
using MicroSaaS.Core.Interfaces;

namespace MicroSaaS.Application.Subscriptions.Commands.CreateTenantSubscription;

/// <summary>
/// Manejador de <see cref="CreateTenantSubscriptionCommand"/> que utiliza <see cref="IPaymentProviderFactory"/>
/// para resolver la pasarela de pago e iniciar el flujo de suscripción recurrente.
/// </summary>
public sealed class CreateTenantSubscriptionCommandHandler : IRequestHandler<CreateTenantSubscriptionCommand, string>
{
    private readonly IPaymentProviderFactory _paymentProviderFactory;

    public CreateTenantSubscriptionCommandHandler(IPaymentProviderFactory paymentProviderFactory)
    {
        _paymentProviderFactory = paymentProviderFactory ?? throw new ArgumentNullException(nameof(paymentProviderFactory));
    }

    public async Task<string> Handle(
        CreateTenantSubscriptionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var paymentService = _paymentProviderFactory.GetPaymentService(request.ProviderEnum);

        return await paymentService.CreateSubscriptionAsync(
            tenantId: request.TenantId,
            planName: request.PlanName,
            amount: request.Amount,
            payerEmail: request.PayerEmail,
            cancellationToken: cancellationToken);
    }
}

