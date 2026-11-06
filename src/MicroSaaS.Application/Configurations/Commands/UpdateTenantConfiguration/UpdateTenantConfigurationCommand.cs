using MediatR;
using MicroSaaS.Application.Configurations.Queries.GetTenantConfiguration;

namespace MicroSaaS.Application.Configurations.Commands.UpdateTenantConfiguration;

/// <summary>
/// Comando para actualizar los parámetros de configuración del Tenant autenticado.
/// </summary>
public record UpdateTenantConfigurationCommand(
    decimal TaxPercentage,
    string CurrencyCode,
    string CurrencySymbol,
    string TimeZoneId,
    string? ReceiptFooterText,
    decimal DefaultMarginPercentage = 30.00m,
    string? BusinessActivity = null,
    string? CompanyAddress = null,
    string? CompanyCommune = null) : IRequest<TenantConfigurationDto>;
