using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroSaaS.Core.Entities.TenantDomain;

namespace MicroSaaS.Infrastructure.Persistence.Configurations;

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    private readonly TenantDbContext? _context;

    public SaleConfiguration()
    {
    }

    public SaleConfiguration(TenantDbContext context)
    {
        _context = context;
    }

    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("Sales");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .ValueGeneratedNever();

        builder.Property(s => s.TenantId)
            .IsRequired();

        builder.Property(s => s.UserId)
            .IsRequired();

        builder.Property(s => s.CashRegisterSessionId)
            .IsRequired();

        builder.Property(s => s.TotalAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.HasIndex(s => s.TenantId);
        builder.HasIndex(s => new { s.TenantId, s.TipoDte, s.Folio });

        if (_context is not null)
        {
            builder.HasQueryFilter(s => _context.CurrentTenantId == null || s.TenantId == _context.CurrentTenantId);
        }
        else
        {
            builder.HasQueryFilter(s => s.TenantId != Guid.Empty);
        }

        builder.Property(s => s.Folio)
            .IsRequired();

        builder.Property(s => s.TipoDte)
            .IsRequired();

        builder.Property(s => s.FechaEmision)
            .IsRequired();

        builder.Property(s => s.RutCliente)
            .IsRequired(false)
            .HasMaxLength(20);

        builder.Property(s => s.RazonSocialCliente)
            .IsRequired(false)
            .HasMaxLength(200);

        builder.Property(s => s.MontoNeto)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasColumnType("decimal(18,2)");

        builder.Property(s => s.Iva)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasColumnType("decimal(18,2)");

        builder.Property(s => s.MontoTotal)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasColumnType("decimal(18,2)");

        builder.Property(s => s.PaymentMethod)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(s => s.CashAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.CardAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasMany(s => s.Items)
            .WithOne(i => i.Sale)
            .HasForeignKey(i => i.SaleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(s => s.DomainEvents);
    }
}

public class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    private readonly TenantDbContext? _context;

    public SaleItemConfiguration()
    {
    }

    public SaleItemConfiguration(TenantDbContext context)
    {
        _context = context;
    }

    public void Configure(EntityTypeBuilder<SaleItem> builder)
    {
        builder.ToTable("SaleItems");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .ValueGeneratedNever();

        if (_context is not null)
        {
            builder.HasQueryFilter(i => _context.CurrentTenantId == null || i.Sale!.TenantId == _context.CurrentTenantId);
        }
        else
        {
            builder.HasQueryFilter(i => i.Sale!.TenantId != Guid.Empty);
        }

        builder.Property(i => i.SaleId)
            .IsRequired();

        builder.Property(i => i.ProductId)
            .IsRequired();

        builder.Property(i => i.Quantity)
            .IsRequired();

        builder.Property(i => i.UnitPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(i => i.Subtotal)
            .HasPrecision(18, 2)
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
