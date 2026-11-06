using MediatR;
using MicroSaaS.Application.Common.Security;
using MicroSaaS.Core.Enums;

namespace MicroSaaS.Application.ElectronicInvoicing.Commands.EmitDte;

/// <summary>
/// Comando CQRS para asignar folio correlativo, firmar y emitir un Documento Tributario Electrónico (DTE)
/// asociado a una venta del Tenant actual.
/// </summary>
[AuthorizeRole("Owner", "Admin")]
public sealed record EmitDteCommand(
    Guid SaleId,
    DteType DteType,
    decimal TotalAmount) : IRequest<Guid>;

