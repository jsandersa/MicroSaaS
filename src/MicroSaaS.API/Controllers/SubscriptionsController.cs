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
        // 1. IGNORAR el ID del body por seguridad y extraerlo directamente del Token JWT
        var tenantIdClaim = User.FindFirstValue(JwtProvider.TenantIdClaimName);

        if (!Guid.TryParse(tenantIdClaim, out var tenantId) || tenantId == Guid.Empty)
        {
            return Unauthorized(new
            {
                error = "Unauthorized",
                message = "No se pudo identificar el Tenant desde el token de sesión."
            });
        }

        // 2. Despachar a MediatR con el TenantId 100% verificado y seguro
        var nextDueDate = await _mediator.Send(new PaySubscriptionCommand(tenantId), cancellationToken);

        return Ok(new
        {
            message = "Suscripción reactivada exitosamente por 30 días.",
            nextDueDate
        });
    }
}