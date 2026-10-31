using MediatR;
using Microsoft.AspNetCore.Mvc;
using MicroSaaS.Application.Tenants.Commands.RegisterTenant;

namespace MicroSaaS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TenantController : ControllerBase
{
    private readonly IMediator _mediator;

    public TenantController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterTenantCommand command)
    {
        // MediatR toma el comando y ejecuta tu RegisterTenantCommandHandler automáticamente
        var tenantId = await _mediator.Send(command);

        return Ok(new
        {
            Message = "Pyme registrada y esquema de BD generado exitosamente",
            TenantId = tenantId
        });
    }
}