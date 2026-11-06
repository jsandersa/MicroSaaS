using MediatR;
using Microsoft.EntityFrameworkCore;
using MicroSaaS.Core.Entities.TenantDomain;
using MicroSaaS.Core.Events;
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
    private readonly IMediator? _mediator;

    public TenantDbContext(
        DbContextOptions<TenantDbContext> options,
        ITenantProvider? tenantProvider = null,
        IMediator? mediator = null)
        : base(options)
    {
        _tenantProvider = tenantProvider;
        _mediator = mediator;
    }

    public Guid? CurrentTenantId => _tenantProvider?.GetCurrentTenantId();
    public string? CurrentTenantIdentifier => _tenantProvider?.GetCurrentTenantId()?.ToString();

    public DbSet<Product> Products { get; set; }
    public DbSet<TenantDomainConfiguration> Configurations => Set<TenantDomainConfiguration>();
    public DbSet<TenantDigitalCertificate> DigitalCertificates => Set<TenantDigitalCertificate>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<CashRegisterSession> CashRegisterSessions => Set<CashRegisterSession>();
    public DbSet<KardexTransaction> KardexTransactions => Set<KardexTransaction>();
    public DbSet<DteDocument> DteDocuments => Set<DteDocument>();
    public DbSet<CafAuthorization> CafAuthorizations => Set<CafAuthorization>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_mediator is not null)
        {
            var entitiesWithEvents = ChangeTracker
                .Entries<IHasDomainEvents>()
                .Select(e => e.Entity)
                .Where(e => e.DomainEvents.Count > 0)
                .ToList();

            var domainEvents = entitiesWithEvents
                .SelectMany(e => e.DomainEvents)
                .ToList();

            foreach (var entity in entitiesWithEvents)
            {
                entity.ClearDomainEvents();
            }

            foreach (var domainEvent in domainEvents)
            {
                await _mediator.Publish(domainEvent, cancellationToken);
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Regla de oro: NO se define HasDefaultSchema.
        // El esquema activo se establece dinámicamente por petición en el SchemaInterceptor.
        modelBuilder.ApplyConfiguration(new ProductConfiguration(this));
        modelBuilder.ApplyConfiguration(new TenantConfigurationEntityConfiguration());
        modelBuilder.ApplyConfiguration(new TenantDigitalCertificateConfiguration());
        modelBuilder.ApplyConfiguration(new SaleConfiguration(this));
        modelBuilder.ApplyConfiguration(new SaleItemConfiguration(this));
        modelBuilder.ApplyConfiguration(new PurchaseConfiguration(this));
        modelBuilder.ApplyConfiguration(new PurchaseItemConfiguration(this));
        modelBuilder.ApplyConfiguration(new CashRegisterSessionConfiguration(this));
        modelBuilder.ApplyConfiguration(new KardexTransactionConfiguration(this));
        modelBuilder.ApplyConfiguration(new DteDocumentConfiguration(this));
        modelBuilder.ApplyConfiguration(new CafAuthorizationConfiguration());
    }
}
