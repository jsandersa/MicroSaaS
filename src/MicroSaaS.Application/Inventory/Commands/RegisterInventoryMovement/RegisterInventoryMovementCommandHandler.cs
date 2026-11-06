using MediatR;
using MicroSaaS.Core.Entities.TenantDomain;
using MicroSaaS.Core.Enums;
using MicroSaaS.Core.Interfaces;

namespace MicroSaaS.Application.Inventory.Commands.RegisterInventoryMovement;

/// <summary>
/// Manejador de <see cref="RegisterInventoryMovementCommand"/> que actualiza el stock y el PMP del producto,
/// persiste el movimiento inmutable en el libro Kardex del Tenant y notifica en tiempo real vía SignalR.
/// </summary>
public sealed class RegisterInventoryMovementCommandHandler : IRequestHandler<RegisterInventoryMovementCommand, Guid>
{
    private readonly IProductRepository _productRepository;
    private readonly IKardexRepository _kardexRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IInventoryNotifier _inventoryNotifier;

    public RegisterInventoryMovementCommandHandler(
        IProductRepository productRepository,
        IKardexRepository kardexRepository,
        ICurrentUserService currentUserService,
        IInventoryNotifier inventoryNotifier)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _kardexRepository = kardexRepository ?? throw new ArgumentNullException(nameof(kardexRepository));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _inventoryNotifier = inventoryNotifier ?? throw new ArgumentNullException(nameof(inventoryNotifier));
    }

    public async Task<Guid> Handle(
        RegisterInventoryMovementCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // a. Obtener el TenantId actual desde ICurrentUserService
        var tenantId = _currentUserService.TenantId;
        if (tenantId == Guid.Empty)
        {
            throw new UnauthorizedAccessException(
                "No se pudo determinar el TenantId del usuario autenticado desde el token JWT.");
        }

        // b. Obtener el producto por Id y TenantId
        var product = await _productRepository.GetByIdAsync(
            id: request.ProductId,
            tenantId: tenantId,
            cancellationToken: cancellationToken);

        if (product is null)
        {
            throw new KeyNotFoundException(
                $"No se encontró el producto '{request.ProductId}' dentro del inventario del Tenant '{tenantId}'.");
        }

        // c. Según el MovementType, invocar RegisterStockIn o RegisterStockOut en la entidad Product
        decimal effectiveUnitCost;

        switch (request.MovementType)
        {
            case MovementType.In:
                product.RegisterStockIn(request.Quantity, request.UnitCost);
                effectiveUnitCost = request.UnitCost;
                break;

            case MovementType.Out:
                product.RegisterStockOut(request.Quantity);
                effectiveUnitCost = product.PmpCost > 0m ? product.PmpCost : request.UnitCost;
                break;

            case MovementType.Adjustment:
                if (request.Quantity >= 0)
                {
                    var adjustmentCost = request.UnitCost > 0m ? request.UnitCost : product.PmpCost;
                    product.RegisterStockIn(request.Quantity, adjustmentCost);
                    effectiveUnitCost = adjustmentCost;
                }
                else
                {
                    product.RegisterStockOut(Math.Abs(request.Quantity));
                    effectiveUnitCost = product.PmpCost > 0m ? product.PmpCost : request.UnitCost;
                }
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(request.MovementType),
                    request.MovementType,
                    $"Tipo de movimiento de inventario no soportado: '{request.MovementType}'.");
        }

        // d. Instanciar una nueva KardexTransaction
        var kardexTransaction = KardexTransaction.Create(
            tenantId: tenantId,
            productId: product.Id,
            movementType: request.MovementType,
            quantity: Math.Abs(request.Quantity),
            unitCost: effectiveUnitCost);

        // e. Persistir ambos cambios en los repositorios y notificar a los clientes del Tenant por SignalR
        await _productRepository.UpdateAsync(product, cancellationToken);
        await _kardexRepository.AddAsync(kardexTransaction, cancellationToken);

        await _inventoryNotifier.NotifyStockUpdateAsync(
            product.Id,
            product.CurrentStock,
            cancellationToken);

        return kardexTransaction.Id;
    }
}
