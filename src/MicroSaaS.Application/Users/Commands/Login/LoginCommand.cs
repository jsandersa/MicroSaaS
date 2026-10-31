using MediatR;

namespace MicroSaaS.Application.Users.Commands.Login;

/// <summary>
/// Comando para autenticar a un usuario y obtener su token JWT con el esquema de su Tenant.
/// </summary>
public record LoginCommand(
    string Email,
    string Password) : IRequest<string>;

