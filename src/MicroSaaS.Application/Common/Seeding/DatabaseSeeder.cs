using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MicroSaaS.Application.Tenants.Commands.RegisterTenant;
using MicroSaaS.Core.Entities.TenantDomain;
using MicroSaaS.Core.Interfaces;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.Application.Common.Seeding;

/// <summary>
/// Inicializador de datos semilla (Data Seeder) para entornos nuevos o contenedores con volúmenes vacíos.
/// Reutiliza <see cref="RegisterTenantCommand"/> para garantizar el aprovisionamiento completo del esquema aislado
/// y su registro inicial en la tabla <c>Configurations</c>.
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        var masterDb = serviceProvider.GetRequiredService<MasterDbContext>();
        var tenantDb = serviceProvider.GetRequiredService<TenantDbContext>();
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var tenantProvider = serviceProvider.GetRequiredService<ITenantProvider>();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger(nameof(DatabaseSeeder));

        if (!await masterDb.Tenants.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Iniciando Data Seeding: registrando Tenant inicial 'Comercializadora Sur SpA' (77.654.321-K)...");

            var initialTenantCommand = new RegisterTenantCommand(
                Rut: "77.654.321-K",
                LegalName: "Comercializadora Sur SpA",
                BusinessLine: "Comercialización y Distribución",
                Address: "Av. Libertador Bernardo O'Higgins 1234, Santiago",
                AdminEmail: "admin@comercializadora.cl",
                AdminPassword: "Password123!");

            var tenantId = await mediator.Send(initialTenantCommand, cancellationToken);

            // Asegurar que el esquema creado tenga su registro por defecto en la tabla Configurations
            await EnsureTenantDefaultConfigurationAsync(
                tenantDb,
                tenantProvider,
                schemaName: "t_77654321k",
                logger,
                cancellationToken);

            tenantProvider.SetCurrentSchema("public");

            logger.LogInformation(
                "Data Seeding completado exitosamente. TenantId: {TenantId}, Esquema: 't_77654321k', Usuario: 'admin@comercializadora.cl'.",
                tenantId);
            return;
        }

        // Si ya existen Tenants en la BD local, asegurar que cada esquema existente cuente con la tabla Configurations y su registro por defecto
        var tenants = await masterDb.Tenants
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        foreach (var tenant in tenants)
        {
            await EnsureTenantDefaultConfigurationAsync(
                tenantDb,
                tenantProvider,
                tenant.SchemaName,
                logger,
                cancellationToken);
        }

        tenantProvider.SetCurrentSchema("public");
    }

    private static async Task EnsureTenantDefaultConfigurationAsync(
        TenantDbContext tenantDb,
        ITenantProvider tenantProvider,
        string schemaName,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        tenantProvider.SetCurrentSchema(schemaName);

        var ensureTableSql = $"""
            CREATE TABLE IF NOT EXISTS "{schemaName}"."Configurations" (
                "Id" uuid NOT NULL,
                "TaxPercentage" decimal(5,2) NOT NULL DEFAULT 19.00,
                "CurrencyCode" character varying(3) NOT NULL,
                "CurrencySymbol" character varying(10) NOT NULL,
                "TimeZoneId" character varying(100) NOT NULL,
                "ReceiptFooterText" character varying(500) NULL,
                CONSTRAINT "PK_Configurations" PRIMARY KEY ("Id")
            );
            CREATE TABLE IF NOT EXISTS "{schemaName}"."DigitalCertificates" (
                "Id" uuid NOT NULL,
                "FileName" character varying(255) NOT NULL,
                "FileData" bytea NOT NULL,
                "EncryptedPassword" character varying(1024) NOT NULL,
                "ExpirationDate" timestamp with time zone NOT NULL,
                "Subject" character varying(500) NOT NULL,
                "IsActive" boolean NOT NULL,
                CONSTRAINT "PK_DigitalCertificates" PRIMARY KEY ("Id")
            );
            CREATE TABLE IF NOT EXISTS "{schemaName}"."Sales" (
                "Id" uuid NOT NULL,
                "TenantId" uuid NOT NULL,
                "Folio" integer NOT NULL,
                "TipoDte" integer NOT NULL,
                "FechaEmision" timestamp with time zone NOT NULL,
                "RutCliente" character varying(20) NOT NULL,
                "RazonSocialCliente" character varying(200) NOT NULL,
                "MontoNeto" decimal(18,2) NOT NULL,
                "Iva" decimal(18,2) NOT NULL,
                "MontoTotal" decimal(18,2) NOT NULL,
                CONSTRAINT "PK_Sales" PRIMARY KEY ("Id")
            );
            CREATE TABLE IF NOT EXISTS "{schemaName}"."SaleItems" (
                "Id" uuid NOT NULL,
                "SaleId" uuid NOT NULL,
                "NumeroLinea" integer NOT NULL,
                "NombreItem" character varying(200) NOT NULL,
                "Cantidad" decimal(18,4) NOT NULL,
                "PrecioUnitario" decimal(18,2) NOT NULL,
                "MontoItem" decimal(18,2) NOT NULL,
                "EsExento" boolean NOT NULL,
                CONSTRAINT "PK_SaleItems" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_SaleItems_Sales_SaleId" FOREIGN KEY ("SaleId") REFERENCES "{schemaName}"."Sales" ("Id") ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS "{schemaName}"."Purchases" (
                "Id" uuid NOT NULL,
                "TenantId" uuid NOT NULL,
                "Folio" integer NOT NULL,
                "TipoDte" integer NOT NULL,
                "FechaEmision" timestamp with time zone NOT NULL,
                "RutProveedor" character varying(20) NOT NULL,
                "RazonSocialProveedor" character varying(200) NOT NULL,
                "MontoNeto" decimal(18,2) NOT NULL,
                "Iva" decimal(18,2) NOT NULL,
                "MontoTotal" decimal(18,2) NOT NULL,
                CONSTRAINT "PK_Purchases" PRIMARY KEY ("Id")
            );
            CREATE TABLE IF NOT EXISTS "{schemaName}"."PurchaseItems" (
                "Id" uuid NOT NULL,
                "PurchaseId" uuid NOT NULL,
                "NumeroLinea" integer NOT NULL,
                "NombreItem" character varying(200) NOT NULL,
                "Cantidad" decimal(18,4) NOT NULL,
                "PrecioUnitario" decimal(18,2) NOT NULL,
                "MontoItem" decimal(18,2) NOT NULL,
                "EsExento" boolean NOT NULL,
                CONSTRAINT "PK_PurchaseItems" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_PurchaseItems_Purchases_PurchaseId" FOREIGN KEY ("PurchaseId") REFERENCES "{schemaName}"."Purchases" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_Sales_TenantId" ON "{schemaName}"."Sales" ("TenantId");
            CREATE INDEX IF NOT EXISTS "IX_Sales_TenantId_TipoDte_Folio" ON "{schemaName}"."Sales" ("TenantId", "TipoDte", "Folio");
            CREATE INDEX IF NOT EXISTS "IX_SaleItems_SaleId" ON "{schemaName}"."SaleItems" ("SaleId");
            CREATE INDEX IF NOT EXISTS "IX_Purchases_TenantId" ON "{schemaName}"."Purchases" ("TenantId");
            CREATE INDEX IF NOT EXISTS "IX_Purchases_TenantId_TipoDte_Folio" ON "{schemaName}"."Purchases" ("TenantId", "TipoDte", "Folio");
            CREATE INDEX IF NOT EXISTS "IX_PurchaseItems_PurchaseId" ON "{schemaName}"."PurchaseItems" ("PurchaseId");
            """;

        await tenantDb.Database.ExecuteSqlRawAsync(ensureTableSql, cancellationToken);

        if (!await tenantDb.Configurations.AnyAsync(cancellationToken))
        {
            var defaultConfig = TenantConfiguration.Create(
                taxPercentage: 19.00m,
                currencyCode: "CLP",
                currencySymbol: "$",
                timeZoneId: "America/Santiago",
                receiptFooterText: null);

            await tenantDb.Configurations.AddAsync(defaultConfig, cancellationToken);
            await tenantDb.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Configuración por defecto (IVA 19%, Moneda CLP, TimeZone America/Santiago) insertada en el esquema '{SchemaName}'.",
                schemaName);
        }
    }
}
