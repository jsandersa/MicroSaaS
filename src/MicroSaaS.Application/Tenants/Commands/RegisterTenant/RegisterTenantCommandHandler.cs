using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using MicroSaaS.Core.Entities;
using MicroSaaS.Core.Entities.TenantDomain;
using MicroSaaS.Core.Interfaces;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.Application.Tenants.Commands.RegisterTenant;

/// <summary>
/// Manejador del comando <see cref="RegisterTenantCommand"/> encargado de dar de alta un nuevo Tenant,
/// su suscripción inicial, su usuario administrador y aprovisionar físicamente su esquema privado en PostgreSQL.
/// </summary>
public class RegisterTenantCommandHandler : IRequestHandler<RegisterTenantCommand, Guid>
{
    private const decimal BaseMonthlyFee = 10000m;

    private readonly MasterDbContext _masterDbContext;
    private readonly TenantDbContext _tenantDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<RegisterTenantCommandHandler> _logger;

    public RegisterTenantCommandHandler(
        MasterDbContext masterDbContext,
        TenantDbContext tenantDbContext,
        ITenantProvider tenantProvider,
        ILogger<RegisterTenantCommandHandler> logger)
    {
        _masterDbContext = masterDbContext ?? throw new ArgumentNullException(nameof(masterDbContext));
        _tenantDbContext = tenantDbContext ?? throw new ArgumentNullException(nameof(tenantDbContext));
        _tenantProvider = tenantProvider ?? throw new ArgumentNullException(nameof(tenantProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> Handle(RegisterTenantCommand request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Rut);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.LegalName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.BusinessLine);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Address);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.AdminEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.AdminPassword);

        var normalizedRut = request.Rut.Trim();
        var normalizedEmail = request.AdminEmail.Trim().ToLowerInvariant();

        // 1. Validar que el RUT y el correo del administrador no existan previamente en la base de datos maestra
        var rutExists = await _masterDbContext.Tenants
            .AnyAsync(t => t.Rut == normalizedRut, cancellationToken);

        if (rutExists)
        {
            throw new InvalidOperationException($"Ya existe un cliente registrado con el RUT '{normalizedRut}'.");
        }

        var emailExists = await _masterDbContext.Users
            .AnyAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (emailExists)
        {
            throw new InvalidOperationException($"Ya existe un usuario registrado con el correo '{normalizedEmail}'.");
        }

        // 2. Generar un SchemaName seguro para PostgreSQL (RUT sin puntos ni guión, en minúsculas y prefijado con "t_")
        var cleanRut = normalizedRut
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        var schemaName = $"t_{cleanRut}";

        // 3. Instanciar la entidad Tenant, su Subscription inicial y el User administrador con contraseña hasheada
        var tenant = Tenant.Create(
            rut: normalizedRut,
            legalName: request.LegalName,
            businessLine: request.BusinessLine,
            address: request.Address,
            schemaName: schemaName);

        var startDate = DateTime.UtcNow;
        var nextDueDate = startDate.AddMonths(1);

        var subscription = Subscription.Create(
            tenantId: tenant.Id,
            monthlyFee: BaseMonthlyFee,
            startDate: startDate,
            nextDueDate: nextDueDate);

        var passwordHash = HashPassword(request.AdminPassword);
        var adminUser = User.Create(
            tenantId: tenant.Id,
            email: normalizedEmail,
            passwordHash: passwordHash);

        var previousSchema = _tenantProvider.GetCurrentSchema();
        await using var masterTransaction = await _masterDbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // 4. Persistir Tenant, Subscription y User administrador en el esquema maestro ("public")
            await _masterDbContext.Tenants.AddAsync(tenant, cancellationToken);
            await _masterDbContext.Subscriptions.AddAsync(subscription, cancellationToken);
            await _masterDbContext.Users.AddAsync(adminUser, cancellationToken);

            await _masterDbContext.SaveChangesAsync(cancellationToken);

            // 5. Cambiar el contexto actual al esquema recién generado para aprovisionar su base de datos privada
            _tenantProvider.SetCurrentSchema(tenant.SchemaName);

            // 6. Crear físicamente el esquema privado en PostgreSQL
            var createSchemaSql = $"CREATE SCHEMA IF NOT EXISTS \"{tenant.SchemaName}\"";
            await _tenantDbContext.Database.ExecuteSqlRawAsync(createSchemaSql, cancellationToken);

            // 7. Forzar la creación de las tablas del dominio privado dentro del nuevo esquema
            var created = await _tenantDbContext.Database.EnsureCreatedAsync(cancellationToken);
            if (!created)
            {
                // Si la BD ya contiene tablas (esquema public), forzamos la creación de las tablas del modelo TenantDbContext en el search_path actual
                var databaseCreator = _tenantDbContext.GetService<IRelationalDatabaseCreator>();
                await databaseCreator.CreateTablesAsync(cancellationToken);
            }

            // 8. Insertar automáticamente los parámetros de configuración por defecto para el nuevo Tenant (IVA 19%, CLP, America/Santiago)
            if (!await _tenantDbContext.Configurations.AnyAsync(cancellationToken))
            {
                var defaultConfig = TenantConfiguration.CreateDefault();
                await _tenantDbContext.Configurations.AddAsync(defaultConfig, cancellationToken);
                await _tenantDbContext.SaveChangesAsync(cancellationToken);
            }

            await masterTransaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Tenant '{LegalName}' ({Rut}) y usuario administrador '{AdminEmail}' registrados con éxito. Esquema aprovisionado: '{SchemaName}'.",
                tenant.LegalName,
                tenant.Rut,
                adminUser.Email,
                tenant.SchemaName);

            return tenant.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error al aprovisionar el esquema '{SchemaName}' para el Tenant con RUT '{Rut}'. Ejecutando rollback.",
                tenant.SchemaName,
                normalizedRut);

            await masterTransaction.RollbackAsync(cancellationToken);
            _tenantProvider.SetCurrentSchema(previousSchema);

            throw;
        }
    }

    public static string HashPassword(string password)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
    }
}
