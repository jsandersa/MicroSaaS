using MediatR;
using MicroSaaS.Application.Common.Security;
using MicroSaaS.Core.Enums;

namespace MicroSaaS.Application.CashRegister.Commands.CloseCashRegister;

/// <summary>
/// DTO con el resumen completo del arqueo y cuadratura al cerrar una sesión de caja.
/// </summary>
public sealed record CloseCashRegisterResultDto(
    Guid SessionId,
    CashRegisterStatus Status,
    decimal OpeningAmount,
    decimal ExpectedCashAmount,
    decimal ActualCashAmount,
    decimal CashDifference,
    decimal ExpectedCardAmount,
    decimal ActualCardAmount,
    decimal CardDifference,
    decimal ExpectedClosingAmount,
    decimal ActualClosingAmount,
    decimal TotalDifference,
    DateTime ClosedAt);

/// <summary>
/// Comando CQRS para ejecutar el arqueo, cuadratura y cierre de la sesión de caja abierta del operador actual.
/// </summary>
[AuthorizeRole("Owner", "Admin")]
public sealed record CloseCashRegisterCommand(
    decimal ActualCashAmount,
    decimal ActualCardAmount) : IRequest<CloseCashRegisterResultDto>;

