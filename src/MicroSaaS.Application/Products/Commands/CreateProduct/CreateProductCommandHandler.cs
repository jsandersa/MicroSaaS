using MediatR;
using MicroSaaS.Core.Entities.TenantDomain;
using MicroSaaS.Core.Enums;
using MicroSaaS.Core.Interfaces;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.Application.Products.Commands.CreateProduct;

/// <summary>
/// Manejador del comando <see cref="CreateProductCommand"/> que instancia la entidad <see cref="Product"/>,
/// le asigna el <c>TenantId</c> del contexto actual, registra el movimiento inicial en el libro Kardex si posee stock inicial,
/// y persiste en el esquema del Tenant autenticado.
/// </summary>
public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Guid>
{
    private readonly TenantDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CreateProductCommandHandler(
        TenantDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<Guid> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var resolvedTenantGuid = _context.CurrentTenantId ?? _currentUserService.TenantId;
        var tenantId = resolvedTenantGuid != Guid.Empty
            ? resolvedTenantGuid.ToString()
            : (_context.CurrentTenantIdentifier ?? string.Empty);

        var product = new Product(
            tenantId: tenantId,
            sku: request.Sku,
            name: request.Name,
            salePrice: request.SalePrice,
            costPrice: request.CostPrice,
            currentStock: request.CurrentStock);

        await _context.Products.AddAsync(product, cancellationToken);

        if (request.CurrentStock > 0 && resolvedTenantGuid != Guid.Empty)
        {
            var initialKardex = KardexTransaction.Create(
                tenantId: resolvedTenantGuid,
                productId: product.Id,
                movementType: MovementType.In,
                quantity: request.CurrentStock,
                unitCost: request.CostPrice);

            await _context.KardexTransactions.AddAsync(initialKardex, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return product.Id;
    }
}
