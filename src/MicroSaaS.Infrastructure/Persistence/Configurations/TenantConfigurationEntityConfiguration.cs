using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TenantDomainConfiguration = MicroSaaS.Core.Entities.TenantDomain.TenantConfiguration;

namespace MicroSaaS.Infrastructure.Persistence.Configurations;

public class TenantConfigurationEntityConfiguration : IEntityTypeConfiguration<TenantDomainConfiguration>
{
    public void Configure(EntityTypeBuilder<TenantDomainConfiguration> builder)
    {
        builder.ToTable("Configurations");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ValueGeneratedNever();

        builder.Property(c => c.TaxPercentage)
            .IsRequired()
            .HasPrecision(5, 2)
            .HasColumnType("decimal(5,2)")
            .HasDefaultValue(19.00m);

        builder.Property(c => c.CurrencyCode)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(c => c.CurrencySymbol)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(c => c.TimeZoneId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.ReceiptFooterText)
            .IsRequired(false)
            .HasMaxLength(500);
    }
}

