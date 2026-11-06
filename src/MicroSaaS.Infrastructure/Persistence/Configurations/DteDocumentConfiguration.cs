using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroSaaS.Core.Entities.TenantDomain;

namespace MicroSaaS.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración de Entity Framework Core para la entidad <see cref="DteDocument"/>.
/// </summary>
public sealed class DteDocumentConfiguration : IEntityTypeConfiguration<DteDocument>
{
    private readonly TenantDbContext _context;

    public DteDocumentConfiguration(TenantDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public void Configure(EntityTypeBuilder<DteDocument> builder)
    {
        builder.ToTable("DteDocuments");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .ValueGeneratedNever();

        builder.Property(d => d.TenantId)
            .IsRequired();

        builder.Property(d => d.SaleId)
            .IsRequired();

        builder.Property(d => d.DteType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(d => d.Folio)
            .IsRequired();

        builder.Property(d => d.TotalAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(d => d.XmlContent)
            .IsRequired();

        builder.Property(d => d.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(d => d.EmissionDate)
            .IsRequired();

        builder.HasIndex(d => new { d.TenantId, d.DteType, d.Folio })
            .IsUnique();

        builder.HasQueryFilter(d => _context.CurrentTenantId == null || d.TenantId == _context.CurrentTenantId);
    }
}

