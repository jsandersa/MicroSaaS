using Microsoft.EntityFrameworkCore;
using MicroSaaS.Core.Entities.TenantDomain;
using MicroSaaS.Core.Interfaces;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.Infrastructure.Multitenancy;

/// <summary>
/// Servicio encargado de crear físicamente el esquema aislado del nuevo Tenant en PostgreSQL
/// y aplicar dinámicamente las migraciones de <see cref="TenantDbContext"/> (<c>_context.Database.MigrateAsync()</c>)
/// para construir sus tablas de Ventas, Compras, Productos y Configuraciones.
/// </summary>
public class TenantProvisioner : ITenantProvisioner
{
    private readonly TenantDbContext _context;
    private readonly ITenantProvider _tenantProvider;

    public TenantProvisioner(
        TenantDbContext context,
        ITenantProvider tenantProvider)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _tenantProvider = tenantProvider ?? throw new ArgumentNullException(nameof(tenantProvider));
    }

    public async Task ProvisionTenantAsync(string schemaName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);

        var normalizedSchema = schemaName.Trim().ToLowerInvariant();
        var previousSchema = _tenantProvider.GetCurrentSchema();

        try
        {
            // 1. Establecer el esquema activo en ITenantProvider para que SchemaInterceptor apunte el search_path al nuevo esquema
            _tenantProvider.SetCurrentSchema(normalizedSchema);

            // 2. Crear el esquema y su tabla local de historial de migraciones para aislarlo de "public"
            var initSchemaSql = $"""
                CREATE SCHEMA IF NOT EXISTS "{normalizedSchema}";
                CREATE TABLE IF NOT EXISTS "{normalizedSchema}"."__EFMigrationsHistory" (
                    "MigrationId" character varying(150) NOT NULL,
                    "ProductVersion" character varying(32) NOT NULL,
                    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
                );
                """;

            await _context.Database.ExecuteSqlRawAsync(initSchemaSql, cancellationToken);

            // 3. Aplicar dinámicamente las migraciones de EF Core sobre el esquema del Tenant
            await _context.Database.MigrateAsync(cancellationToken);

            // 4. Garantizar idempotentemente la estructura completa de tablas del dominio privado (Productos, Ventas, Compras, Configuración)
            var ensureTablesSql = $"""
                CREATE TABLE IF NOT EXISTS "{normalizedSchema}"."Products" (
                    "Id" uuid NOT NULL,
                    "TenantId" character varying(64) NOT NULL DEFAULT '',
                    "Sku" character varying(50) NOT NULL,
                    "Name" character varying(150) NOT NULL,
                    "SalePrice" decimal(18,2) NOT NULL DEFAULT 0,
                    "CostPrice" decimal(18,2) NOT NULL DEFAULT 0,
                    "CurrentStock" integer NOT NULL DEFAULT 0,
                    "IsActive" boolean NOT NULL DEFAULT true,
                    CONSTRAINT "PK_Products" PRIMARY KEY ("Id")
                );
                ALTER TABLE "{normalizedSchema}"."Products" ADD COLUMN IF NOT EXISTS "TenantId" character varying(64) NOT NULL DEFAULT '';
                ALTER TABLE "{normalizedSchema}"."Products" ADD COLUMN IF NOT EXISTS "SalePrice" decimal(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE "{normalizedSchema}"."Products" ADD COLUMN IF NOT EXISTS "CostPrice" decimal(18,2) NOT NULL DEFAULT 0;
                ALTER TABLE "{normalizedSchema}"."Products" ADD COLUMN IF NOT EXISTS "CurrentStock" integer NOT NULL DEFAULT 0;
                """;

            await _context.Database.ExecuteSqlRawAsync(ensureTablesSql, cancellationToken);

            // 5. Insertar configuración tributaria inicial por defecto si aún no existe en el esquema
            if (!await _context.Configurations.AnyAsync(cancellationToken))
            {
                var defaultConfig = TenantConfiguration.CreateDefault();
                await _context.Configurations.AddAsync(defaultConfig, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
        finally
        {
            _tenantProvider.SetCurrentSchema(previousSchema);
        }
    }
}

