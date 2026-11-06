using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MicroSaaS.Core.Interfaces;

namespace MicroSaaS.Infrastructure.Authentication;

/// <summary>
/// Implementación de <see cref="ICurrentUserService"/> que extrae la identidad, el TenantId y el Rol
/// del usuario autenticado desde <see cref="IHttpContextAccessor"/>.
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

    public Guid TenantId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirstValue(JwtProvider.TenantIdClaimName);
            return Guid.TryParse(claim, out var tenantId) ? tenantId : Guid.Empty;
        }
    }

    public Guid UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var claim = user?.FindFirstValue("sub")
                        ?? user?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(claim, out var userId) ? userId : Guid.Empty;
        }
    }

    public string Role
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var role = user?.FindFirstValue("role")
                       ?? user?.FindFirstValue(ClaimTypes.Role);
            return !string.IsNullOrWhiteSpace(role) ? role : "Owner";
        }
    }
}

