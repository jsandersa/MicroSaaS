using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroSaaS.Core.Entities.TenantDomain;

namespace MicroSaaS.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de Entity Framework Core para la entidad <see cref="KardexTransaction"/>.
/// </summary>
public sealed class KardexTransactionConfiguration : IEntityTypeConfiguration<KardexTransaction>
{
    private readonly TenantDbContext _context;

    public KardexTransactionConfiguration(TenantDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public void Configure(EntityTypeBuilder<KardexTransaction> builder)
    {
        builder.ToTable("KardexTransactions");

        builder.HasKey(k => k.Id);

        builder.Property(k => k.Id)
            .ValueGeneratedNever();

        builder.Property(k => k.TenantId)
            .IsRequired();

        builder.Property(k => k.ProductId)
            .IsRequired();

        builder.Property(k => k.MovementType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(k => k.Quantity)
            .IsRequired();

        builder.Property(k => k.UnitCost)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(k => k.TotalValue)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(k => k.TransactionDate)
            .IsRequired();

        builder.Property(k => k.Reason)
            .IsRequired(false)
            .HasMaxLength(300);

        builder.HasIndex(k => new { k.TenantId, k.ProductId, k.TransactionDate });

        builder.HasQueryFilter(k => _context.CurrentTenantId == null || k.TenantId == _context.CurrentTenantId);
    }
}

