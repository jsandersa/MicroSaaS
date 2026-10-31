using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroSaaS.Application.Subscriptions.Commands.PaySubscription;
using MicroSaaS.Infrastructure.Authentication;

namespace MicroSaaS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SubscriptionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SubscriptionsController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    [HttpPost("pay")]
    public async Task<IActionResult> Pay(
        [FromBody] PaySubscriptionCommand? command,
        CancellationToken cancellationToken)
    {
        Guid? tenantId = command?.TenantId;

        if ((!tenantId.HasValue || tenantId.Value == Guid.Empty) &&
            Guid.TryParse(User.FindFirstValue(JwtProvider.TenantIdClaimName), out var claimTenantId))
        {
            tenantId = claimTenantId;
        }

        var nextDueDate = await _mediator.Send(new PaySubscriptionCommand(tenantId), cancellationToken);

        return Ok(new
        {
            message = "Suscripción reactivada exitosamente por 30 días.",
            nextDueDate
        });
    }
}

