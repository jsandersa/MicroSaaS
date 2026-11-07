using MediatR;
using MicroSaaS.Application.Common.Security;
using MicroSaaS.Core.Enums;

namespace MicroSaaS.Application.Inventory.Commands.RegisterInventoryMovement;

/// <summary>
/// Comando CQRS para registrar un movimiento de inventario (entrada, salida o ajuste),
/// recalcular el Precio Medio Ponderado (PMP) del producto y generar su asiento inmutable en el Kardex.
/// </summary>
[AuthorizeRole("Owner", "Admin", "Cashier")]
public sealed record RegisterInventoryMovementCommand(
    Guid ProductId,
    MovementType MovementType,
    int Quantity,
    decimal UnitCost) : IRequest<Guid>;

