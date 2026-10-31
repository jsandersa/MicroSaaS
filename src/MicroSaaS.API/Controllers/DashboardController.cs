using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroSaaS.Application.Dashboard.Queries.GetDashboardMetrics;

namespace MicroSaaS.API.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IMediator _mediator;

    public DashboardController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    [HttpGet]
    public async Task<ActionResult<DashboardResultDto>> GetMetrics(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetDashboardMetricsQuery(), cancellationToken);
        return Ok(result);
    }
}

