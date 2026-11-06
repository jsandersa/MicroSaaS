using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MicroSaaS.Application.Configurations.Queries.GetTenantConfiguration;
using MicroSaaS.Core.Entities.TenantDomain;
using MicroSaaS.Core.Interfaces;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.Application.Configurations.Commands.UpdateTenantConfiguration;

/// <summary>
/// Manejador de <see cref="UpdateTenantConfigurationCommand"/> que persiste los cambios en el esquema aislado del Tenant,
/// recalcula los precios de venta sobre el PMP según el nuevo margen comercial parametrizado e invalida/actualiza la entrada en <see cref="IMemoryCache"/>.
/// </summary>
public class UpdateTenantConfigurationCommandHandler
    : IRequestHandler<UpdateTenantConfigurationCommand, TenantConfigurationDto>
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    private readonly TenantDbContext _tenantDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IMemoryCache _memoryCache;

    public UpdateTenantConfigurationCommandHandler(
        TenantDbContext tenantDbContext,
        ITenantProvider tenantProvider,
        IMemoryCache memoryCache)
    {
        _tenantDbContext = tenantDbContext ?? throw new ArgumentNullException(nameof(tenantDbContext));
        _tenantProvider = tenantProvider ?? throw new ArgumentNullException(nameof(tenantProvider));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
    }

    public async Task<TenantConfigurationDto> Handle(
        UpdateTenantConfigurationCommand request,
        CancellationToken cancellationToken)
    {
        var config = await _tenantDbContext.Configurations
            .FirstOrDefaultAsync(cancellationToken);

        var effectiveMargin = request.DefaultMarginPercentage >= 0m
            ? request.DefaultMarginPercentage
            : 30.00m;

        if (config is null)
        {
            config = TenantConfiguration.Create(
                taxPercentage: request.TaxPercentage,
                currencyCode: request.CurrencyCode,
                currencySymbol: request.CurrencySymbol,
                timeZoneId: request.TimeZoneId,
                receiptFooterText: request.ReceiptFooterText,
                defaultMarginPercentage: effectiveMargin,
                businessActivity: request.BusinessActivity,
                companyAddress: request.CompanyAddress,
                companyCommune: request.CompanyCommune);

            await _tenantDbContext.Configurations.AddAsync(config, cancellationToken);
        }
        else
        {
            config.Update(
                taxPercentage: request.TaxPercentage,
                currencyCode: request.CurrencyCode,
                currencySymbol: request.CurrencySymbol,
                timeZoneId: request.TimeZoneId,
                receiptFooterText: request.ReceiptFooterText,
                defaultMarginPercentage: effectiveMargin,
                businessActivity: request.BusinessActivity,
                companyAddress: request.CompanyAddress,
                companyCommune: request.CompanyCommune);
        }

        // Actualizar precios de venta de productos con PMP conocido según el nuevo margen parametrizado
        var products = await _tenantDbContext.Products.ToListAsync(cancellationToken);
        foreach (var product in products)
        {
            var basePmp = product.PmpCost > 0m ? product.PmpCost : product.CostPrice;
            if (basePmp > 0m)
            {
                var updatedSalePrice = Math.Round(
                    basePmp * (1m + (effectiveMargin / 100m)),
                    0,
                    MidpointRounding.AwayFromZero);

                product.Update(
                    sku: product.Sku,
                    name: product.Name,
                    salePrice: updatedSalePrice,
                    costPrice: basePmp,
                    currentStock: product.CurrentStock);
            }
        }

        await _tenantDbContext.SaveChangesAsync(cancellationToken);

        var dto = new TenantConfigurationDto(
            config.Id,
            config.TaxPercentage,
            config.CurrencyCode,
            config.CurrencySymbol,
            config.TimeZoneId,
            config.ReceiptFooterText,
            config.DefaultMarginPercentage,
            config.BusinessActivity,
            config.CompanyAddress,
            config.CompanyCommune);

        var schema = _tenantProvider.GetCurrentSchema();
        _memoryCache.Set($"tenant_configuration:{schema}", dto, CacheDuration);

        return dto;
    }
}
