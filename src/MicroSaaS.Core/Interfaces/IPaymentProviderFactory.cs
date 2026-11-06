using MicroSaaS.Core.Enums;

namespace MicroSaaS.Core.Interfaces;

/// <summary>
/// Factory para resolver dinámicamente la implementación de <see cref="ISubscriptionPaymentService"/>
/// según el proveedor de pago seleccionado (<see cref="PaymentProvider"/>).
/// </summary>
public interface IPaymentProviderFactory
{
    ISubscriptionPaymentService GetPaymentService(PaymentProvider provider);
}

