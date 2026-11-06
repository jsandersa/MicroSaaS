using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroSaaS.Core.Entities.TenantDomain;

namespace MicroSaaS.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de Entity Framework Core para la entidad <see cref="CashRegisterSession"/>.
/// </summary>
public sealed class CashRegisterSessionConfiguration : IEntityTypeConfiguration<CashRegisterSession>
{
    private readonly TenantDbContext _context;

    public CashRegisterSessionConfiguration(TenantDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public void Configure(EntityTypeBuilder<CashRegisterSession> builder)
    {
        builder.ToTable("CashRegisterSessions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.OpeningAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.ExpectedClosingAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.ActualClosingAmount)
            .HasPrecision(18, 2)
            .IsRequired(false);

        builder.Property(x => x.OpenedAt)
            .IsRequired();

        builder.Property(x => x.ClosedAt)
            .IsRequired(false);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.UserId, x.Status });

        builder.HasQueryFilter(x => _context.CurrentTenantId == null || x.TenantId == _context.CurrentTenantId);
    }
}

