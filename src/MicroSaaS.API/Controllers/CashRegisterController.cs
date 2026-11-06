using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroSaaS.Application.CashRegister.Commands.CloseCashRegister;
using MicroSaaS.Application.CashRegister.Commands.OpenCashRegister;
using MicroSaaS.Core.Interfaces;

namespace MicroSaaS.API.Controllers;

[ApiController]
[Route("api/cash-register")]
[Authorize]
public sealed class CashRegisterController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICashRegisterRepository _cashRegisterRepository;
    private readonly ICurrentUserService _currentUserService;

    public CashRegisterController(
        IMediator mediator,
        ICashRegisterRepository cashRegisterRepository,
        ICurrentUserService currentUserService)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _cashRegisterRepository = cashRegisterRepository ?? throw new ArgumentNullException(nameof(cashRegisterRepository));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrentSession(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId;
        var userId = _currentUserService.UserId;

        if (tenantId == Guid.Empty || userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var session = await _cashRegisterRepository.GetOpenSessionByUserAsync(
            tenantId: tenantId,
            userId: userId,
            cancellationToken: cancellationToken);

        if (session is null)
        {
            return Ok(new { isOpen = false, session = (object?)null });
        }

        return Ok(new
        {
            isOpen = true,
            session = new
            {
                id = session.Id,
                openingAmount = session.OpeningAmount,
                expectedClosingAmount = session.ExpectedClosingAmount,
                openedAt = session.OpenedAt,
                status = session.Status.ToString()
            }
        });
    }

    [HttpPost("open")]
    public async Task<IActionResult> Open(
        [FromBody] OpenCashRegisterCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var sessionId = await _mediator.Send(command, cancellationToken);
            return Ok(new { id = sessionId });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("close")]
    public async Task<ActionResult<CloseCashRegisterResultDto>> Close(
        [FromBody] CloseCashRegisterCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
