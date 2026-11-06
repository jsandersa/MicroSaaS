using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MicroSaaS.Core.Interfaces;

namespace MicroSaaS.Infrastructure.Payments.MercadoPago;

/// <summary>
/// Implementación del servicio de suscripciones recurrentes utilizando la API de Preapproval de Mercado Pago.
/// </summary>
public sealed class MercadoPagoSubscriptionService : ISubscriptionPaymentService
{
    private readonly HttpClient _httpClient;
    private readonly MercadoPagoOptions _options;
    private readonly ILogger<MercadoPagoSubscriptionService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public MercadoPagoSubscriptionService(
        HttpClient httpClient,
        IOptions<MercadoPagoOptions> options,
        ILogger<MercadoPagoSubscriptionService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> CreateSubscriptionAsync(
        Guid tenantId,
        string planName,
        decimal amount,
        string payerEmail,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("El TenantId no puede ser vacío.", nameof(tenantId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(planName);
        ArgumentException.ThrowIfNullOrWhiteSpace(payerEmail);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        if (string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            throw new InvalidOperationException(
                "No se ha configurado el AccessToken de Mercado Pago en la sección 'MercadoPago'.");
        }

        var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl)
            ? "https://api.mercadopago.com"
            : _options.BaseUrl.TrimEnd('/');

        var backUrl = string.IsNullOrWhiteSpace(_options.BackUrl)
            ? "http://localhost:4200/inventory"
            : _options.BackUrl;

        var currencyId = string.IsNullOrWhiteSpace(_options.CurrencyId)
            ? "CLP"
            : _options.CurrencyId;

        var payload = new MercadoPagoPreapprovalRequest(
            Reason: planName,
            ExternalReference: tenantId.ToString(),
            PayerEmail: payerEmail.Trim(),
            BackUrl: backUrl,
            Status: "pending",
            AutoRecurring: new MercadoPagoAutoRecurringRequest(
                Frequency: 1,
                FrequencyType: "months",
                TransactionAmount: amount,
                CurrencyId: currencyId));

        var jsonContent = JsonSerializer.Serialize(payload, JsonOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/preapproval")
        {
            Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
        request.Headers.Add("X-Idempotency-Key", $"{tenantId:N}-{DateTime.UtcNow:yyyyMMddHHmm}");

        _logger.LogInformation(
            "Creando suscripción Preapproval en Mercado Pago para el Tenant '{TenantId}' (Plan: '{PlanName}', Monto: {Amount} {Currency}).",
            tenantId,
            planName,
            amount,
            currencyId);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Error HTTP {StatusCode} al crear suscripción en Mercado Pago para el Tenant '{TenantId}': {ResponseBody}",
                (int)response.StatusCode,
                tenantId,
                responseBody);

            throw new HttpRequestException(
                $"Mercado Pago rechazó la creación de la suscripción (HTTP {(int)response.StatusCode}): {responseBody}",
                inner: null,
                statusCode: response.StatusCode);
        }

        var preapprovalResponse = JsonSerializer.Deserialize<MercadoPagoPreapprovalResponse>(responseBody, JsonOptions);

        if (preapprovalResponse is null || string.IsNullOrWhiteSpace(preapprovalResponse.InitPoint))
        {
            throw new InvalidOperationException(
                "La respuesta de Mercado Pago no incluyó una URL de pago válida ('init_point').");
        }

        _logger.LogInformation(
            "Suscripción Preapproval '{PreapprovalId}' creada exitosamente en Mercado Pago para el Tenant '{TenantId}'.",
            preapprovalResponse.Id,
            tenantId);

        return preapprovalResponse.InitPoint;
    }

    public async Task<bool> CancelSubscriptionAsync(
        string externalSubscriptionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalSubscriptionId);

        if (string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            throw new InvalidOperationException(
                "No se ha configurado el AccessToken de Mercado Pago en la sección 'MercadoPago'.");
        }

        var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl)
            ? "https://api.mercadopago.com"
            : _options.BaseUrl.TrimEnd('/');

        var payload = new { status = "cancelled" };
        var jsonContent = JsonSerializer.Serialize(payload, JsonOptions);

        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"{baseUrl}/preapproval/{Uri.EscapeDataString(externalSubscriptionId)}")
        {
            Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Error HTTP {StatusCode} al cancelar suscripción '{SubscriptionId}' en Mercado Pago: {ResponseBody}",
                (int)response.StatusCode,
                externalSubscriptionId,
                responseBody);

            return false;
        }

        return true;
    }

    private sealed record MercadoPagoPreapprovalRequest(
        [property: JsonPropertyName("reason")] string Reason,
        [property: JsonPropertyName("external_reference")] string ExternalReference,
        [property: JsonPropertyName("payer_email")] string PayerEmail,
        [property: JsonPropertyName("back_url")] string BackUrl,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("auto_recurring")] MercadoPagoAutoRecurringRequest AutoRecurring);

    private sealed record MercadoPagoAutoRecurringRequest(
        [property: JsonPropertyName("frequency")] int Frequency,
        [property: JsonPropertyName("frequency_type")] string FrequencyType,
        [property: JsonPropertyName("transaction_amount")] decimal TransactionAmount,
        [property: JsonPropertyName("currency_id")] string CurrencyId);

    private sealed record MercadoPagoPreapprovalResponse(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("init_point")] string? InitPoint,
        [property: JsonPropertyName("status")] string? Status);
}

