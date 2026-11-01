using MediatR;

namespace MicroSaaS.Application.Tenants.Commands.RegisterTenant;

/// <summary>
/// Comando para el Onboarding (registro) de un nuevo Tenant, su usuario administrador y aprovisionamiento de su esquema privado.
/// </summary>
public record RegisterTenantCommand(
    string Rut,
    string LegalName,
    string BusinessLine,
    string Address,
    string AdminEmail,
    string AdminPassword) : IRequest<string>;
