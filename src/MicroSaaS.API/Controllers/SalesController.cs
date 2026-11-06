using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroSaaS.Application.Sales.Commands.RegisterSale;
using MicroSaaS.Application.Sales.Queries.GetSales;
using MicroSaaS.Core.Enums;
using MicroSaaS.Core.Interfaces;

namespace MicroSaaS.API.Controllers;

[ApiController]
[Route("api/sales")]
[Authorize]
public class SalesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IDteRepository _dteRepository;
    private readonly ICurrentUserService _currentUserService;

    public SalesController(
        IMediator mediator,
        IDteRepository dteRepository,
        ICurrentUserService currentUserService)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _dteRepository = dteRepository ?? throw new ArgumentNullException(nameof(dteRepository));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SaleSummaryDto>>> GetSales(CancellationToken cancellationToken)
    {
        var sales = await _mediator.Send(new GetSalesQuery(), cancellationToken);
        return Ok(sales);
    }

    [HttpGet("dtes")]
    public async Task<IActionResult> GetEmittedDtes(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId;
        if (tenantId == Guid.Empty)
        {
            return Unauthorized();
        }

        var dtes = await _dteRepository.GetByTenantAsync(tenantId, cancellationToken);

        var response = dtes.Select(d => new
        {
            Id = d.Id,
            SaleId = d.SaleId,
            DteType = (int)d.DteType,
            DteTypeName = d.DteType switch
            {
                DteType.FacturaElectronica => "Factura Electrónica (33)",
                DteType.BoletaElectronica => "Boleta Electrónica (39)",
                DteType.NotaCredito => "Nota de Crédito (61)",
                _ => $"DTE ({(int)d.DteType})"
            },
            Folio = d.Folio,
            TotalAmount = d.TotalAmount,
            Status = (int)d.Status,
            StatusName = d.Status.ToString(),
            EmissionDate = d.EmissionDate,
            XmlContent = d.XmlContent
        });

        return Ok(response);
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
