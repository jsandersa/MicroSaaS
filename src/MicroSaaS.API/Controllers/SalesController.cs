using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroSaaS.Application.Sales.Commands.RegisterSale;
using MicroSaaS.Application.Sales.Queries.GetSales;

namespace MicroSaaS.API.Controllers;

[ApiController]
[Route("api/sales")]
[Authorize]
public class SalesController : ControllerBase
{
    private readonly IMediator _mediator;

    public SalesController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SaleSummaryDto>>> GetSales(CancellationToken cancellationToken)
    {
        var sales = await _mediator.Send(new GetSalesQuery(), cancellationToken);
        return Ok(sales);
    }

    [HttpPost]
    public async Task<IActionResult> RegisterSale(
        [FromBody] RegisterSaleCommand command,
        CancellationToken cancellationToken)
    {
        var saleId = await _mediator.Send(command, cancellationToken);
        return Ok(new { Id = saleId });
    }
}

