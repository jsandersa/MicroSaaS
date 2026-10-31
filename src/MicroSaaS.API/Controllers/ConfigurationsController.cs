using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroSaaS.Application.Configurations.Commands.UpdateTenantConfiguration;
using MicroSaaS.Application.Configurations.Queries.GetTenantConfiguration;

namespace MicroSaaS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConfigurationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ConfigurationsController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    [HttpGet]
    public async Task<ActionResult<TenantConfigurationDto>> Get(CancellationToken cancellationToken)
    {
        var config = await _mediator.Send(new GetTenantConfigurationQuery(), cancellationToken);
        if (config is null)
        {
            return NotFound();
        }

        return Ok(config);
    }

    [HttpPut]
    public async Task<ActionResult<TenantConfigurationDto>> Update(
        [FromBody] UpdateTenantConfigurationCommand command,
        CancellationToken cancellationToken)
    {
        var updated = await _mediator.Send(command, cancellationToken);
        return Ok(updated);
    }
}

