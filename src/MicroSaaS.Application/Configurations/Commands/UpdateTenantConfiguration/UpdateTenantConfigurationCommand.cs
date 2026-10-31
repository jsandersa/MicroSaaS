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
    string? ReceiptFooterText) : IRequest<TenantConfigurationDto>;

