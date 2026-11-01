using MediatR;
using MicroSaaS.Core.Entities.TenantDomain;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.Application.Sales.Commands.RegisterSale;

/// <summary>
/// Manejador de <see cref="RegisterSaleCommand"/> que persiste una nueva venta y sus líneas de detalle
/// en el esquema aislado del Tenant actual.
/// </summary>
public class RegisterSaleCommandHandler : IRequestHandler<RegisterSaleCommand, Guid>
{
    private readonly TenantDbContext _context;

    public RegisterSaleCommandHandler(TenantDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Guid> Handle(RegisterSaleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RutCliente);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RazonSocialCliente);
        ArgumentNullException.ThrowIfNull(request.Items);

        var fechaEmisionUtc = request.FechaEmision.Kind switch
        {
            DateTimeKind.Utc => request.FechaEmision,
            DateTimeKind.Local => request.FechaEmision.ToUniversalTime(),
            _ => DateTime.SpecifyKind(request.FechaEmision, DateTimeKind.Utc)
        };

        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            TenantId = _context.CurrentTenantId ?? Guid.Empty,
            Folio = request.Folio,
            TipoDte = request.TipoDte,
            FechaEmision = fechaEmisionUtc,
            RutCliente = request.RutCliente.Trim(),
            RazonSocialCliente = request.RazonSocialCliente.Trim(),
            MontoNeto = request.MontoNeto,
            Iva = request.Iva,
            MontoTotal = request.MontoTotal
        };

        var lineNumber = 1;
        foreach (var itemDto in request.Items)
        {
            var saleItem = new SaleItem
            {
                Id = Guid.NewGuid(),
                SaleId = sale.Id,
                NumeroLinea = lineNumber++,
                NombreItem = itemDto.NombreItem.Trim(),
                Cantidad = itemDto.Cantidad,
                PrecioUnitario = itemDto.PrecioUnitario,
                MontoItem = itemDto.MontoItem,
                EsExento = itemDto.EsExento
            };

            sale.AddItem(saleItem, itemDto.Sku);
        }

        await _context.Sales.AddAsync(sale, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return sale.Id;
    }
}

