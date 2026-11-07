namespace MicroSaaS.Infrastructure.Payments.MercadoPago;

/// <summary>
/// Opciones de configuración para la integración con la API de Suscripciones (Preapproval) de Mercado Pago.
/// </summary>
public sealed class MercadoPagoOptions
{
    public const string SectionName = "MercadoPago";

    /// <summary>
    /// Access Token privado de producción o pruebas (APP_USR-...).
    /// </summary>
    public string AccessToken { get; set; } = "APP_USR-5728087613147389-110611-e4a09b28914c048a2b7c60d8a5572a98-3818301145";

    /// <summary>
    /// Public Key del comercio en Mercado Pago.
    /// </summary>
    public string PublicKey { get; set; } = "APP_USR-32a1e8a7-4d2d-4543-a257-731a7bfc5aec";

    /// <summary>
    /// URL base de la API de Mercado Pago.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.mercadopago.com";

    /// <summary>
    /// URL de retorno al finalizar el flujo de suscripción en el checkout de Mercado Pago.
    /// </summary>
    public string BackUrl { get; set; } = "http://localhost:4200/inventory";

    /// <summary>
    /// Moneda por defecto para los cargos recurrentes (ej. CLP para Chile).
    /// </summary>
    public string CurrencyId { get; set; } = "CLP";

    /// <summary>
    /// Clave secreta para validar firmas de Webhooks de Mercado Pago.
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;
}

