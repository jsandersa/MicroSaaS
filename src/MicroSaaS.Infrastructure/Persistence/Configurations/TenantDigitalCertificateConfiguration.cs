using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroSaaS.Core.Entities.TenantDomain;

namespace MicroSaaS.Infrastructure.Persistence.Configurations;

public class TenantDigitalCertificateConfiguration : IEntityTypeConfiguration<TenantDigitalCertificate>
{
    public void Configure(EntityTypeBuilder<TenantDigitalCertificate> builder)
    {
        builder.ToTable("DigitalCertificates");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ValueGeneratedNever();

        builder.Property(c => c.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(c => c.FileData)
            .IsRequired()
            .HasColumnType("bytea");

        builder.Property(c => c.EncryptedPassword)
            .IsRequired()
            .HasMaxLength(1024);

        builder.Property(c => c.ExpirationDate)
            .IsRequired();

        builder.Property(c => c.Subject)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(c => c.IsActive)
            .IsRequired();
    }
}

