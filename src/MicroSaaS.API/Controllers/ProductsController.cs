using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroSaaS.Application.Products.Commands.CreateProduct;
using MicroSaaS.Application.Products.Commands.DeleteProduct;
using MicroSaaS.Application.Products.Commands.UpdateProduct;
using MicroSaaS.Application.Products.Queries.GetProductById;
using MicroSaaS.Application.Products.Queries.GetProducts;
using MicroSaaS.Core.Enums;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.API.Controllers;

[ApiController]
[Route("api/products")]
[Route("api/inventory")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly TenantDbContext _context;

    public ProductsController(IMediator mediator, TenantDbContext context)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetProducts(CancellationToken cancellationToken)
    {
        var products = await _mediator.Send(new GetProductsQuery(), cancellationToken);
        return Ok(products);
    }

    [HttpGet("kardex")]
    public async Task<IActionResult> GetKardex(CancellationToken cancellationToken)
    {
        var productsMap = await _context.Products
            .AsNoTracking()
            .ToDictionaryAsync(
                p => p.Id,
                p => new { p.Sku, p.Name, PmpCost = p.PmpCost > 0m ? p.PmpCost : p.CostPrice },
                cancellationToken);

        var transactions = await _context.KardexTransactions
            .AsNoTracking()
            .OrderByDescending(k => k.TransactionDate)
            .Take(200)
            .ToListAsync(cancellationToken);

        var result = transactions.Select(k =>
        {
            productsMap.TryGetValue(k.ProductId, out var prod);
            return new
            {
                Id = k.Id,
                ProductId = k.ProductId,
                ProductSku = prod?.Sku ?? "N/A",
                ProductName = prod?.Name ?? "Producto Eliminado",
                CurrentPmpCost = prod?.PmpCost ?? k.UnitCost,
                MovementType = (int)k.MovementType,
                MovementTypeName = k.MovementType switch
                {
                    MovementType.In => "Entrada (Compra / Ingreso)",
                    MovementType.Out => "Salida (Venta POS)",
                    MovementType.Adjustment => "Ajuste de Inventario",
                    _ => k.MovementType.ToString()
                },
                Quantity = k.Quantity,
                UnitCost = k.UnitCost,
                TotalValue = k.TotalValue,
                TransactionDate = k.TransactionDate,
                Reason = !string.IsNullOrWhiteSpace(k.Reason)
                    ? k.Reason
                    : k.MovementType switch
                    {
                        MovementType.In => "Ingreso por Compra / Stock Inicial",
                        MovementType.Out => "Salida por Venta POS",
                        MovementType.Adjustment => "Ajuste de Inventario",
                        _ => "-"
                    }
            };
        });

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var product = await _mediator.Send(new GetProductByIdQuery(id), cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        return Ok(product);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateProductCommand command,
        CancellationToken cancellationToken)
    {
        var productId = await _mediator.Send(command, cancellationToken);
        return Ok(new { Id = productId });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateProductCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var effectiveCommand = command with { Id = id };
            var updated = await _mediator.Send(effectiveCommand, cancellationToken);

            if (!updated)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var deleted = await _mediator.Send(new DeleteProductCommand(id), cancellationToken);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }
}
