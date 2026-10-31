using MediatR;

namespace MicroSaaS.Application.Tenants.Commands.RegisterTenant;

/// <summary>
/// Comando para registrar un nuevo cliente (Pyme/Tenant), su usuario administrador y su esquema privado.
/// </summary>
public record RegisterTenantCommand(
    string Rut,
    string LegalName,
    string BusinessLine,
    string Address,
    string AdminEmail,
    string AdminPassword) : IRequest<Guid>;
