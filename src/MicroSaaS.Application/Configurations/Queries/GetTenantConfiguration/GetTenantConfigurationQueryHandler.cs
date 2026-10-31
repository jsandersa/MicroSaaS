using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MicroSaaS.Core.Interfaces;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.Application.Configurations.Queries.GetTenantConfiguration;

/// <summary>
/// Manejador de <see cref="GetTenantConfigurationQuery"/> que devuelve la configuración del Tenant autenticado
/// desde caché en memoria (<see cref="IMemoryCache"/>) o desde su esquema aislado en PostgreSQL.
/// </summary>
public class GetTenantConfigurationQueryHandler
    : IRequestHandler<GetTenantConfigurationQuery, TenantConfigurationDto?>
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    private readonly TenantDbContext _tenantDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IMemoryCache _memoryCache;

    public GetTenantConfigurationQueryHandler(
        TenantDbContext tenantDbContext,
        ITenantProvider tenantProvider,
        IMemoryCache memoryCache)
    {
        _tenantDbContext = tenantDbContext ?? throw new ArgumentNullException(nameof(tenantDbContext));
        _tenantProvider = tenantProvider ?? throw new ArgumentNullException(nameof(tenantProvider));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
    }

    public async Task<TenantConfigurationDto?> Handle(
        GetTenantConfigurationQuery request,
        CancellationToken cancellationToken)
    {
        var schema = _tenantProvider.GetCurrentSchema();
        var cacheKey = $"tenant_configuration:{schema}";

        if (_memoryCache.TryGetValue(cacheKey, out TenantConfigurationDto? cachedConfig) &&
            cachedConfig is not null)
        {
            return cachedConfig;
        }

        var config = await _tenantDbContext.Configurations
            .AsNoTracking()
            .Select(c => new TenantConfigurationDto(
                c.Id,
                c.TaxPercentage,
                c.CurrencyCode,
                c.CurrencySymbol,
                c.TimeZoneId,
                c.ReceiptFooterText))
            .FirstOrDefaultAsync(cancellationToken);

        if (config is not null)
        {
            _memoryCache.Set(cacheKey, config, CacheDuration);
        }

        return config;
    }
}

