using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MicroSaaS.Application.Certificates.Commands.UploadCertificate;

namespace MicroSaaS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CertificatesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CertificatesController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    [HttpPost("upload")]
    public async Task<ActionResult<Guid>> Upload(
        [FromForm] IFormFile file,
        [FromForm] string password,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new UploadCertificateCommand(file, password);
            var certificateId = await _mediator.Send(command, cancellationToken);

            return Ok(certificateId);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
