using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroSaaS.Core.Entities.TenantDomain;

namespace MicroSaaS.Infrastructure.Persistence.Configurations;

public class CafAuthorizationConfiguration : IEntityTypeConfiguration<CafAuthorization>
{
    public void Configure(EntityTypeBuilder<CafAuthorization> builder)
    {
        builder.ToTable("CafAuthorizations");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ValueGeneratedNever();

        builder.Property(c => c.TenantId)
            .IsRequired();

        builder.Property(c => c.DteType)
            .IsRequired();

        builder.Property(c => c.StartFolio)
            .IsRequired();

        builder.Property(c => c.EndFolio)
            .IsRequired();

        builder.Property(c => c.CurrentFolio)
            .IsRequired();

        builder.Property(c => c.XmlContent)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(c => c.AuthorizedAt)
            .IsRequired();

        builder.Property(c => c.ExpiresAt)
            .IsRequired();

        builder.Property(c => c.IsActive)
            .IsRequired();

        builder.HasIndex(c => new { c.TenantId, c.DteType, c.IsActive });
    }
}

