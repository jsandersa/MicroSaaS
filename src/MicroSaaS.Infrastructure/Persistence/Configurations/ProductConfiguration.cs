using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroSaaS.Core.Entities.TenantDomain;

namespace MicroSaaS.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    private readonly TenantDbContext? _context;

    public ProductConfiguration()
    {
    }

    public ProductConfiguration(TenantDbContext context)
    {
        _context = context;
    }

    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedNever();

        builder.Property(p => p.TenantId)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(p => p.TenantId);

        // Filtro global de consulta para TenantId
        if (_context is not null)
        {
            builder.HasQueryFilter(p => _context.CurrentTenantIdentifier == null || p.TenantId == _context.CurrentTenantIdentifier);
        }
        else
        {
            builder.HasQueryFilter(p => !string.IsNullOrEmpty(p.TenantId));
        }

        builder.Property(p => p.Sku)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(p => new { p.TenantId, p.Sku })
            .IsUnique();

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(p => p.SalePrice)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasColumnType("decimal(18,2)");

        builder.Ignore(p => p.SKU);

        builder.Property(p => p.CostPrice)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasColumnType("decimal(18,2)");

        builder.Property(p => p.PmpCost)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasColumnType("decimal(18,2)");

        builder.Property(p => p.CurrentStock)
            .IsRequired();

        builder.Property(p => p.IsActive)
            .IsRequired();
    }
}
