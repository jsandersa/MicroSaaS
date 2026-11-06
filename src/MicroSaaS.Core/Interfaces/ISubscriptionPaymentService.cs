namespace MicroSaaS.Core.Interfaces;

/// <summary>
/// Contrato de dominio/aplicación para gestionar suscripciones recurrentes en pasarelas de pago externas.
/// </summary>
public interface ISubscriptionPaymentService
{
    /// <summary>
    /// Crea una suscripción recurrente (preapproval) en el proveedor de pagos y retorna la URL de checkout (init_point).
    /// </summary>
    Task<string> CreateSubscriptionAsync(
        Guid tenantId,
        string planName,
        decimal amount,
        string payerEmail,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancela una suscripción existente en el proveedor de pagos.
    /// </summary>
    Task<bool> CancelSubscriptionAsync(
        string externalSubscriptionId,
        CancellationToken cancellationToken = default);
}

