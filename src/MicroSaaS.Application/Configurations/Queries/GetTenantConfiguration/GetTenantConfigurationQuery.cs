using MediatR;

namespace MicroSaaS.Application.Configurations.Queries.GetTenantConfiguration;

public record TenantConfigurationDto(
    Guid Id,
    decimal TaxPercentage,
    string CurrencyCode,
    string CurrencySymbol,
    string TimeZoneId,
    string? ReceiptFooterText);

/// <summary>
/// Consulta MediatR para obtener los parámetros de configuración actuales del Tenant autenticado.
/// </summary>
public record GetTenantConfigurationQuery : IRequest<TenantConfigurationDto?>;

