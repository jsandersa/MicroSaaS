using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroSaaS.Core.Entities;

namespace MicroSaaS.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .ValueGeneratedNever();

        builder.Property(t => t.Rut)
            .IsRequired()
            .HasMaxLength(12);

        builder.HasIndex(t => t.Rut)
            .IsUnique();

        builder.Property(t => t.LegalName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(t => t.BusinessLine)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(t => t.Address)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(t => t.SchemaName)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(t => t.SchemaName)
            .IsUnique();

        builder.Property(t => t.IsActive)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .IsRequired();
    }
}

