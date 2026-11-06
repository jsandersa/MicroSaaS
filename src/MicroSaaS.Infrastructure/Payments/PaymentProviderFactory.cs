using Microsoft.Extensions.DependencyInjection;
using MicroSaaS.Core.Enums;
using MicroSaaS.Core.Interfaces;
using MicroSaaS.Infrastructure.Payments.MercadoPago;

namespace MicroSaaS.Infrastructure.Payments;

/// <summary>
/// Implementación del patrón Factory que resuelve dinámicamente el proveedor de pagos de suscripción
/// a partir de <see cref="IServiceProvider"/>.
/// </summary>
public sealed class PaymentProviderFactory : IPaymentProviderFactory
{
    private readonly IServiceProvider _serviceProvider;

    public PaymentProviderFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public ISubscriptionPaymentService GetPaymentService(PaymentProvider provider)
    {
        return provider switch
        {
            PaymentProvider.MercadoPago => _serviceProvider.GetRequiredService<MercadoPagoSubscriptionService>(),
            PaymentProvider.GetNet => throw new NotSupportedException(
                "El proveedor de pagos GetNet aún no está habilitado en este entorno."),
            PaymentProvider.Transbank => throw new NotSupportedException(
                "El proveedor de pagos Transbank aún no está habilitado en este entorno."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(provider),
                provider,
                $"Proveedor de pago no reconocido: '{provider}'.")
        };
    }
}

