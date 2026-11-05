using MediatR;
using MicroSaaS.Core.Entities.TenantDomain;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.Application.Products.Commands.CreateProduct;

/// <summary>
/// Manejador del comando <see cref="CreateProductCommand"/> que instancia la entidad <see cref="Product"/>,
/// le asigna el <c>TenantId</c> del contexto actual y la persiste en el esquema del Tenant autenticado.
/// </summary>
public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Guid>
{
    private readonly TenantDbContext _context;

    public CreateProductCommandHandler(TenantDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Guid> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenantId = _context.CurrentTenantIdentifier ?? string.Empty;

        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Sku = request.Sku.Trim().ToUpperInvariant(),
            Name = request.Name.Trim(),
            SalePrice = request.SalePrice,
            CostPrice = request.CostPrice,
            IsActive = true
        };

        // Se invoca el comportamiento de dominio para inicializar el stock respetando el encapsulamiento
        if (request.CurrentStock > 0)
        {
            product.AdjustStock(request.CurrentStock);
        }

        await _context.Products.AddAsync(product, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return product.Id;
    }
}