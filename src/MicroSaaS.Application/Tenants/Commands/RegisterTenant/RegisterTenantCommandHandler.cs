using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MicroSaaS.Core.Entities;
using MicroSaaS.Core.Interfaces;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.Application.Tenants.Commands.RegisterTenant;

/// <summary>
/// Manejador del comando <see cref="RegisterTenantCommand"/> encargado de dar de alta un nuevo Tenant en la BD maestra
/// e invocar a <see cref="ITenantProvisioner"/> para crear y migrar dinámicamente su esquema privado en PostgreSQL.
/// </summary>
public class RegisterTenantCommandHandler : IRequestHandler<RegisterTenantCommand, string>
{
    private const decimal BaseMonthlyFee = 10000m;

    private readonly MasterDbContext _masterDbContext;
    private readonly ITenantProvisioner _tenantProvisioner;
    private readonly ILogger<RegisterTenantCommandHandler> _logger;

    public RegisterTenantCommandHandler(
        MasterDbContext masterDbContext,
        ITenantProvisioner tenantProvisioner,
        ILogger<RegisterTenantCommandHandler> logger)
    {
        _masterDbContext = masterDbContext ?? throw new ArgumentNullException(nameof(masterDbContext));
        _tenantProvisioner = tenantProvisioner ?? throw new ArgumentNullException(nameof(tenantProvisioner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> Handle(RegisterTenantCommand request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Rut);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.LegalName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.BusinessLine);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Address);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.AdminEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.AdminPassword);

        var normalizedRut = request.Rut.Trim();
        var normalizedEmail = request.AdminEmail.Trim().ToLowerInvariant();

        // 1. Validar que el RUT (y el correo del administrador) no existan previamente en la base de datos maestra
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

        // 2. Generar el nombre de esquema aislado para PostgreSQL (ej. "t_" + RUT sin puntos ni guion)
        var cleanRut = normalizedRut
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        var schemaName = $"t_{cleanRut}";

        // 3. Crear la entidad Tenant (IsActive = true), su Subscription activa y su User administrador
        var tenant = Tenant.Create(
            rut: normalizedRut,
            legalName: request.LegalName,
            businessLine: request.BusinessLine,
            address: request.Address,
            schemaName: schemaName);

        tenant.Activate();

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

        // 4. Guardar el nuevo Tenant, su suscripción y su administrador en la base de datos maestra
        await _masterDbContext.Tenants.AddAsync(tenant, cancellationToken);
        await _masterDbContext.Subscriptions.AddAsync(subscription, cancellationToken);
        await _masterDbContext.Users.AddAsync(adminUser, cancellationToken);

        await _masterDbContext.SaveChangesAsync(cancellationToken);

        // 5. Aprovisionar dinámicamente el esquema del nuevo Tenant y aplicar las migraciones de EF Core
        await _tenantProvisioner.ProvisionTenantAsync(tenant.SchemaName, cancellationToken);

        _logger.LogInformation(
            "Tenant '{LegalName}' ({Rut}) registrado con éxito. TenantId: '{TenantId}', Esquema: '{SchemaName}'.",
            tenant.LegalName,
            tenant.Rut,
            tenant.Id,
            tenant.SchemaName);

        return tenant.Id.ToString();
    }

    public static string HashPassword(string password)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
    }
}
