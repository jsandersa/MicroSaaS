using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroSaaS.Core.Entities.TenantDomain;

namespace MicroSaaS.Infrastructure.Persistence.Configurations;

public class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    private readonly TenantDbContext? _context;

    public PurchaseConfiguration()
    {
    }

    public PurchaseConfiguration(TenantDbContext context)
    {
        _context = context;
    }

    public void Configure(EntityTypeBuilder<Purchase> builder)
    {
        builder.ToTable("Purchases");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedNever();

        builder.Property(p => p.TenantId)
            .IsRequired();

        builder.HasIndex(p => p.TenantId);
        builder.HasIndex(p => new { p.TenantId, p.TipoDte, p.Folio });

        // Filtro global por TenantId
        if (_context is not null)
        {
            builder.HasQueryFilter(p => _context.CurrentTenantId == null || p.TenantId == _context.CurrentTenantId);
        }
        else
        {
            builder.HasQueryFilter(p => p.TenantId != Guid.Empty);
        }

        builder.Property(p => p.Folio)
            .IsRequired();

        builder.Property(p => p.TipoDte)
            .IsRequired();

        builder.Property(p => p.FechaEmision)
            .IsRequired();

        builder.Property(p => p.RutProveedor)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(p => p.RazonSocialProveedor)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.MontoNeto)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasColumnType("decimal(18,2)");

        builder.Property(p => p.Iva)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasColumnType("decimal(18,2)");

        builder.Property(p => p.MontoTotal)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasColumnType("decimal(18,2)");

        builder.HasMany(p => p.Items)
            .WithOne(i => i.Purchase)
            .HasForeignKey(i => i.PurchaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(p => p.DomainEvents);
    }
}

public class PurchaseItemConfiguration : IEntityTypeConfiguration<PurchaseItem>
{
    private readonly TenantDbContext? _context;

    public PurchaseItemConfiguration()
    {
    }

    public PurchaseItemConfiguration(TenantDbContext context)
    {
        _context = context;
    }

    public void Configure(EntityTypeBuilder<PurchaseItem> builder)
    {
        builder.ToTable("PurchaseItems");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .ValueGeneratedNever();

        if (_context is not null)
        {
            builder.HasQueryFilter(i => _context.CurrentTenantId == null || i.Purchase!.TenantId == _context.CurrentTenantId);
        }
        else
        {
            builder.HasQueryFilter(i => i.Purchase!.TenantId != Guid.Empty);
        }

        builder.Property(i => i.PurchaseId)
            .IsRequired();

        builder.Property(i => i.NumeroLinea)
            .IsRequired();

        builder.Property(i => i.NombreItem)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(i => i.Cantidad)
            .IsRequired()
            .HasPrecision(18, 4)
            .HasColumnType("decimal(18,4)");

        builder.Property(i => i.PrecioUnitario)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasColumnType("decimal(18,2)");

        builder.Property(i => i.MontoItem)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasColumnType("decimal(18,2)");

        builder.Property(i => i.EsExento)
            .IsRequired();
    }
}

