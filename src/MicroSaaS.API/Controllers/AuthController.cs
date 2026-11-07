using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroSaaS.Application.Users.Commands.Login;
using MicroSaaS.Core.Entities;
using MicroSaaS.Core.Interfaces;
using MicroSaaS.Infrastructure.Persistence;

namespace MicroSaaS.API.Controllers;

public sealed record UpsertAccountantRequest(string Email, string Password);
public sealed record UpsertTenantUserRequest(
    string Email,
    string Password,
    string Role,
    string? CashRegisterId = null,
    string? PaymentDeviceId = null);
public sealed record ResetPasswordRequest(string Email, string NewPassword);
public sealed record TenantUserDto(
    Guid Id,
    string Email,
    string Role,
    string? CashRegisterId,
    string? PaymentDeviceId,
    DateTime CreatedAt);

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly MasterDbContext _masterDb;
    private readonly ICurrentUserService _currentUserService;

    public AuthController(
        IMediator mediator,
        MasterDbContext masterDb,
        ICurrentUserService currentUserService)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _masterDb = masterDb ?? throw new ArgumentNullException(nameof(masterDb));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(
        [FromBody] LoginCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var token = await _mediator.Send(command, cancellationToken);
            return Ok(new { token });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpGet("accountant")]
    [Authorize]
    public async Task<IActionResult> GetAccountantUser(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId;
        if (tenantId == Guid.Empty)
        {
            return Unauthorized();
        }

        var accountant = await _masterDb.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Role == "Accountant", cancellationToken);

        return Ok(new
        {
            HasAccountant = accountant is not null,
            Email = accountant?.Email ?? string.Empty
        });
    }

    [HttpPost("accountant")]
    [Authorize]
    public async Task<IActionResult> UpsertAccountantUser(
        [FromBody] UpsertAccountantRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId;
        if (tenantId == Guid.Empty)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            return BadRequest(new { message = "Debes ingresar un correo válido y una contraseña de al menos 6 caracteres para el Contador." });
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var passwordHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(request.Password)));

        var existingEmailOther = await _masterDb.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email && (u.TenantId != tenantId || u.Role != "Accountant"), cancellationToken);

        if (existingEmailOther is not null)
        {
            return BadRequest(new { message = $"El correo '{email}' ya está en uso por otro usuario del sistema." });
        }

        var currentAccountant = await _masterDb.Users
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Role == "Accountant", cancellationToken);

        if (currentAccountant is not null)
        {
            _masterDb.Users.Remove(currentAccountant);
            await _masterDb.SaveChangesAsync(cancellationToken);
        }

        var newAccountant = MicroSaaS.Core.Entities.User.Create(
            tenantId: tenantId,
            email: email,
            passwordHash: passwordHash,
            role: "Accountant");

        await _masterDb.Users.AddAsync(newAccountant, cancellationToken);
        await _masterDb.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            Email = newAccountant.Email,
            Message = $"Credenciales del Contador ({newAccountant.Email}) habilitadas exitosamente."
        });
    }

    [HttpGet("users")]
    [Authorize]
    public async Task<IActionResult> GetTenantUsers(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId;
        if (tenantId == Guid.Empty)
        {
            return Unauthorized();
        }

        await _masterDb.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE public."Users" ADD COLUMN IF NOT EXISTS "Role" character varying(50) NOT NULL DEFAULT 'Owner';
            ALTER TABLE public."Users" ADD COLUMN IF NOT EXISTS "CashRegisterId" character varying(50) NULL;
            ALTER TABLE public."Users" ADD COLUMN IF NOT EXISTS "PaymentDeviceId" character varying(80) NULL;
            UPDATE public."Users" SET "CashRegisterId" = 'CAJA-01' WHERE "CashRegisterId" IS NULL AND "Role" <> 'Accountant';
            UPDATE public."Users" SET "PaymentDeviceId" = 'MP-POINT-01' WHERE "PaymentDeviceId" IS NULL AND "Role" <> 'Accountant';
            """,
            cancellationToken);

        var users = await _masterDb.Users
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId)
            .OrderBy(u => u.Role)
            .ThenBy(u => u.Email)
            .Select(u => new TenantUserDto(
                u.Id,
                u.Email,
                u.Role,
                u.CashRegisterId,
                u.PaymentDeviceId,
                DateTime.UtcNow))
            .ToListAsync(cancellationToken);

        return Ok(users);
    }

    [HttpPost("users")]
    [Authorize]
    public async Task<IActionResult> UpsertTenantUser(
        [FromBody] UpsertTenantUserRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId;
        if (tenantId == Guid.Empty)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            request.Password.Length < 6)
        {
            return BadRequest(new { message = "Debes ingresar un correo válido y una contraseña de al menos 6 caracteres." });
        }

        var normalizedRole = (request.Role ?? "Cashier").Trim() switch
        {
            "Owner" or "Administrador" => "Owner",
            "Cashier" or "Cajero" => "Cashier",
            "Accountant" or "Contador" => "Accountant",
            _ => "Cashier"
        };

        var email = request.Email.Trim().ToLowerInvariant();
        var passwordHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(request.Password)));

        var existingInOtherTenant = await _masterDb.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email && u.TenantId != tenantId, cancellationToken);

        if (existingInOtherTenant is not null)
        {
            return BadRequest(new { message = $"El correo '{email}' ya pertenece a otra empresa registrada." });
        }

        var existingInSameTenant = await _masterDb.Users
            .FirstOrDefaultAsync(u => u.Email == email && u.TenantId == tenantId, cancellationToken);

        var isUpdate = existingInSameTenant is not null;
        if (existingInSameTenant is not null)
        {
            _masterDb.Users.Remove(existingInSameTenant);
            await _masterDb.SaveChangesAsync(cancellationToken);
        }

        var createdUser = MicroSaaS.Core.Entities.User.Create(
            tenantId: tenantId,
            email: email,
            passwordHash: passwordHash,
            role: normalizedRole,
            cashRegisterId: request.CashRegisterId,
            paymentDeviceId: request.PaymentDeviceId);

        await _masterDb.Users.AddAsync(createdUser, cancellationToken);
        await _masterDb.SaveChangesAsync(cancellationToken);

        var roleLabel = normalizedRole switch
        {
            "Owner" => "Administrador",
            "Cashier" => "Cajero POS",
            "Accountant" => "Contador Auditor",
            _ => normalizedRole
        };

        return Ok(new
        {
            Message = isUpdate
                ? $"Usuario '{createdUser.Email}' actualizado con rol {roleLabel}."
                : $"Usuario '{createdUser.Email}' creado exitosamente con rol {roleLabel}.",
            User = new TenantUserDto(
                createdUser.Id,
                createdUser.Email,
                createdUser.Role,
                createdUser.CashRegisterId,
                createdUser.PaymentDeviceId,
                DateTime.UtcNow)
        });
    }

    [HttpDelete("users/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteTenantUser(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId;
        if (tenantId == Guid.Empty)
        {
            return Unauthorized();
        }

        var user = await _masterDb.Users
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId, cancellationToken);

        if (user is null)
        {
            return NotFound(new { message = "El usuario especificado no existe en esta empresa." });
        }

        if (user.Id == _currentUserService.UserId)
        {
            return BadRequest(new { message = "No puedes eliminar tu propia cuenta con sesión activa." });
        }

        if (user.Role == "Owner")
        {
            var ownersCount = await _masterDb.Users
                .CountAsync(u => u.TenantId == tenantId && u.Role == "Owner", cancellationToken);

            if (ownersCount <= 1)
            {
                return BadRequest(new { message = "La empresa debe mantener al menos un usuario con rol Administrador." });
            }
        }

        _masterDb.Users.Remove(user);
        await _masterDb.SaveChangesAsync(cancellationToken);

        return Ok(new { message = $"Usuario '{user.Email}' eliminado correctamente." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.NewPassword) ||
            request.NewPassword.Length < 6)
        {
            return BadRequest(new { message = "Ingresa un correo válido y una nueva contraseña de al menos 6 caracteres." });
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var existingUser = await _masterDb.Users
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (existingUser is null)
        {
            return NotFound(new { message = $"No encontramos una cuenta registrada con el correo '{email}'." });
        }

        var tenantId = existingUser.TenantId;
        var role = existingUser.Role;
        var newHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(request.NewPassword)));

        _masterDb.Users.Remove(existingUser);
        await _masterDb.SaveChangesAsync(cancellationToken);

        var updatedUser = MicroSaaS.Core.Entities.User.Create(
            tenantId: tenantId,
            email: email,
            passwordHash: newHash,
            role: role);

        await _masterDb.Users.AddAsync(updatedUser, cancellationToken);
        await _masterDb.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = $"Contraseña restablecida exitosamente para '{email}'. Ya puedes iniciar sesión."
        });
    }
}
