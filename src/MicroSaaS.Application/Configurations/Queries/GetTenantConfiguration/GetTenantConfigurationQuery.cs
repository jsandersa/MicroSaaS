using MediatR;

namespace MicroSaaS.Application.Configurations.Queries.GetTenantConfiguration;

public record TenantConfigurationDto(
    Guid Id,
    decimal TaxPercentage,
    string CurrencyCode,
    string CurrencySymbol,
    string TimeZoneId,
    string? ReceiptFooterText,
    decimal DefaultMarginPercentage = 30.00m,
    string? BusinessActivity = null,
    string? CompanyAddress = null,
    string? CompanyCommune = null);

/// <summary>
/// Consulta MediatR para obtener los parámetros de configuración actuales del Tenant autenticado.
/// </summary>
public record GetTenantConfigurationQuery : IRequest<TenantConfigurationDto?>;
