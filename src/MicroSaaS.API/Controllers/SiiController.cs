using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroSaaS.Application.Sii.Commands.EmitDte;
using MicroSaaS.Application.Sii.Queries.TestSiiConnection;

namespace MicroSaaS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SiiController : ControllerBase
{
    private readonly IMediator _mediator;

    public SiiController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    [HttpGet("test-connection")]
    public async Task<ActionResult<SiiConnectionResultDto>> TestConnection(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new TestSiiConnectionQuery(), cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("emit-invoice")]
    public async Task<ActionResult<EmitDteResultDto>> EmitInvoice(
        [FromBody] EmitDteCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}

