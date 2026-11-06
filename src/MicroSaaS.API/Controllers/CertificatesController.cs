using System.Xml.Linq;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroSaaS.Application.Certificates.Commands.UploadCertificate;
using MicroSaaS.Core.Entities.TenantDomain;
using MicroSaaS.Core.Enums;
using MicroSaaS.Core.Interfaces;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.API.Controllers;

public record RegisterCafRequest(
    int DteType,
    int StartFolio,
    int EndFolio,
    string? XmlContent = null);

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CertificatesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly TenantDbContext _tenantDbContext;
    private readonly ICurrentUserService _currentUserService;

    public CertificatesController(
        IMediator mediator,
        TenantDbContext tenantDbContext,
        ICurrentUserService currentUserService)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _tenantDbContext = tenantDbContext ?? throw new ArgumentNullException(nameof(tenantDbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetCertificateStatus(CancellationToken cancellationToken)
    {
        var activeCert = await _tenantDbContext.DigitalCertificates
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.ExpirationDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeCert is null)
        {
            return Ok(new
            {
                HasActiveCertificate = false,
                FileName = (string?)null,
                Subject = (string?)null,
                ExpirationDate = (DateTime?)null,
                IsExpired = false,
                DaysRemaining = 0
            });
        }

        var now = DateTime.UtcNow;
        var daysRemaining = (int)Math.Ceiling((activeCert.ExpirationDate - now).TotalDays);

        return Ok(new
        {
            HasActiveCertificate = true,
            FileName = activeCert.FileName,
            Subject = activeCert.Subject,
            ExpirationDate = activeCert.ExpirationDate,
            IsExpired = activeCert.ExpirationDate <= now,
            DaysRemaining = Math.Max(0, daysRemaining)
        });
    }

    [HttpGet("caf")]
    public async Task<IActionResult> GetCafAuthorizations(CancellationToken cancellationToken)
    {
        var cafList = await _tenantDbContext.CafAuthorizations
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.DteType)
            .ToListAsync(cancellationToken);

        var maxFoliosByType = await _tenantDbContext.DteDocuments
            .AsNoTracking()
            .GroupBy(d => (int)d.DteType)
            .Select(g => new { DteType = g.Key, MaxFolio = g.Max(d => d.Folio) })
            .ToDictionaryAsync(x => x.DteType, x => x.MaxFolio, cancellationToken);

        var response = cafList.Select(caf =>
        {
            maxFoliosByType.TryGetValue(caf.DteType, out var maxDteFolio);
            var effectiveLastFolio = Math.Max(caf.CurrentFolio, maxDteFolio);
            var totalFolios = Math.Max(1, caf.EndFolio - caf.StartFolio + 1);
            var usedFolios = effectiveLastFolio >= caf.StartFolio
                ? Math.Min(totalFolios, effectiveLastFolio - caf.StartFolio + 1)
                : 0;
            var availableFolios = Math.Max(0, totalFolios - usedFolios);
            var usagePercentage = Math.Round((decimal)usedFolios / totalFolios * 100m, 1);

            return new
            {
                Id = caf.Id,
                DteType = caf.DteType,
                DteTypeName = caf.DteType switch
                {
                    33 => "Factura Electrónica (33)",
                    39 => "Boleta Electrónica (39)",
                    61 => "Nota de Crédito Electrónica (61)",
                    _ => $"DTE Tipo {caf.DteType}"
                },
                StartFolio = caf.StartFolio,
                EndFolio = caf.EndFolio,
                CurrentFolio = effectiveLastFolio >= caf.StartFolio ? effectiveLastFolio : 0,
                TotalFolios = totalFolios,
                UsedFolios = usedFolios,
                AvailableFolios = availableFolios,
                UsagePercentage = usagePercentage,
                AuthorizedAt = caf.AuthorizedAt,
                ExpiresAt = caf.ExpiresAt,
                IsActive = caf.IsActive
            };
        });

        return Ok(response);
    }

    [HttpPost("caf")]
    public async Task<IActionResult> RegisterCaf(
        [FromBody] RegisterCafRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { message = "Solicitud CAF inválida." });

        var dteType = request.DteType;
        var startFolio = request.StartFolio;
        var endFolio = request.EndFolio;
        var xmlContent = request.XmlContent ?? string.Empty;

        // Si el usuario adjuntó un XML CAF oficial del SII, intentar extraer TD, D y H automáticamente
        if (!string.IsNullOrWhiteSpace(xmlContent))
        {
            try
            {
                var doc = XDocument.Parse(xmlContent);
                var tdNode = doc.Descendants("TD").FirstOrDefault()?.Value;
                var dNode = doc.Descendants("D").FirstOrDefault()?.Value;
                var hNode = doc.Descendants("H").FirstOrDefault()?.Value;

                if (int.TryParse(tdNode, out var parsedTd)) dteType = parsedTd;
                if (int.TryParse(dNode, out var parsedD)) startFolio = parsedD;
                if (int.TryParse(hNode, out var parsedH)) endFolio = parsedH;
            }
            catch
            {
                // Si el XML es personalizado o fragmentado, se conservan los parámetros del formulario
            }
        }

        if (startFolio <= 0 || endFolio < startFolio)
        {
            return BadRequest(new { message = "El rango de folios (Desde / Hasta) no es válido." });
        }

        var tenantId = _tenantDbContext.CurrentTenantId ?? _currentUserService.TenantId;
        if (tenantId == Guid.Empty)
        {
            return Unauthorized(new { message = "No se pudo resolver el Tenant actual." });
        }

        // Desactivar CAF anterior del mismo tipo de DTE
        var previousCafs = await _tenantDbContext.CafAuthorizations
            .Where(c => c.IsActive && c.DteType == dteType)
            .ToListAsync(cancellationToken);

        foreach (var prev in previousCafs)
        {
            prev.Deactivate();
        }

        var maxEmitted = await _tenantDbContext.DteDocuments
            .AsNoTracking()
            .Where(d => (int)d.DteType == dteType)
            .Select(d => (int?)d.Folio)
            .MaxAsync(cancellationToken) ?? 0;

        var nowUtc = DateTime.UtcNow;
        var newCaf = new CafAuthorization(
            tenantId: tenantId,
            dteType: dteType,
            startFolio: startFolio,
            endFolio: endFolio,
            currentFolio: Math.Max(startFolio - 1, maxEmitted),
            xmlContent: xmlContent,
            authorizedAt: nowUtc,
            expiresAt: nowUtc.AddMonths(6));

        await _tenantDbContext.CafAuthorizations.AddAsync(newCaf, cancellationToken);
        await _tenantDbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { Id = newCaf.Id, Message = "Rango de Folios CAF registrado y activado exitosamente." });
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
