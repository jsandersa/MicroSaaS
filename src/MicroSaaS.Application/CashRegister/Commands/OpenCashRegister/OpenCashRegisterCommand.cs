using MediatR;
using MicroSaaS.Application.Common.Security;

namespace MicroSaaS.Application.CashRegister.Commands.OpenCashRegister;

/// <summary>
/// Comando CQRS para abrir una nueva sesión diaria de caja registradora en el esquema del Tenant actual.
/// Restringido mediante <see cref="AuthorizeRoleAttribute"/> a los roles <c>Owner</c> y <c>Admin</c>.
/// </summary>
[AuthorizeRole("Owner", "Admin", "Cashier")]
public sealed record OpenCashRegisterCommand(decimal OpeningAmount) : IRequest<Guid>;

