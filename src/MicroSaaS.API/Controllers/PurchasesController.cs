using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroSaaS.Application.Purchases.Commands.RegisterPurchase;
using MicroSaaS.Application.Purchases.Queries.GetPurchaseById;
using MicroSaaS.Application.Purchases.Queries.GetPurchases;

namespace MicroSaaS.API.Controllers;

[ApiController]
[Route("api/purchases")]
[Authorize]
public class PurchasesController : ControllerBase
{
    private readonly IMediator _mediator;

    public PurchasesController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PurchaseSummaryDto>>> GetPurchases(CancellationToken cancellationToken)
    {
        var purchases = await _mediator.Send(new GetPurchasesQuery(), cancellationToken);
        return Ok(purchases);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PurchaseDetailDto>> GetPurchaseById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var purchase = await _mediator.Send(new GetPurchaseByIdQuery(id), cancellationToken);
        if (purchase is null)
        {
            return NotFound();
        }

        return Ok(purchase);
    }

    [HttpPost]
    public async Task<IActionResult> RegisterPurchase(
        [FromBody] RegisterPurchaseCommand command,
        CancellationToken cancellationToken)
    {
        var purchaseId = await _mediator.Send(command, cancellationToken);
        return Ok(new { Id = purchaseId });
    }
}

