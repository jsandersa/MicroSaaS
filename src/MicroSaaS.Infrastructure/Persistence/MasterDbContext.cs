using Microsoft.EntityFrameworkCore;
using MicroSaaS.Core.Entities;
using MicroSaaS.Infrastructure.Persistence.Configurations;

namespace MicroSaaS.Infrastructure.Persistence;

/// <summary>
/// Contexto maestro de base de datos para la administración de Tenants, Suscripciones y Facturación (esquema "public").
/// </summary>
public class MasterDbContext : DbContext
{
    public MasterDbContext(DbContextOptions<MasterDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<BillingRecord> BillingRecords => Set<BillingRecord>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("public");

        modelBuilder.ApplyConfiguration(new TenantConfiguration());
        modelBuilder.ApplyConfiguration(new SubscriptionConfiguration());
        modelBuilder.ApplyConfiguration(new BillingRecordConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());
    }
}
