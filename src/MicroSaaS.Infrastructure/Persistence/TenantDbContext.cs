using Microsoft.EntityFrameworkCore;
using MicroSaaS.Core.Entities.TenantDomain;
using MicroSaaS.Core.Interfaces;
using MicroSaaS.Infrastructure.Persistence.Configurations;
using TenantDomainConfiguration = MicroSaaS.Core.Entities.TenantDomain.TenantConfiguration;

namespace MicroSaaS.Infrastructure.Persistence;

/// <summary>
/// Contexto de base de datos dinámico para el dominio privado de cada Tenant (Inventario, Configuraciones, Bóveda SII, Ventas, Compras, etc.).
/// Su esquema en PostgreSQL es resuelto en tiempo de ejecución mediante <see cref="Interceptors.SchemaInterceptor"/>.
/// </summary>
public class TenantDbContext : DbContext
{
    private readonly ITenantProvider? _tenantProvider;

    public TenantDbContext(
        DbContextOptions<TenantDbContext> options,
        ITenantProvider? tenantProvider = null)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public Guid? CurrentTenantId => _tenantProvider?.GetCurrentTenantId();

    public DbSet<Product> Products => Set<Product>();
    public DbSet<TenantDomainConfiguration> Configurations => Set<TenantDomainConfiguration>();
    public DbSet<TenantDigitalCertificate> DigitalCertificates => Set<TenantDigitalCertificate>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Regla de oro: NO se define HasDefaultSchema.
        // El esquema activo se establece dinámicamente por petición en el SchemaInterceptor.
        modelBuilder.ApplyConfiguration(new ProductConfiguration());
        modelBuilder.ApplyConfiguration(new TenantConfigurationEntityConfiguration());
        modelBuilder.ApplyConfiguration(new TenantDigitalCertificateConfiguration());
        modelBuilder.ApplyConfiguration(new SaleConfiguration(this));
        modelBuilder.ApplyConfiguration(new SaleItemConfiguration(this));
        modelBuilder.ApplyConfiguration(new PurchaseConfiguration(this));
        modelBuilder.ApplyConfiguration(new PurchaseItemConfiguration(this));
    }
}
